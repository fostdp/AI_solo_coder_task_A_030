using ChillerPlantOptimization.Services;
using ChillerPlantOptimization.Modules.BacnetGateway;

namespace ChillerPlantOptimization.BackgroundServices;

public class BACnetCollectionBackgroundService : BackgroundService
{
    private readonly IBacnetGateway _bacnetGateway;
    private readonly ILogger<BACnetCollectionBackgroundService> _logger;

    public BACnetCollectionBackgroundService(
        IBacnetGateway bacnetGateway,
        ILogger<BACnetCollectionBackgroundService> logger)
    {
        _bacnetGateway = bacnetGateway;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BACnet数据采集后台服务已启动（模块化架构）");

        try
        {
            await _bacnetGateway.StartCollectionAsync(stoppingToken);
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
