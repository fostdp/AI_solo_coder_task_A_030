using ChillerPlantOptimization.Models;

namespace ChillerPlantOptimization.Repositories;

public interface IDeviceRepository
{
    Task<IEnumerable<Device>> GetAllAsync();
    Task<Device?> GetByIdAsync(string id);
    Task<IEnumerable<Device>> GetByTypeAsync(DeviceType type);
    Task UpdateAsync(Device device);
    Task UpdateStatusAsync(string deviceId, DeviceStatus status);
    Task UpdateEfficiencyStatusAsync(string deviceId, EfficiencyStatus status, decimal? currentCOP);
}

public interface ITimeSeriesRepository
{
    Task AddDeviceDataAsync(DeviceData data);
    Task AddRangeDeviceDataAsync(IEnumerable<DeviceData> data);
    Task<DeviceData?> GetLatestDataAsync(string deviceId);
    Task<IEnumerable<DeviceData>> GetTrendDataAsync(string deviceId, DateTime startTime, DateTime endTime);
    Task<IEnumerable<DeviceData>> GetRecentDataAsync(TimeSpan timeSpan);
}

public interface IEfficiencyRepository
{
    Task AddEfficiencyRecordAsync(EfficiencyRecord record);
    Task<EfficiencyRecord?> GetLatestEfficiencyAsync();
    Task<IEnumerable<EfficiencyRecord>> GetEfficiencyTrendAsync(DateTime startTime, DateTime endTime);
    Task<SystemMetric?> GetLatestSystemMetricAsync();
    Task<IEnumerable<SystemMetric>> GetSystemMetricsTrendAsync(DateTime startTime, DateTime endTime);
    Task<DiagnosisReport?> GetLatestDiagnosisReportAsync();
    Task<DiagnosisReport> GenerateDiagnosisReportAsync(DateTime reportDate);
}

public interface IAlarmRepository
{
    Task<IEnumerable<Alarm>> GetActiveAlarmsAsync();
    Task<IEnumerable<Alarm>> GetAlarmsAsync(DateTime startTime, DateTime endTime, int? level = null);
    Task<Alarm?> GetAlarmByIdAsync(long id);
    Task AddAlarmAsync(Alarm alarm);
    Task UpdateAlarmAsync(Alarm alarm);
    Task AcknowledgeAlarmAsync(long id, string acknowledgedBy);
    Task ResolveAlarmAsync(long id, string resolvedBy);
    Task<IEnumerable<AlarmThreshold>> GetAllThresholdsAsync();
}

public interface IWorkOrderRepository
{
    Task<IEnumerable<WorkOrder>> GetWorkOrdersAsync(WorkOrderStatus? status = null);
    Task<WorkOrder?> GetWorkOrderByIdAsync(long id);
    Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder);
    Task UpdateWorkOrderAsync(WorkOrder workOrder);
    Task ProcessWorkOrderAsync(long id, string processor, string resolution);
}

public interface IOptimizationRepository
{
    Task<OptimizationRecommendation?> GetLatestRecommendationAsync();
    Task<IEnumerable<OptimizationRecommendation>> GetRecommendationHistoryAsync(int count = 24);
    Task AddRecommendationAsync(OptimizationRecommendation recommendation);
    Task ApplyRecommendationAsync(long id, string appliedBy);
    Task RejectRecommendationAsync(long id, string rejectedBy);
}

public interface ISystemConfigRepository
{
    Task<Dictionary<string, string>> GetAllSettingsAsync();
    Task<string?> GetSettingAsync(string key);
    Task UpdateSettingAsync(string key, string value);
    Task<IEnumerable<AlarmThreshold>> GetAlarmThresholdsAsync();
}
