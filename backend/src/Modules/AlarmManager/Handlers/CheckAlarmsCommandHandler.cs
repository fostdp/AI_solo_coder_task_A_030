using MediatR;
using ChillerPlantOptimization.Contracts.Commands;

namespace ChillerPlantOptimization.Modules.AlarmManager.Handlers;

public class CheckAlarmsCommandHandler : IRequestHandler<CheckAlarmsCommand, int>
{
    private readonly IAlarmManager _alarmManager;

    public CheckAlarmsCommandHandler(IAlarmManager alarmManager)
    {
        _alarmManager = alarmManager;
    }

    public async Task<int> Handle(CheckAlarmsCommand request, CancellationToken cancellationToken)
    {
        await _alarmManager.MonitorAndProcessAlarmsAsync(request.Timestamp);
        var activeAlarms = await _alarmManager.GetActiveAlarmsAsync();
        return activeAlarms.Count();
    }
}
