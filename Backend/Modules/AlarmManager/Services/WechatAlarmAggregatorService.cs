using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ChillerPlant.Modules.AlarmManager.Models;
using ChillerPlant.Modules.Shared.Events;

namespace ChillerPlant.Modules.AlarmManager.Services
{
    public class WechatAlarmAggregatorService : BackgroundService
    {
        private readonly ILogger<WechatAlarmAggregatorService> _logger;
        private readonly IMediator _mediator;
        private readonly WechatPushConfig _config;
        private readonly ConcurrentDictionary<string, List<WechatPushQueueItem>> _alarmGroups;
        private readonly SemaphoreSlim _semaphore;
        private readonly HttpClient _httpClient;

        public WechatAlarmAggregatorService(
            ILogger<WechatAlarmAggregatorService> logger,
            IMediator mediator,
            IOptions<WechatPushConfig> config)
        {
            _logger = logger;
            _mediator = mediator;
            _config = config.Value;
            _alarmGroups = new ConcurrentDictionary<string, List<WechatPushQueueItem>>();
            _semaphore = new SemaphoreSlim(0, 1000);
            _httpClient = new HttpClient();
        }

        public void EnqueueAlarm(long alarmId, string alarmType, int alarmLevel, string alarmMessage, string deviceName)
        {
            var item = new WechatPushQueueItem
            {
                AlarmId = alarmId,
                AlarmType = alarmType,
                AlarmLevel = alarmLevel,
                AlarmMessage = alarmMessage,
                DeviceName = deviceName,
                AddedAt = DateTime.Now
            };

            var groupKey = alarmLevel == 2 ? "critical" : "normal";
            
            _alarmGroups.AddOrUpdate(
                groupKey,
                _ => new List<WechatPushQueueItem> { item },
                (_, list) => { lock (list) { list.Add(item); } return list; }
            );

            _semaphore.Release();
            _logger.LogInformation($"Alarm {alarmId} enqueued for WeChat push, group: {groupKey}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"WechatAlarmAggregatorService started, aggregate window: {_config.AggregateWindowSeconds}s");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.WhenAny(
                        _semaphore.WaitAsync(stoppingToken),
                        Task.Delay(TimeSpan.FromSeconds(_config.AggregateWindowSeconds), stoppingToken));

                    await ProcessAggregatedAlarms(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in WeChat aggregator: {ex.Message}");
                    await Task.Delay(5000, stoppingToken);
                }
            }

            _logger.LogInformation("WechatAlarmAggregatorService stopped");
        }

        private async Task ProcessAggregatedAlarms(CancellationToken cancellationToken)
        {
            var now = DateTime.Now;

            foreach (var groupKey in _alarmGroups.Keys.ToList())
            {
                if (_alarmGroups.TryGetValue(groupKey, out var list))
                {
                    List<WechatPushQueueItem> itemsToPush;
                    lock (list)
                    {
                        itemsToPush = list
                            .Where(x => (now - x.AddedAt).TotalSeconds >= _config.AggregateWindowSeconds / 2)
                            .Take(_config.MaxAlarmsPerMessage)
                            .ToList();
                        
                        foreach (var item in itemsToPush)
                        {
                            list.Remove(item);
                        }
                    }

                    if (itemsToPush.Any())
                    {
                        await PushAlarmsToWechat(itemsToPush, groupKey, cancellationToken);
                    }
                }
            }
        }

        private async Task PushAlarmsToWechat(List<WechatPushQueueItem> alarms, string groupKey, CancellationToken cancellationToken)
        {
            try
            {
                var message = BuildWechatMessage(alarms, groupKey);
                var success = await SendWechatNotification(message, cancellationToken);

                foreach (var alarm in alarms)
                {
                    await _mediator.Publish(new AlarmPushedEvent
                    {
                        AlarmId = alarm.AlarmId,
                        Success = success,
                        PushedAt = DateTime.Now
                    }, cancellationToken);
                }

                if (success)
                {
                    _logger.LogInformation($"Successfully pushed {alarms.Count} alarms to WeChat, group: {groupKey}");
                }
                else
                {
                    _logger.LogWarning($"Failed to push {alarms.Count} alarms to WeChat, group: {groupKey}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error pushing alarms to WeChat: {ex.Message}");
            }
        }

        private string BuildWechatMessage(List<WechatPushQueueItem> alarms, string groupKey)
        {
            var levelText = groupKey == "critical" ? "🔴 严重告警" : "🟡 一般告警";
            var sb = new StringBuilder();
            
            sb.AppendLine($"【{levelText}】冷站系统告警汇总");
            sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"告警数量: {alarms.Count}");
            sb.AppendLine();

            for (int i = 0; i < alarms.Count; i++)
            {
                var alarm = alarms[i];
                var levelIcon = alarm.AlarmLevel == 2 ? "🔴" : "🟡";
                sb.AppendLine($"{i + 1}. {levelIcon} {alarm.DeviceName ?? "系统"}");
                sb.AppendLine($"   类型: {alarm.AlarmType}");
                sb.AppendLine($"   描述: {alarm.AlarmMessage}");
                sb.AppendLine($"   时间: {alarm.AddedAt:HH:mm:ss}");
                if (i < alarms.Count - 1) sb.AppendLine();
            }

            return sb.ToString();
        }

        private async Task<bool> SendWechatNotification(string message, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_config.WebhookUrl))
            {
                _logger.LogWarning("WeChat webhook URL not configured, skipping push");
                return false;
            }

            try
            {
                var payload = new
                {
                    msgtype = "text",
                    text = new
                    {
                        content = message
                    }
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(_config.WebhookUrl, content, cancellationToken);
                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var wechatResponse = JsonConvert.DeserializeObject<WechatApiResponse>(result);
                    return wechatResponse?.Errcode == 0;
                }

                _logger.LogWarning($"WeChat API error: {response.StatusCode} - {result}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"WeChat push exception: {ex.Message}");
                return false;
            }
        }

        private class WechatApiResponse
        {
            public int Errcode { get; set; }
            public string Errmsg { get; set; }
        }
    }
}
