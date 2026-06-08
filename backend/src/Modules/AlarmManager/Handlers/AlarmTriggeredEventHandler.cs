using MediatR;
using ChillerPlantOptimization.Contracts.Events;
using ChillerPlantOptimization.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ChillerPlantOptimization.Modules.AlarmManager.Handlers;

public class AlarmTriggeredEventHandler : INotificationHandler<AlarmTriggeredEvent>
{
    private readonly IHubContext<RealtimeHub> _hubContext;
    private readonly ILogger<AlarmTriggeredEventHandler> _logger;

    public AlarmTriggeredEventHandler(IHubContext<RealtimeHub> hubContext, ILogger<AlarmTriggeredEventHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Handle(AlarmTriggeredEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var alarmDto = new DTOs.AlarmDto
            {
                Id = notification.Alarm.Id,
                DeviceId = notification.Alarm.DeviceId,
                DeviceName = notification.Alarm.Device?.Name,
                AlarmType = notification.Alarm.AlarmType.ToString(),
                AlarmLevel = notification.Alarm.AlarmLevel.ToString(),
                ParameterName = notification.Alarm.ParameterName,
                ParameterValue = notification.Alarm.ParameterValue,
                ThresholdValue = notification.Alarm.ThresholdValue,
                Message = notification.Alarm.Message,
                StartTime = notification.Alarm.StartTime,
                DurationMinutes = notification.Alarm.DurationMinutes,
                Status = notification.Alarm.Status.ToString()
            };

            await _hubContext.Clients.Group("Alarms").SendAsync("ReceiveAlarmUpdate", alarmDto, cancellationToken);
            _logger.LogDebug("告警事件已推送到前端: {AlarmId}", notification.Alarm.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送告警事件失败");
        }
    }
}
