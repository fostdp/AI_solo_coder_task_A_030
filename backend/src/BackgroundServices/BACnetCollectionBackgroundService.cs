using ChillerPlantOptimization.Services;

namespace ChillerPlantOptimization.BackgroundServices;

public class BACnetCollectionBackgroundService : BackgroundService
{
    private readonly IBACnetDataCollectionService _bacnetService;
    private readonly ILogger<BACnetCollectionBackgroundService> _logger;

    public BACnetCollectionBackgroundService(
        IBACnetDataCollectionService bacnetService,
        ILogger<BACnetCollectionBackgroundService> logger)
    {
        _bacnetService = bacnetService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BACnet数据采集后台服务已启动");

        try
        {
            await _bacnetService.StartCollectionAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("BACnet数据采集已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BACnet数据采集服务异常");
        }
        finally
        {
            _logger.LogInformation("BACnet数据采集后台服务已停止");
        }
    }
}
