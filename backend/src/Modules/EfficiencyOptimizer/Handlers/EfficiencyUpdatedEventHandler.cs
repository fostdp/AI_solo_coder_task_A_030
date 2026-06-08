using MediatR;
using ChillerPlantOptimization.Contracts.Events;
using ChillerPlantOptimization.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ChillerPlantOptimization.Modules.EfficiencyOptimizer.Handlers;

public class EfficiencyUpdatedEventHandler : INotificationHandler<EfficiencyUpdatedEvent>
{
    private readonly IHubContext<RealtimeHub> _hubContext;
    private readonly ILogger<EfficiencyUpdatedEventHandler> _logger;

    public EfficiencyUpdatedEventHandler(IHubContext<RealtimeHub> hubContext, ILogger<EfficiencyUpdatedEventHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Handle(EfficiencyUpdatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var metrics = new DTOs.SystemMetricsDto
            {
                Timestamp = notification.EfficiencyRecord.Timestamp,
                RealTimeCOP = notification.EfficiencyRecord.SystemCOP,
                DesignCOP = notification.EfficiencyRecord.DesignCOP,
                EfficiencyRatio = notification.EfficiencyRecord.EfficiencyRatio,
                TotalPower = notification.EfficiencyRecord.TotalPower,
                TotalCoolingCapacity = notification.EfficiencyRecord.TotalCoolingCapacity,
                LoadRate = notification.EfficiencyRecord.LoadRate,
                DailyEnergyConsumption = notification.EfficiencyRecord.DailyEnergyConsumption,
                EnergySaving = notification.EfficiencyRecord.EnergySaving,
                RunningChillerCount = notification.EfficiencyRecord.RunningChillerCount,
                RunningPumpCount = notification.EfficiencyRecord.RunningPumpCount,
                RunningTowerCount = notification.EfficiencyRecord.RunningTowerCount
            };

            await _hubContext.Clients.Group("Metrics").SendAsync("ReceiveSystemMetrics", metrics, cancellationToken);
            _logger.LogDebug("能效更新事件已推送到前端，COP: {COP:F2}", notification.EfficiencyRecord.SystemCOP);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送能效更新事件失败");
        }
    }
}
