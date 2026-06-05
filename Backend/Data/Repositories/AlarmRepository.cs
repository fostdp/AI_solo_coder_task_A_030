using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ChillerPlant.Models;

namespace ChillerPlant.Data.Repositories
{
    public class AlarmRepository : IAlarmRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;
        private readonly AppSettings _appSettings;
        private readonly WechatWorkSettings _wechatSettings;
        private readonly IHttpClientFactory _httpClientFactory;

        public AlarmRepository(ApplicationDbContext context, string connectionString, 
            IOptions<AppSettings> appSettings, IOptions<WechatWorkSettings> wechatSettings,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _connectionString = connectionString;
            _appSettings = appSettings.Value;
            _wechatSettings = wechatSettings.Value;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<List<AlarmDto>> GetActiveAlarmsAsync()
        {
            return await _context.Alarms
                .Include(a => a.Device)
                .Where(a => a.Status == 1)
                .OrderBy(a => a.AlarmLevel)
                .ThenByDescending(a => a.StartTime)
                .Select(a => new AlarmDto
                {
                    AlarmId = a.AlarmId,
                    AlarmCode = a.AlarmCode,
                    AlarmLevel = a.AlarmLevel,
                    DeviceId = a.DeviceId,
                    DeviceName = a.Device.DeviceName,
                    DeviceCode = a.Device.DeviceCode,
                    AlarmType = a.AlarmType,
                    AlarmMessage = a.AlarmMessage,
                    ParameterName = a.ParameterName,
                    ActualValue = a.ActualValue,
                    ThresholdValue = a.ThresholdValue,
                    StartTime = a.StartTime,
                    Duration = a.Duration,
                    Status = a.Status,
                    AckStatus = a.AckStatus
                })
                .ToListAsync();
        }

        public async Task<List<AlarmDto>> GetAlarmHistoryAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Alarms
                .Include(a => a.Device)
                .Where(a => a.StartTime >= startDate && a.StartTime <= endDate)
                .OrderByDescending(a => a.StartTime)
                .Select(a => new AlarmDto
                {
                    AlarmId = a.AlarmId,
                    AlarmCode = a.AlarmCode,
                    AlarmLevel = a.AlarmLevel,
                    DeviceId = a.DeviceId,
                    DeviceName = a.Device.DeviceName,
                    DeviceCode = a.Device.DeviceCode,
                    AlarmType = a.AlarmType,
                    AlarmMessage = a.AlarmMessage,
                    ParameterName = a.ParameterName,
                    ActualValue = a.ActualValue,
                    ThresholdValue = a.ThresholdValue,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Duration = a.Duration,
                    Status = a.Status,
                    AckStatus = a.AckStatus
                })
                .ToListAsync();
        }

        public async Task<Alarm> CreateAlarmAsync(Alarm alarm)
        {
            _context.Alarms.Add(alarm);
            await _context.SaveChangesAsync();
            return alarm;
        }

        public async Task UpdateAlarmStatusAsync(long alarmId, int status)
        {
            var alarm = await _context.Alarms.FindAsync(alarmId);
            if (alarm != null)
            {
                alarm.Status = status;
                if (status == 0)
                {
                    alarm.EndTime = DateTime.Now;
                    alarm.Duration = (int)(alarm.EndTime.Value - alarm.StartTime).TotalMinutes;
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task AcknowledgeAlarmAsync(long alarmId, string ackBy)
        {
            var alarm = await _context.Alarms.FindAsync(alarmId);
            if (alarm != null)
            {
                alarm.AckStatus = 1;
                alarm.AckBy = ackBy;
                alarm.AckTime = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder)
        {
            workOrder.OrderNo = $"WO{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();

            if (workOrder.AlarmId.HasValue)
            {
                var alarm = await _context.Alarms.FindAsync(workOrder.AlarmId.Value);
                if (alarm != null)
                {
                    alarm.WorkOrderId = workOrder.WorkOrderId;
                    await _context.SaveChangesAsync();
                }
            }

            return workOrder;
        }

        public async Task<List<WorkOrderDto>> GetWorkOrdersAsync(int? status = null)
        {
            var query = _context.WorkOrders
                .Include(w => w.Device)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(w => w.Status == status.Value);
            }

            return await query
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new WorkOrderDto
                {
                    WorkOrderId = w.WorkOrderId,
                    OrderNo = w.OrderNo,
                    AlarmId = w.AlarmId,
                    DeviceId = w.DeviceId,
                    DeviceName = w.Device.DeviceName,
                    OrderType = w.OrderType,
                    Priority = w.Priority,
                    PriorityText = w.Priority == 1 ? "紧急" : w.Priority == 2 ? "高" : w.Priority == 3 ? "中" : "低",
                    Title = w.Title,
                    Description = w.Description,
                    Status = w.Status,
                    StatusText = w.Status == 0 ? "待处理" : w.Status == 1 ? "处理中" : w.Status == 2 ? "已完成" : "已关闭",
                    Assignee = w.Assignee,
                    CreatedAt = w.CreatedAt
                })
                .ToListAsync();
        }

        public async Task UpdateWorkOrderStatusAsync(long workOrderId, int status, string remark = null)
        {
            var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
            if (workOrder != null)
            {
                workOrder.Status = status;
                workOrder.UpdatedAt = DateTime.Now;
                if (!string.IsNullOrEmpty(remark))
                {
                    workOrder.Remark = remark;
                }
                if (status == 1 && workOrder.StartTime == null)
                {
                    workOrder.StartTime = DateTime.Now;
                }
                if (status == 2 && workOrder.CompleteTime == null)
                {
                    workOrder.CompleteTime = DateTime.Now;
                }
                if (status == 3)
                {
                    workOrder.CloseTime = DateTime.Now;
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task CheckAndCreateAlarmsAsync()
        {
            var now = DateTime.Now;
            var level1Duration = TimeSpan.FromMinutes(_appSettings.AlarmLevel1DurationMinutes);
            var level2Duration = TimeSpan.FromMinutes(_appSettings.AlarmLevel2DurationMinutes);

            var deviceIds = await _context.Devices.Select(d => d.DeviceId).ToListAsync();

            foreach (var deviceId in deviceIds)
            {
                var device = await _context.Devices.FindAsync(deviceId);
                var recentData = await _context.DeviceData
                    .Where(d => d.DeviceId == deviceId && d.Timestamp >= now.AddHours(-1))
                    .OrderByDescending(d => d.Timestamp)
                    .Take(120)
                    .ToListAsync();

                if (!recentData.Any()) continue;

                var thresholds = GetDeviceThresholds(device.DeviceTypeId);
                
                foreach (var threshold in thresholds)
                {
                    var outOfRangeData = recentData
                        .Where(d => IsOutOfRange(d, threshold))
                        .OrderBy(d => d.Timestamp)
                        .ToList();

                    if (!outOfRangeData.Any()) continue;

                    var firstOutOfRange = outOfRangeData.First();
                    var duration = now - firstOutOfRange.Timestamp;

                    if (duration >= level1Duration)
                    {
                        var existingAlarm = await _context.Alarms
                            .FirstOrDefaultAsync(a => a.DeviceId == deviceId && 
                                a.ParameterName == threshold.ParameterName && 
                                a.Status == 1 &&
                                a.AlarmLevel == 1);

                        if (existingAlarm == null)
                        {
                            var alarm = new Alarm
                            {
                                AlarmCode = $"L1-{device.DeviceCode}-{threshold.ParameterName}",
                                AlarmLevel = 1,
                                DeviceId = deviceId,
                                AlarmType = "参数超限",
                                AlarmMessage = $"{device.DeviceName} {threshold.ParameterName} 持续超限超过{_appSettings.AlarmLevel1DurationMinutes}分钟",
                                ParameterName = threshold.ParameterName,
                                ActualValue = GetParameterValue(recentData.First(), threshold.ParameterName),
                                ThresholdValue = threshold.ThresholdValue,
                                StartTime = firstOutOfRange.Timestamp,
                                Duration = (int)duration.TotalMinutes
                            };

                            await CreateAlarmAsync(alarm);
                            await PushAlarmToWechatAsync(alarm);
                            await CreateWorkOrderFromAlarmAsync(alarm);
                        }
                        else
                        {
                            existingAlarm.Duration = (int)duration.TotalMinutes;
                            existingAlarm.ActualValue = GetParameterValue(recentData.First(), threshold.ParameterName);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
            }

            await CheckSystemCOPAlarmAsync(now, level2Duration);
        }

        private async Task CheckSystemCOPAlarmAsync(DateTime now, TimeSpan level2Duration)
        {
            var threshold = _appSettings.SystemCOPAlarmThreshold * _appSettings.DesignSystemCOP;
            var recentEfficiency = await _context.SystemEfficiencies
                .Where(s => s.Timestamp >= now.AddHours(-1) && s.SystemCOP < threshold)
                .OrderBy(s => s.Timestamp)
                .ToListAsync();

            if (!recentEfficiency.Any()) return;

            var firstBelow = recentEfficiency.First();
            var duration = now - firstBelow.Timestamp;

            if (duration >= level2Duration)
            {
                var existingAlarm = await _context.Alarms
                    .FirstOrDefaultAsync(a => a.AlarmType == "系统COP过低" && a.Status == 1 && a.AlarmLevel == 2);

                if (existingAlarm == null)
                {
                    var latest = recentEfficiency.Last();
                    var alarm = new Alarm
                    {
                        AlarmCode = $"L2-SYS-COP-{now:yyyyMMdd}",
                        AlarmLevel = 2,
                        AlarmType = "系统COP过低",
                        AlarmMessage = $"系统COP持续低于设计值{_appSettings.SystemCOPAlarmThreshold * 100}%超过{_appSettings.AlarmLevel2DurationMinutes}分钟",
                        ParameterName = "系统COP",
                        ActualValue = latest.SystemCOP,
                        ThresholdValue = threshold,
                        StartTime = firstBelow.Timestamp,
                        Duration = (int)duration.TotalMinutes
                    };

                    await CreateAlarmAsync(alarm);
                    await PushAlarmToWechatAsync(alarm);
                    await CreateWorkOrderFromAlarmAsync(alarm);
                }
                else
                {
                    existingAlarm.Duration = (int)duration.TotalMinutes;
                    var latest = recentEfficiency.Last();
                    existingAlarm.ActualValue = latest.SystemCOP;
                    await _context.SaveChangesAsync();
                }
            }
        }

        private async Task CreateWorkOrderFromAlarmAsync(Alarm alarm)
        {
            var workOrder = new WorkOrder
            {
                AlarmId = alarm.AlarmId,
                DeviceId = alarm.DeviceId,
                OrderType = "告警处理",
                Priority = alarm.AlarmLevel == 1 ? 2 : 1,
                Title = alarm.AlarmMessage,
                Description = $"告警代码: {alarm.AlarmCode}\n告警类型: {alarm.AlarmType}\n参数: {alarm.ParameterName}\n当前值: {alarm.ActualValue}\n阈值: {alarm.ThresholdValue}\n开始时间: {alarm.StartTime:yyyy-MM-dd HH:mm:ss}",
                Status = 0,
                CreatedBy = "System"
            };

            await CreateWorkOrderAsync(workOrder);
        }

        private List<(string ParameterName, decimal ThresholdValue, bool IsUpper)> GetDeviceThresholds(int deviceTypeId)
        {
            var thresholds = new List<(string, decimal, bool)>();
            
            switch (deviceTypeId)
            {
                case 1:
                case 2:
                    thresholds.Add(("SupplyWaterTemp", 8.0m, true));
                    thresholds.Add(("SupplyWaterTemp", 5.0m, false));
                    thresholds.Add(("ReturnWaterTemp", 15.0m, true));
                    thresholds.Add(("CoolingWaterOutTemp", 37.0m, true));
                    thresholds.Add(("LoadRate", 100.0m, true));
                    thresholds.Add(("Vibration", 5.0m, true));
                    thresholds.Add(("Current", 1500.0m, true));
                    break;
                case 3:
                    thresholds.Add(("Vibration", 6.0m, true));
                    thresholds.Add(("Current", 50.0m, true));
                    break;
                case 4:
                case 5:
                    thresholds.Add(("SupplyPressure", 1.2m, true));
                    thresholds.Add(("SupplyPressure", 0.8m, false));
                    thresholds.Add(("Vibration", 4.5m, true));
                    thresholds.Add(("Current", 200.0m, true));
                    thresholds.Add(("Frequency", 55.0m, true));
                    break;
            }

            return thresholds;
        }

        private bool IsOutOfRange(DeviceData data, (string ParameterName, decimal ThresholdValue, bool IsUpper) threshold)
        {
            var value = GetParameterValue(data, threshold.ParameterName);
            if (!value.HasValue) return false;

            return threshold.IsUpper ? value > threshold.ThresholdValue : value < threshold.ThresholdValue;
        }

        private decimal? GetParameterValue(DeviceData data, string parameterName)
        {
            return parameterName switch
            {
                "SupplyWaterTemp" => data.SupplyWaterTemp,
                "ReturnWaterTemp" => data.ReturnWaterTemp,
                "CoolingWaterInTemp" => data.CoolingWaterInTemp,
                "CoolingWaterOutTemp" => data.CoolingWaterOutTemp,
                "FlowRate" => data.FlowRate,
                "SupplyPressure" => data.SupplyPressure,
                "ReturnPressure" => data.ReturnPressure,
                "LoadRate" => data.LoadRate,
                "Frequency" => data.Frequency,
                "Vibration" => data.Vibration,
                "Current" => data.Current,
                "Voltage" => data.Voltage,
                "Power" => data.Power,
                "COP" => data.COP,
                _ => null
            };
        }

        public async Task PushAlarmToWechatAsync(Alarm alarm)
        {
            if (!_wechatSettings.Enabled || string.IsNullOrEmpty(_wechatSettings.WebhookUrl))
            {
                return;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var levelText = alarm.AlarmLevel == 1 ? "一级告警" : "二级告警";
                var deviceName = alarm.DeviceId.HasValue ? 
                    (await _context.Devices.FindAsync(alarm.DeviceId.Value))?.DeviceName : "系统";

                var message = new
                {
                    msgtype = "markdown",
                    markdown = new
                    {
                        content = $@"### <font color=""warning"">{levelText}</font>
**告警时间**: {alarm.StartTime:yyyy-MM-dd HH:mm:ss}
**告警设备**: {deviceName}
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
                var response = await client.PostAsync(_wechatSettings.WebhookUrl, content);
                var result = await response.Content.ReadAsStringAsync();

                alarm.WechatPushStatus = response.IsSuccessStatusCode ? 1 : 2;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"推送微信告警失败: {ex.Message}");
                alarm.WechatPushStatus = 2;
                await _context.SaveChangesAsync();
            }
        }
    }

    public class AppSettings
    {
        public decimal DesignSystemCOP { get; set; }
        public int AlarmLevel1DurationMinutes { get; set; }
        public int AlarmLevel2DurationMinutes { get; set; }
        public int OptimizationIntervalMinutes { get; set; }
        public int DataReportIntervalSeconds { get; set; }
        public double EnergyEfficiencyThreshold { get; set; }
        public double SystemCOPAlarmThreshold { get; set; }
    }

    public class WechatWorkSettings
    {
        public bool Enabled { get; set; }
        public string WebhookUrl { get; set; }
        public List<string> MentionedList { get; set; } = new List<string>();
        public List<string> MentionedMobileList { get; set; } = new List<string>();
    }
}
