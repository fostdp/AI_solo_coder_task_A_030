using ChillerPlantOptimization.Models;
using ChillerPlantOptimization.Repositories;

namespace ChillerPlantOptimization.Services;

public interface ISystemConfigService
{
    Task<IEnumerable<SystemConfig>> GetAllConfigsAsync();
    Task<SystemConfig?> GetConfigAsync(string key);
    Task<bool> UpdateConfigAsync(string key, string value);
    Task<decimal> GetSystemDesignCOPAsync();
    Task<object> GetDashboardSummaryAsync();
    Task<object> GetDeviceCountByTypeAsync();
}

public class SystemConfigService : ISystemConfigService
{
    private readonly ISystemConfigRepository _configRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IEfficiencyRepository _efficiencyRepository;
    private readonly IAlarmRepository _alarmRepository;
    private readonly ILogger<SystemConfigService> _logger;

    public SystemConfigService(
        ISystemConfigRepository configRepository,
        IDeviceRepository deviceRepository,
        IEfficiencyRepository efficiencyRepository,
        IAlarmRepository alarmRepository,
        ILogger<SystemConfigService> logger)
    {
        _configRepository = configRepository;
        _deviceRepository = deviceRepository;
        _efficiencyRepository = efficiencyRepository;
        _alarmRepository = alarmRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<SystemConfig>> GetAllConfigsAsync()
    {
        return await _configRepository.GetAllAsync();
    }

    public async Task<SystemConfig?> GetConfigAsync(string key)
    {
        return await _configRepository.GetByKeyAsync(key);
    }

    public async Task<bool> UpdateConfigAsync(string key, string value)
    {
        var config = await _configRepository.GetByKeyAsync(key);
        if (config == null) return false;

        config.Value = value;
        config.UpdatedAt = DateTime.UtcNow;
        await _configRepository.UpdateAsync(config);
        return true;
    }

    public async Task<decimal> GetSystemDesignCOPAsync()
    {
        var config = await _configRepository.GetByKeyAsync("SystemDesignCOP");
        if (config != null && decimal.TryParse(config.Value, out var cop))
        {
            return cop;
        }
        return 5.0m;
    }

    public async Task<object> GetDashboardSummaryAsync()
    {
        var devices = (await _deviceRepository.GetAllAsync()).ToList();
        var todayStart = DateTime.Today.ToUniversalTime();
        var todayRecords = (await _efficiencyRepository.GetEfficiencyRecordsAsync(todayStart, DateTime.UtcNow)).ToList();
        var activeAlarms = (await _alarmRepository.GetActiveAlarmsAsync()).ToList();

        var latestRecord = todayRecords.OrderByDescending(r => r.Timestamp).FirstOrDefault();

        return new
        {
            TotalDevices = devices.Count,
            RunningDevices = devices.Count(d => d.Status == DeviceStatus.Running),
            StandbyDevices = devices.Count(d => d.Status == DeviceStatus.Standby),
            FaultDevices = devices.Count(d => d.Status == DeviceStatus.Fault),
            ActiveAlarms = activeAlarms.Count(a => a.AlarmLevel == AlarmLevel.Level1),
            CriticalAlarms = activeAlarms.Count(a => a.AlarmLevel == AlarmLevel.Level2),
            TodayEnergy = latestRecord?.DailyEnergyConsumption ?? 0,
            CurrentCOP = latestRecord?.SystemCOP ?? 0,
            DesignCOPRatio = latestRecord?.DesignCOPRatio ?? 0,
            TodaySaving = latestRecord?.EnergySaving ?? 0,
            PeakPower = todayRecords.Max(r => (decimal?)r.TotalPower) ?? 0
        };
    }

    public async Task<object> GetDeviceCountByTypeAsync()
    {
        var devices = (await _deviceRepository.GetAllAsync()).ToList();
        return new
        {
            CentrifugalChiller = devices.Count(d => d.DeviceTypeId == DeviceType.CentrifugalChiller),
            ScrewChiller = devices.Count(d => d.DeviceTypeId == DeviceType.ScrewChiller),
            CoolingTower = devices.Count(d => d.DeviceTypeId == DeviceType.CoolingTower),
            ChilledWaterPump = devices.Count(d => d.DeviceTypeId == DeviceType.ChilledWaterPump),
            CoolingWaterPump = devices.Count(d => d.DeviceTypeId == DeviceType.CoolingWaterPump),
            Total = devices.Count
        };
    }
}
