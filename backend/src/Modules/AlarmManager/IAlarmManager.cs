using ChillerPlantOptimization.Models;

namespace ChillerPlantOptimization.Modules.AlarmManager;

public interface IAlarmManager
{
    Task MonitorAndProcessAlarmsAsync(DateTime timestamp);
    Task<IEnumerable<Alarm>> GetActiveAlarmsAsync();
    Task AcknowledgeAlarmAsync(long id, string acknowledgedBy);
    Task ResolveAlarmAsync(long id, string resolvedBy);
    Task PushAggregatedAlarmsAsync();
}
