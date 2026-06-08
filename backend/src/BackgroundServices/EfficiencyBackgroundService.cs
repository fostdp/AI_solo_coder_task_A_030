using MediatR;
using Microsoft.AspNetCore.SignalR;
using ChillerPlantOptimization.Hubs;
using ChillerPlantOptimization.Services;
using ChillerPlantOptimization.DTOs;
using ChillerPlantOptimization.Contracts.Commands;
using ChillerPlantOptimization.Modules.EfficiencyOptimizer;
using ChillerPlantOptimization.Modules.AlarmManager;

namespace ChillerPlantOptimization.BackgroundServices;

public class EfficiencyBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<RealtimeHub, IRealtimeClient> _hubContext;
    private readonly ILogger<EfficiencyBackgroundService> _logger;
    private TimeSpan _efficiencyInterval = TimeSpan.FromMinutes(1);
    private TimeSpan _optimizationInterval = TimeSpan.FromHours(1);
    private TimeSpan _alarmCheckInterval = TimeSpan.FromMinutes(1);
    private DateTime _lastOptimizationRun;

    public EfficiencyBackgroundService(
        IServiceProvider serviceProvider,
        IHubContext<RealtimeHub, IRealtimeClient> hubContext,
        ILogger<EfficiencyBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
        _lastOptimizationRun = DateTime.UtcNow.Date;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("后台定时服务已启动（模块化架构）");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var efficiencyOptimizer = scope.ServiceProvider.GetRequiredService<IEfficiencyOptimizer>();
                var alarmManager = scope.ServiceProvider.GetRequiredService<IAlarmManager>();
                var alarmEngine = scope.ServiceProvider.GetRequiredService<IAlarmEngineService>();

                var copResult = await mediator.Send(new CalculateCOPCommand { Timestamp = now }, stoppingToken);
                if (copResult != null)
                {
                    _logger.LogInformation("系统COP计算完成: {COP}", copResult.SystemCOP);
                }

                await efficiencyOptimizer.UpdateDeviceEfficiencyStatusAsync();

                var activeAlarmCount = await mediator.Send(new CheckAlarmsCommand { Timestamp = now }, stoppingToken);
                _logger.LogDebug("告警检测完成，活动告警数: {Count}", activeAlarmCount);

                if ((now - _lastOptimizationRun) >= _optimizationInterval)
                {
                    _logger.LogInformation("开始执行能效优化推荐");
                    var recommendation = await mediator.Send(new GenerateRecommendationCommand(), stoppingToken);
                    if (recommendation != null)
                    {
                        _logger.LogInformation("优化推荐已生成，预测COP: {COP}，版本: {Version}",
                            recommendation.PredictedCOP, recommendation.ModelVersion);
                    }
                    _lastOptimizationRun = now;
                }

                await alarmEngine.ClearExpiredAlarmsAsync(now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "后台服务执行出错");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }

        _logger.LogInformation("后台定时服务已停止");
    }
}
