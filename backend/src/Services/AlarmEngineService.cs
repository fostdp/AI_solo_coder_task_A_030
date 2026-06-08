using ChillerPlantOptimization.Models;
using ChillerPlantOptimization.Repositories;

namespace ChillerPlantOptimization.Services;

public interface IAlarmEngineService
{
    Task MonitorAndProcessAlarmsAsync();
    Task<IEnumerable<Alarm>> GetActiveAlarmsAsync();
    Task<IEnumerable<Alarm>> GetAlarmsAsync(DateTime startTime, DateTime endTime, int? level = null);
    Task<Alarm?> GetAlarmByIdAsync(long id);
    Task AcknowledgeAlarmAsync(long id, string acknowledgedBy);
    Task ResolveAlarmAsync(long id, string resolvedBy);
    Task<IEnumerable<WorkOrder>> GetWorkOrdersAsync(WorkOrderStatus? status = null);
    Task<WorkOrder?> GetWorkOrderByIdAsync(long id);
    Task<WorkOrder> CreateWorkOrderFromAlarmAsync(long alarmId, string title, string description);
    Task ProcessWorkOrderAsync(long id, string processor, string resolution);
}

public class ParameterExceedanceTracker
{
    public string DeviceId { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public DateTime FirstExceedTime { get; set; }
    public decimal ExceedValue { get; set; }
    public decimal Threshold { get; set; }
    public bool IsUpperLimit { get; set; }
    public bool AlarmTriggered { get; set; }
}

public class AlarmEngineService : IAlarmEngineService
{
    private readonly IAlarmRepository _alarmRepository;
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly ITimeSeriesRepository _timeSeriesRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ISystemConfigRepository _configRepository;
    private readonly INotificationService _notificationService;
    private readonly IEfficiencyService _efficiencyService;
    private readonly ILogger<AlarmEngineService> _logger;

    private readonly List<ParameterExceedanceTracker> _parameterTrackers = new();
    private DateTime _lowEfficiencyStart = DateTime.MinValue;
    private bool _lowEfficiencyAlarmTriggered = false;

    public AlarmEngineService(
        IAlarmRepository alarmRepository,
        IWorkOrderRepository workOrderRepository,
        ITimeSeriesRepository timeSeriesRepository,
        IDeviceRepository deviceRepository,
        ISystemConfigRepository configRepository,
        INotificationService notificationService,
        IEfficiencyService efficiencyService,
        ILogger<AlarmEngineService> logger)
    {
        _alarmRepository = alarmRepository;
        _workOrderRepository = workOrderRepository;
        _timeSeriesRepository = timeSeriesRepository;
        _deviceRepository = deviceRepository;
        _configRepository = configRepository;
        _notificationService = notificationService;
        _efficiencyService = efficiencyService;
        _logger = logger;
    }

    public async Task MonitorAndProcessAlarmsAsync()
    {
        _logger.LogInformation("开始监控告警条件");

        await CheckParameterExceedanceAsync();
        await CheckLowEfficiencyAsync();
        await UpdateActiveAlarmDurationsAsync();
    }

    private async Task CheckParameterExceedanceAsync()
    {
        var thresholds = await _configRepository.GetAlarmThresholdsAsync();
        var devices = await _deviceRepository.GetAllAsync();
        var deviceList = devices.ToList();

        foreach (var threshold in thresholds)
        {
            var relevantDevices = threshold.DeviceTypeId.HasValue
                ? deviceList.Where(d => d.DeviceTypeId == threshold.DeviceTypeId.Value).ToList()
                : deviceList;

            foreach (var device in relevantDevices)
            {
                if (device.Status != DeviceStatus.Running) continue;

                var latestData = await _timeSeriesRepository.GetLatestDataAsync(device.Id);
                if (latestData == null) continue;

                var paramValue = GetParameterValue(latestData, threshold.ParameterName);
                if (!paramValue.HasValue) continue;

                var isExceeded = threshold.UpperLimit.HasValue && paramValue > threshold.UpperLimit ||
                                threshold.LowerLimit.HasValue && paramValue < threshold.LowerLimit;

                var tracker = _parameterTrackers.FirstOrDefault(
                    t => t.DeviceId == device.Id && t.ParameterName == threshold.ParameterName);

                if (isExceeded)
                {
                    if (tracker == null)
                    {
                        tracker = new ParameterExceedanceTracker
                        {
                            DeviceId = device.Id,
                            ParameterName = threshold.ParameterName,
                            FirstExceedTime = DateTime.UtcNow,
                            ExceedValue = paramValue.Value,
                            Threshold = threshold.UpperLimit ?? threshold.LowerLimit ?? 0,
                            IsUpperLimit = threshold.UpperLimit.HasValue
                        };
                        _parameterTrackers.Add(tracker);
                    }
                    else
                    {
                        tracker.ExceedValue = paramValue.Value;
                    }

                    var duration = DateTime.UtcNow - tracker.FirstExceedTime;
                    if (duration.TotalMinutes >= threshold.DurationMinutes && !tracker.AlarmTriggered)
                    {
                        await TriggerParameterAlarmAsync(device, threshold, tracker);
                        tracker.AlarmTriggered = true;
                    }
                }
                else if (tracker != null)
                {
                    if (tracker.AlarmTriggered)
                    {
                        await ClearAlarmAsync(device.Id, threshold.ParameterName);
                    }
                    _parameterTrackers.Remove(tracker);
                }
            }
        }
    }

    private async Task CheckLowEfficiencyAsync()
    {
        var latestEfficiency = await _efficiencyService.GetLatestEfficiencyAsync();
        if (latestEfficiency == null) return;

        var criticalThreshold = await GetCriticalLowEfficiencyThreshold();

        if (latestEfficiency.DesignCOPRatio < criticalThreshold)
        {
            if (_lowEfficiencyStart == DateTime.MinValue)
            {
                _lowEfficiencyStart = DateTime.UtcNow;
                _logger.LogWarning("系统能效低于阈值 {Threshold}%，开始计时", criticalThreshold * 100);
            }

            var duration = DateTime.UtcNow - _lowEfficiencyStart;
            if (duration.TotalMinutes >= 30 && !_lowEfficiencyAlarmTriggered)
            {
                await TriggerLowEfficiencyAlarmAsync(latestEfficiency);
                _lowEfficiencyAlarmTriggered = true;
            }
        }
        else
        {
            if (_lowEfficiencyAlarmTriggered)
            {
                await ClearLowEfficiencyAlarmAsync();
            }
            _lowEfficiencyStart = DateTime.MinValue;
            _lowEfficiencyAlarmTriggered = false;
        }
    }

    private async Task TriggerParameterAlarmAsync(Device device, AlarmThreshold threshold, ParameterExceedanceTracker tracker)
    {
        var message = tracker.IsUpperLimit
            ? $"{device.Name} 的 {threshold.ParameterName} 参数过高: {tracker.ExceedValue:F2}，阈值: {tracker.Threshold:F2}，已持续超过 {threshold.DurationMinutes} 分钟"
            : $"{device.Name} 的 {threshold.ParameterName} 参数过低: {tracker.ExceedValue:F2}，阈值: {tracker.Threshold:F2}，已持续超过 {threshold.DurationMinutes} 分钟";

        var alarm = new Alarm
        {
            DeviceId = device.Id,
            AlarmLevel = threshold.AlarmLevel,
            AlarmType = AlarmType.ParameterExceedance,
            ParameterName = threshold.ParameterName,
            ParameterValue = tracker.ExceedValue,
            ThresholdValue = tracker.Threshold,
            Message = message,
            StartTime = tracker.FirstExceedTime,
            Status = AlarmStatus.Active
        };

        await _alarmRepository.AddAlarmAsync(alarm);
        _logger.LogWarning("触发一级告警: {Message}", message);

        await _notificationService.PushWeChatAlarmAsync(alarm);
        await CreateWorkOrderFromAlarmAsync(alarm.Id, $"{device.Name} 参数超限告警", message);
    }

    private async Task TriggerLowEfficiencyAlarmAsync(EfficiencyRecord efficiency)
    {
        var designCOP = await GetDesignCOP();
        var message = $"系统能效严重偏低，实时COP: {efficiency.SystemCOP:F2}，设计COP: {designCOP:F2}，仅为设计值的 {efficiency.DesignCOPRatio * 100:F1}%，已持续超过30分钟";

        var alarm = new Alarm
        {
            DeviceId = null,
            AlarmLevel = AlarmLevel.Level2,
            AlarmType = AlarmType.LowEfficiency,
            ParameterName = "SystemCOP",
            ParameterValue = efficiency.SystemCOP,
            ThresholdValue = designCOP * 0.6m,
            Message = message,
            StartTime = _lowEfficiencyStart,
            Status = AlarmStatus.Active
        };

        await _alarmRepository.AddAlarmAsync(alarm);
        _logger.LogCritical("触发二级告警: {Message}", message);

        await _notificationService.PushWeChatAlarmAsync(alarm);
        await CreateWorkOrderFromAlarmAsync(alarm.Id, "系统能效严重偏低告警", message);
    }

    private async Task ClearAlarmAsync(string deviceId, string parameterName)
    {
        var activeAlarm = (await _alarmRepository.GetActiveAlarmsAsync())
            .FirstOrDefault(a => a.DeviceId == deviceId &&
                                 a.ParameterName == parameterName &&
                                 a.Status == AlarmStatus.Active);

        if (activeAlarm != null)
        {
            activeAlarm.Status = AlarmStatus.Cleared;
            activeAlarm.EndTime = DateTime.UtcNow;
            activeAlarm.DurationMinutes = (int)Math.Round((activeAlarm.EndTime.Value - activeAlarm.StartTime).TotalMinutes);
            await _alarmRepository.UpdateAlarmAsync(activeAlarm);
            _logger.LogInformation("告警已解除: {DeviceId} - {ParameterName}", deviceId, parameterName);
        }
    }

    private async Task ClearLowEfficiencyAlarmAsync()
    {
        var activeAlarm = (await _alarmRepository.GetActiveAlarmsAsync())
            .FirstOrDefault(a => a.AlarmType == AlarmType.LowEfficiency &&
                                 a.Status == AlarmStatus.Active);

        if (activeAlarm != null)
        {
            activeAlarm.Status = AlarmStatus.Cleared;
            activeAlarm.EndTime = DateTime.UtcNow;
            activeAlarm.DurationMinutes = (int)Math.Round((activeAlarm.EndTime.Value - activeAlarm.StartTime).TotalMinutes);
            await _alarmRepository.UpdateAlarmAsync(activeAlarm);
            _logger.LogInformation("低能效告警已解除");
        }
    }

    private async Task UpdateActiveAlarmDurationsAsync()
    {
        var activeAlarms = (await _alarmRepository.GetActiveAlarmsAsync()).ToList();
        foreach (var alarm in activeAlarms)
        {
            if (alarm.Status == AlarmStatus.Active || alarm.Status == AlarmStatus.Acknowledged)
            {
                alarm.DurationMinutes = (int)Math.Round((DateTime.UtcNow - alarm.StartTime).TotalMinutes);
            }
        }
    }

    public async Task<IEnumerable<Alarm>> GetActiveAlarmsAsync()
    {
        return await _alarmRepository.GetActiveAlarmsAsync();
    }

    public async Task<IEnumerable<Alarm>> GetAlarmsAsync(DateTime startTime, DateTime endTime, int? level = null)
    {
        return await _alarmRepository.GetAlarmsAsync(startTime, endTime, level);
    }

    public async Task<Alarm?> GetAlarmByIdAsync(long id)
    {
        return await _alarmRepository.GetAlarmByIdAsync(id);
    }

    public async Task AcknowledgeAlarmAsync(long id, string acknowledgedBy)
    {
        await _alarmRepository.AcknowledgeAlarmAsync(id, acknowledgedBy);
    }

    public async Task ResolveAlarmAsync(long id, string resolvedBy)
    {
        await _alarmRepository.ResolveAlarmAsync(id, resolvedBy);
    }

    public async Task<IEnumerable<WorkOrder>> GetWorkOrdersAsync(WorkOrderStatus? status = null)
    {
        return await _workOrderRepository.GetWorkOrdersAsync(status);
    }

    public async Task<WorkOrder?> GetWorkOrderByIdAsync(long id)
    {
        return await _workOrderRepository.GetWorkOrderByIdAsync(id);
    }

    public async Task<WorkOrder> CreateWorkOrderFromAlarmAsync(long alarmId, string title, string description)
    {
        var alarm = await _alarmRepository.GetAlarmByIdAsync(alarmId);
        if (alarm == null)
        {
            throw new ArgumentException($"告警 {alarmId} 不存在");
        }

        var workOrder = new WorkOrder
        {
            AlarmId = alarmId,
            Title = title,
            Description = description,
            Priority = alarm.AlarmLevel == AlarmLevel.Level2 ? 0 : 1,
            Status = WorkOrderStatus.Pending
        };

        var createdOrder = await _workOrderRepository.CreateWorkOrderAsync(workOrder);
        _logger.LogInformation("自动生成工单: {WorkOrderNo}", createdOrder.WorkOrderNo);

        return createdOrder;
    }

    public async Task ProcessWorkOrderAsync(long id, string processor, string resolution)
    {
        await _workOrderRepository.ProcessWorkOrderAsync(id, processor, resolution);
        _logger.LogInformation("工单 {Id} 已由 {Processor} 处理完成", id, processor);
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

    private async Task<decimal> GetCriticalLowEfficiencyThreshold()
    {
        var value = await _configRepository.GetSettingAsync("CriticalLowEfficiencyThreshold");
        return value != null ? decimal.Parse(value) : 0.6m;
    }

    private async Task<decimal> GetDesignCOP()
    {
        var value = await _configRepository.GetSettingAsync("SystemDesignCOP");
        return value != null ? decimal.Parse(value) : 5.5m;
    }
}
