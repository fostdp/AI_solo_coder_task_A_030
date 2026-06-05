using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ChillerPlant.Data;
using ChillerPlant.Data.Repositories;
using ChillerPlant.Models;

namespace ChillerPlant.Services
{
    public class WechatAlarmAggregatorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WechatAlarmAggregatorService> _logger;
        private readonly WechatWorkSettings _wechatSettings;

        private ConcurrentDictionary<string, AlarmAggregateGroup> _alarmGroups;
        private int _aggregateWindowSeconds = 60;
        private int _maxAlarmsPerMessage = 10;
        private int _pushIntervalSeconds = 5;

        public WechatAlarmAggregatorService(IServiceProvider serviceProvider,
            ILogger<WechatAlarmAggregatorService> logger,
            IOptions<WechatWorkSettings> wechatSettings)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _wechatSettings = wechatSettings.Value;

            _alarmGroups = new ConcurrentDictionary<string, AlarmAggregateGroup>();
        }

        public void EnqueueAlarm(Alarm alarm, string deviceName)
        {
            if (!_wechatSettings.Enabled || string.IsNullOrEmpty(_wechatSettings.WebhookUrl))
            {
                return;
            }

            var groupKey = $"{alarm.AlarmLevel}_{alarm.AlarmType}_{alarm.ParameterName}";

            var aggregateGroup = _alarmGroups.GetOrAdd(groupKey, _ => new AlarmAggregateGroup
            {
                AlarmLevel = alarm.AlarmLevel,
                AlarmType = alarm.AlarmType,
                ParameterName = alarm.ParameterName,
                FirstAlarmTime = DateTime.Now,
                Alarms = new List<AlarmItem>()
            });

            lock (aggregateGroup)
            {
                aggregateGroup.Alarms.Add(new AlarmItem
                {
                    AlarmId = alarm.AlarmId,
                    DeviceName = deviceName,
                    AlarmMessage = alarm.AlarmMessage,
                    ActualValue = alarm.ActualValue,
                    ThresholdValue = alarm.ThresholdValue,
                    AlarmTime = alarm.StartTime
                });

                if (aggregateGroup.Alarms.Count == 1)
                {
                    aggregateGroup.FirstAlarmTime = DateTime.Now;
                }

                _logger.LogInformation($"Alarm enqueued for aggregation: {alarm.AlarmType}, device: {deviceName}, group size: {aggregateGroup.Alarms.Count}");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WeChat Alarm Aggregator Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAggregatedAlarms(stoppingToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.LogError(ex, "Error processing aggregated alarms");
                }

                await Task.Delay(TimeSpan.FromSeconds(_pushIntervalSeconds), stoppingToken);
            }

            _logger.LogInformation("WeChat Alarm Aggregator Service stopped.");
        }

        private async Task ProcessAggregatedAlarms(CancellationToken stoppingToken)
        {
            var now = DateTime.Now;
            var groupsToProcess = new List<AlarmAggregateGroup>();

            foreach (var kvp in _alarmGroups)
            {
                var group = kvp.Value;
                lock (group)
                {
                    var elapsed = (now - group.FirstAlarmTime).TotalSeconds;
                    var hasEnoughAlarms = group.Alarms.Count >= _maxAlarmsPerMessage;
                    var windowExpired = elapsed >= _aggregateWindowSeconds;
                    var hasAnyAlarms = group.Alarms.Count > 0;

                    if (hasAnyAlarms && (hasEnoughAlarms || windowExpired))
                    {
                        groupsToProcess.Add(new AlarmAggregateGroup
                        {
                            AlarmLevel = group.AlarmLevel,
                            AlarmType = group.AlarmType,
                            ParameterName = group.ParameterName,
                            FirstAlarmTime = group.FirstAlarmTime,
                            Alarms = new List<AlarmItem>(group.Alarms)
                        });

                        group.Alarms.Clear();
                    }
                }
            }

            foreach (var group in groupsToProcess)
            {
                await PushAggregatedAlarm(group, stoppingToken);

                await UpdateAlarmPushStatus(group.AlarmIds, stoppingToken);
            }
        }

        private async Task PushAggregatedAlarm(AlarmAggregateGroup group, CancellationToken stoppingToken)
        {
            try
            {
                var client = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
                var levelText = group.AlarmLevel == 1 ? "一级告警" : "二级告警";
                var alarmCount = group.Alarms.Count;

                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine($@"### <font color=""warning"">{levelText} - 聚合通知</font>");
                messageBuilder.AppendLine($"**告警类型**: {group.AlarmType}");
                messageBuilder.AppendLine($"**参数名称**: {group.ParameterName}");
                messageBuilder.AppendLine($"**告警数量**: {alarmCount} 台设备");
                messageBuilder.AppendLine($"**首次告警**: {group.FirstAlarmTime:yyyy-MM-dd HH:mm:ss}");
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("**详细信息**:");

                var displayAlarms = group.Alarms.Take(_maxAlarmsPerMessage).ToList();
                for (int i = 0; i < displayAlarms.Count; i++)
                {
                    var alarm = displayAlarms[i];
                    messageBuilder.AppendLine($"> {i + 1}. **{alarm.DeviceName}**");
                    messageBuilder.AppendLine($">    内容: {alarm.AlarmMessage}");
                    messageBuilder.AppendLine($">    当前值: {alarm.ActualValue} | 阈值: {alarm.ThresholdValue}");
                    messageBuilder.AppendLine($">    时间: {alarm.AlarmTime:HH:mm:ss}");
                }

                if (alarmCount > _maxAlarmsPerMessage)
                {
                    messageBuilder.AppendLine();
                    messageBuilder.AppendLine($"> 另外还有 {alarmCount - _maxAlarmsPerMessage} 台设备同类告警，详情请查看系统。");
                }

                var message = new
                {
                    msgtype = "markdown",
                    markdown = new
                    {
                        content = messageBuilder.ToString()
                    },
                    mentioned_list = _wechatSettings.MentionedList,
                    mentioned_mobile_list = _wechatSettings.MentionedMobileList
                };

                var json = JsonConvert.SerializeObject(message);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(_wechatSettings.WebhookUrl, content, stoppingToken);
                var result = await response.Content.ReadAsStringAsync(stoppingToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Successfully pushed aggregated alarm: {group.AlarmType}, count: {alarmCount}");
                }
                else
                {
                    _logger.LogWarning($"Failed to push aggregated alarm: {result}");
                    await RetryWithSingleAlarms(group, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error pushing aggregated alarm: {group.AlarmType}");
                await RetryWithSingleAlarms(group, stoppingToken);
            }
        }

        private async Task RetryWithSingleAlarms(AlarmAggregateGroup group, CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Retrying with individual pushes for {group.AlarmType}");

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            foreach (var alarmItem in group.Alarms)
            {
                try
                {
                    var alarm = await context.Alarms.FindAsync(new object[] { alarmItem.AlarmId }, stoppingToken);
                    if (alarm != null)
                    {
                        var client = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
                        var levelText = alarm.AlarmLevel == 1 ? "一级告警" : "二级告警";

                        var message = new
                        {
                            msgtype = "markdown",
                            markdown = new
                            {
                                content = $@"### <font color=""warning"">{levelText}</font>
**告警时间**: {alarm.StartTime:yyyy-MM-dd HH:mm:ss}
**告警设备**: {alarmItem.DeviceName}
**告警类型**: {alarm.AlarmType}
**告警内容**: {alarm.AlarmMessage}
**参数名称**: {alarm.ParameterName}
**当前值**: {alarm.ActualValue}
**阈值**: {alarm.ThresholdValue}"
                            },
                            mentioned_list = _wechatSettings.MentionedList,
                            mentioned_mobile_list = _wechatSettings.MentionedMobileList
                        };

                        var json = JsonConvert.SerializeObject(message);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        var response = await client.PostAsync(_wechatSettings.WebhookUrl, content, stoppingToken);

                        if (response.IsSuccessStatusCode)
                        {
                            alarm.WechatPushStatus = 1;
                        }
                        else
                        {
                            alarm.WechatPushStatus = 2;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to push individual alarm {alarmItem.AlarmId}");
                }

                await Task.Delay(1000, stoppingToken);
            }

            await context.SaveChangesAsync(stoppingToken);
        }

        private async Task UpdateAlarmPushStatus(List<long> alarmIds, CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var alarms = await context.Alarms
                    .Where(a => alarmIds.Contains(a.AlarmId))
                    .ToListAsync(stoppingToken);

                foreach (var alarm in alarms)
                {
                    alarm.WechatPushStatus = 1;
                }

                await context.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating alarm push status");
            }
        }
    }

    public class AlarmAggregateGroup
    {
        public int AlarmLevel { get; set; }
        public string AlarmType { get; set; }
        public string ParameterName { get; set; }
        public DateTime FirstAlarmTime { get; set; }
        public List<AlarmItem> Alarms { get; set; } = new List<AlarmItem>();

        public List<long> AlarmIds => Alarms.Select(a => a.AlarmId).ToList();
    }

    public class AlarmItem
    {
        public long AlarmId { get; set; }
        public string DeviceName { get; set; }
        public string AlarmMessage { get; set; }
        public decimal? ActualValue { get; set; }
        public decimal? ThresholdValue { get; set; }
        public DateTime AlarmTime { get; set; }
    }
}
