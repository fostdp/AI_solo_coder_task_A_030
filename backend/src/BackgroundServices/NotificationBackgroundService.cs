using ChillerPlantOptimization.Services;

namespace ChillerPlantOptimization.BackgroundServices;

public class NotificationBackgroundService : BackgroundService
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationBackgroundService> _logger;

    public NotificationBackgroundService(
        INotificationService notificationService,
        ILogger<NotificationBackgroundService> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("通知聚合后台服务已启动");

        try
        {
            await _notificationService.StartAggregationLoopAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("通知聚合后台服务已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知聚合后台服务发生致命错误");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("通知聚合后台服务正在停止...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("通知聚合后台服务已完全停止");
    }
}
