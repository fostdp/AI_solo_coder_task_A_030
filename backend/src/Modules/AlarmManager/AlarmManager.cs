using MediatR;
using ChillerPlantOptimization.Contracts.Events;
using ChillerPlantOptimization.Models;
using ChillerPlantOptimization.Repositories;

namespace ChillerPlantOptimization.Modules.AlarmManager;

public class ParameterExceedanceTracker
{
    public string ParameterName { get; set; } = string.Empty;
    public decimal CurrentValue { get; set; }
    public decimal ThresholdValue { get; set; }
    public DateTime ExceedanceStartTime { get; set; }
    public bool AlarmTriggered { get; set; }
}

public class AlarmManager : IAlarmManager
{
    private readonly IAlarmRepository _alarmRepository;
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly IEfficiencyRepository _efficiencyRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly INotificationService _notificationService;
    private readonly IOptions<AlarmSettings> _alarmSettings;
    private readonly IOptions<SystemSettings> _systemSettings;
    private readonly IMediator _mediator;
    private readonly ILogger<AlarmManager> _logger;

    private readonly Dictionary<string, ParameterExceedanceTracker> _parameterTrackers = new();
    private DateTime? _lowEfficiencyStart;
    private bool _lowEfficiencyAlarmTriggered;

    public AlarmManager(
        IAlarmRepository alarmRepository,
        IWorkOrderRepository workOrderRepository,
        IEfficiencyRepository efficiencyRepository,
        IDeviceRepository deviceRepository,
        INotificationService notificationService,
        IOptions<AlarmSettings> alarmSettings,
        IOptions<SystemSettings> systemSettings,
        IMediator mediator,
        ILogger<AlarmManager> logger)
    {
        _alarmRepository = alarmRepository;
        _workOrderRepository = workOrderRepository;
        _efficiencyRepository = efficiencyRepository;
        _deviceRepository = deviceRepository;
        _notificationService = notificationService;
        _alarmSettings = alarmSettings;
        _systemSettings = systemSettings;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task MonitorAndProcessAlarmsAsync(DateTime timestamp)
    {
        _logger.LogDebug("开始告警检测，时间: {Time}", timestamp);

        var activeAlarms = await _alarmRepository.GetActiveAlarmsAsync();
        var activeAlarmList = activeAlarms.ToList();

        await MonitorParameterAlarmsAsync(timestamp);
        await MonitorLowEfficiencyAlarmAsync(timestamp);
        await CheckAndResolveAlarmsAsync(timestamp, activeAlarmList);

        await _notificationService.PushAggregatedAlarmsAsync();
    }

    public async Task<IEnumerable<Alarm>> GetActiveAlarmsAsync()
    {
        return await _alarmRepository.GetActiveAlarmsAsync();
    }

    public async Task AcknowledgeAlarmAsync(long id, string acknowledgedBy)
    {
        await _alarmRepository.AcknowledgeAlarmAsync(id, acknowledgedBy);
        _logger.LogInformation("告警 {Id} 已被 {User} 确认", id, acknowledgedBy);
    }

    public async Task ResolveAlarmAsync(long id, string resolvedBy)
    {
        await _alarmRepository.ResolveAlarmAsync(id, resolvedBy);
        _logger.LogInformation("告警 {Id} 已被 {User} 解除", id, resolvedBy);
    }

    public async Task PushAggregatedAlarmsAsync()
    {
        await _notificationService.PushAggregatedAlarmsAsync();
    }

    private async Task MonitorParameterAlarmsAsync(DateTime timestamp)
    {
        var thresholds = await _alarmRepository.GetAlarmThresholdsAsync();
        var thresholdList = thresholds.ToList();

        if (!thresholdList.Any()) return;

        var fiveMinutesAgo = timestamp.AddMinutes(-5);
        var deviceData = await _deviceRepository.GetDeviceDataAsync(fiveMinutesAgo, timestamp);
        var deviceDataList = deviceData.ToList();

        var latestData = deviceDataList
            .GroupBy(d => d.DeviceId)
            .Select(g => g.OrderByDescending(d => d.Timestamp).First())
            .ToList();

        foreach (var data in latestData)
        {
            var deviceThresholds = thresholdList
                .Where(t => t.DeviceTypeId == data.Device?.DeviceTypeId)
                .ToList();

            foreach (var threshold in deviceThresholds)
            {
                await CheckParameterThresholdAsync(data, threshold, timestamp);
            }
        }
    }

    private async Task CheckParameterThresholdAsync(DeviceData data, AlarmThreshold threshold, DateTime timestamp)
    {
        var parameterValue = GetParameterValue(data, threshold.ParameterName);
        if (!parameterValue.HasValue) return;

        var trackerKey = $"{data.DeviceId}_{threshold.ParameterName}";

        bool isExceeded = threshold.ParameterName switch
        {
            "Power" => parameterValue > threshold.MaxValue || parameterValue < threshold.MinValue,
            "SupplyTemperature" => parameterValue > threshold.MaxValue || parameterValue < threshold.MinValue,
            "ReturnTemperature" => parameterValue > threshold.MaxValue || parameterValue < threshold.MinValue,
            "Pressure" => parameterValue > threshold.MaxValue || parameterValue < threshold.MinValue,
            "FlowRate" => parameterValue > threshold.MaxValue || parameterValue < threshold.MinValue,
            "Frequency" => parameterValue > threshold.MaxValue || parameterValue < threshold.MinValue,
            "Current" => parameterValue > threshold.MaxValue || parameterValue < threshold.MinValue,
            "Voltage" => parameterValue > threshold.MaxValue || parameterValue < threshold.MinValue,
            _ => parameterValue > threshold.MaxValue || parameterValue < threshold.MinValue
        };

        if (isExceeded)
        {
            if (!_parameterTrackers.TryGetValue(trackerKey, out var tracker))
            {
                tracker = new ParameterExceedanceTracker
                {
                    ParameterName = threshold.ParameterName,
                    CurrentValue = parameterValue.Value,
                    ThresholdValue = threshold.MaxValue,
                    ExceedanceStartTime = timestamp
                };
                _parameterTrackers[trackerKey] = tracker;
            }

            tracker.CurrentValue = parameterValue.Value;

            var duration = timestamp - tracker.ExceedanceStartTime;
            var triggerDuration = TimeSpan.FromMinutes(_alarmSettings.Value.Level1AlarmDurationMinutes);

            if (!tracker.AlarmTriggered && duration >= triggerDuration)
            {
                await TriggerParameterAlarmAsync(data, threshold, parameterValue.Value, tracker, timestamp);
                tracker.AlarmTriggered = true;
            }
        }
        else
        {
            if (_parameterTrackers.TryGetValue(trackerKey, out var tracker))
            {
                _parameterTrackers.Remove(trackerKey);
                _logger.LogDebug("参数 {Param} 超限已恢复，设备: {DeviceId}", threshold.ParameterName, data.DeviceId);
            }
        }
    }

    private decimal? GetParameterValue(DeviceData data, string parameterName)
    {
        return parameterName switch
        {
            "Power" => data.Power,
            "SupplyTemperature" => data.SupplyTemperature,
            "ReturnTemperature" => data.ReturnTemperature,
            "Pressure" => data.Pressure,
            "FlowRate" => data.FlowRate,
            "Frequency" => data.Frequency,
            "Current" => data.Current,
            "Voltage" => data.Voltage,
            "InletTemperature" => data.InletTemperature,
            "OutletTemperature" => data.OutletTemperature,
            "FanSpeed" => data.FanSpeed,
            _ => null
        };
    }

    private async Task TriggerParameterAlarmAsync(DeviceData data, AlarmThreshold threshold,
        decimal currentValue, ParameterExceedanceTracker tracker, DateTime timestamp)
    {
        var device = await _deviceRepository.GetByIdAsync(data.DeviceId);
        var parameterDisplayName = GetParameterDisplayName(threshold.ParameterName);

        var alarm = new Alarm
        {
            DeviceId = data.DeviceId,
            Device = device,
            AlarmType = AlarmType.ParameterExceedance,
            AlarmLevel = AlarmLevel.Level1,
            ParameterName = threshold.ParameterName,
            ParameterValue = currentValue,
            ThresholdValue = currentValue > threshold.MaxValue ? threshold.MaxValue : threshold.MinValue,
            Message = $"{device?.Name ?? data.DeviceId} {parameterDisplayName} 超限，当前值: {currentValue:F2}，阈值: {(currentValue > threshold.MaxValue ? threshold.MaxValue : threshold.MinValue):F2}",
            StartTime = tracker.ExceedanceStartTime,
            DurationMinutes = (int)(timestamp - tracker.ExceedanceStartTime).TotalMinutes,
            Status = AlarmStatus.Active
        };

        await _alarmRepository.CreateAlarmAsync(alarm);
        await CreateWorkOrderForAlarmAsync(alarm);
        await _notificationService.PushWeChatAlarmAsync(alarm);

        await _mediator.Publish(new AlarmTriggeredEvent
        {
            Alarm = alarm,
            TriggeredAt = timestamp
        });

        _logger.LogWarning("一级告警触发: {Message}", alarm.Message);
    }

    private string GetParameterDisplayName(string parameterName)
    {
        return parameterName switch
        {
            "Power" => "功率",
            "SupplyTemperature" => "供水温度",
            "ReturnTemperature" => "回水温度",
            "Pressure" => "压力",
            "FlowRate" => "流量",
            "Frequency" => "频率",
            "Current" => "电流",
            "Voltage" => "电压",
            "InletTemperature" => "进水温度",
            "OutletTemperature" => "出水温度",
            "FanSpeed" => "风机转速",
            _ => parameterName
        };
    }

    private async Task MonitorLowEfficiencyAlarmAsync(DateTime timestamp)
    {
        var latestEfficiency = await _efficiencyRepository.GetLatestEfficiencyAsync();
        if (latestEfficiency == null) return;

        var designCOP = 5.0m;
        var lowEfficiencyThreshold = designCOP * _systemSettings.Value.CriticalEfficiencyThreshold;

        if (latestEfficiency.SystemCOP < lowEfficiencyThreshold)
        {
            if (!_lowEfficiencyStart.HasValue)
            {
                _lowEfficiencyStart = timestamp;
                _logger.LogDebug("系统COP低于阈值，开始计时，当前COP: {COP:F2}, 阈值: {Threshold:F2}",
                    latestEfficiency.SystemCOP, lowEfficiencyThreshold);
            }

            var duration = timestamp - _lowEfficiencyStart.Value;
            var triggerDuration = TimeSpan.FromMinutes(_alarmSettings.Value.Level2AlarmDurationMinutes);

            if (!_lowEfficiencyAlarmTriggered && duration >= triggerDuration)
            {
                await TriggerLowEfficiencyAlarmAsync(latestEfficiency, _lowEfficiencyStart.Value, timestamp);
                _lowEfficiencyAlarmTriggered = true;
            }
        }
        else
        {
            if (_lowEfficiencyStart.HasValue)
            {
                _logger.LogInformation("系统COP已恢复，当前COP: {COP:F2}", latestEfficiency.SystemCOP);
                _lowEfficiencyStart = null;
                _lowEfficiencyAlarmTriggered = false;
            }
        }
    }

    private async Task TriggerLowEfficiencyAlarmAsync(EfficiencyRecord efficiency, DateTime startTime, DateTime timestamp)
    {
        var alarm = new Alarm
        {
            AlarmType = AlarmType.LowEfficiency,
            AlarmLevel = AlarmLevel.Level2,
            ParameterName = "SystemCOP",
            ParameterValue = efficiency.SystemCOP,
            ThresholdValue = 5.0m * _systemSettings.Value.CriticalEfficiencyThreshold,
            Message = $"系统COP持续偏低，当前COP: {efficiency.SystemCOP:F2}，已持续 {(int)(timestamp - startTime).TotalMinutes} 分钟",
            StartTime = startTime,
            DurationMinutes = (int)(timestamp - startTime).TotalMinutes,
            Status = AlarmStatus.Active
        };

        await _alarmRepository.CreateAlarmAsync(alarm);
        await CreateWorkOrderForAlarmAsync(alarm);
        await _notificationService.PushWeChatAlarmAsync(alarm);

        await _mediator.Publish(new AlarmTriggeredEvent
        {
            Alarm = alarm,
            TriggeredAt = timestamp
        });

        _logger.LogWarning("二级告警触发: {Message}", alarm.Message);
    }

    private async Task CheckAndResolveAlarmsAsync(DateTime timestamp, List<Alarm> activeAlarms)
    {
        foreach (var alarm in activeAlarms)
        {
            if (alarm.Status != AlarmStatus.Active) continue;

            var isResolved = await CheckIfAlarmResolvedAsync(alarm, timestamp);
            if (isResolved)
            {
                alarm.EndTime = timestamp;
                alarm.DurationMinutes = (int)(timestamp - alarm.StartTime).TotalMinutes;
                alarm.Status = AlarmStatus.Resolved;
                await _alarmRepository.UpdateAlarmAsync(alarm);

                _logger.LogInformation("告警 {Id} 已自动解除", alarm.Id);
            }
        }
    }

    private async Task<bool> CheckIfAlarmResolvedAsync(Alarm alarm, DateTime timestamp)
    {
        if (alarm.AlarmType == AlarmType.ParameterExceedance && !string.IsNullOrEmpty(alarm.DeviceId))
        {
            var twoMinutesAgo = timestamp.AddMinutes(-2);
            var recentData = await _deviceRepository.GetDeviceTrendDataAsync(alarm.DeviceId, twoMinutesAgo, timestamp);
            var dataList = recentData.ToList();

            if (!dataList.Any()) return false;

            var parameterValues = dataList
                .Select(d => GetParameterValue(d, alarm.ParameterName))
                .Where(v => v.HasValue)
                .Cast<decimal>()
                .ToList();

            if (!parameterValues.Any()) return false;

            var minValue = parameterValues.Min();
            var maxValue = parameterValues.Max();

            if (alarm.ParameterValue > alarm.ThresholdValue)
            {
                return maxValue < alarm.ThresholdValue;
            }
            else
            {
                return minValue > alarm.ThresholdValue;
            }
        }
        else if (alarm.AlarmType == AlarmType.LowEfficiency)
        {
            var latestEfficiency = await _efficiencyRepository.GetLatestEfficiencyAsync();
            var threshold = 5.0m * _systemSettings.Value.CriticalEfficiencyThreshold;
            return latestEfficiency?.SystemCOP > threshold;
        }

        return false;
    }

    private async Task CreateWorkOrderForAlarmAsync(Alarm alarm)
    {
        var random = new Random();
        var workOrderNo = $"WO{DateTime.UtcNow:yyyyMMddHHmmssfff}{random.Next(100, 999)}";

        var workOrder = new WorkOrder
        {
            WorkOrderNo = workOrderNo,
            Title = alarm.AlarmLevel == AlarmLevel.Level1 ? "一级告警处理" : "二级告警处理",
            Description = $"{alarm.Message}\n告警时间: {alarm.StartTime:yyyy-MM-dd HH:mm:ss}\n告警设备: {alarm.Device?.Name ?? "系统整体"}\n参数值: {alarm.ParameterValue:F2}\n阈值: {alarm.ThresholdValue:F2}",
            Priority = alarm.AlarmLevel == AlarmLevel.Level1 ? "High" : "Critical",
            Status = WorkOrderStatus.Created,
            AlarmId = alarm.Id,
            CreatedAt = DateTime.UtcNow,
            Category = alarm.AlarmType.ToString()
        };

        await _workOrderRepository.CreateWorkOrderAsync(workOrder);
        _logger.LogInformation("已为告警 {AlarmId} 生成工单 {WorkOrderNo}", alarm.Id, workOrderNo);
    }
}

public class AlarmSettings
{
    public int Level1AlarmDurationMinutes { get; set; } = 10;
    public int Level2AlarmDurationMinutes { get; set; } = 30;
}

public class SystemSettings
{
    public decimal SystemDesignCOP { get; set; } = 5.0m;
    public decimal LowEfficiencyThreshold { get; set; } = 0.70m;
    public decimal CriticalEfficiencyThreshold { get; set; } = 0.60m;
    public int DataRetentionDays { get; set; } = 365;
    public bool EnableAutoOptimization { get; set; } = false;
}
