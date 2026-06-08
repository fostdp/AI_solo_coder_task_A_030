using Microsoft.AspNetCore.SignalR;
using ChillerPlantOptimization.Hubs;
using ChillerPlantOptimization.Services;
using ChillerPlantOptimization.DTOs;

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
        _logger.LogInformation("后台定时服务已启动");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                using var scope = _serviceProvider.CreateScope();
                var efficiencyService = scope.ServiceProvider.GetRequiredService<IEfficiencyService>();
                var alarmService = scope.ServiceProvider.GetRequiredService<IAlarmEngineService>();
                var optimizationService = scope.ServiceProvider.GetRequiredService<IOptimizationModelService>();

                var efficiencyResult = await efficiencyService.CalculateAndSaveSystemCOPAsync(now);
                if (efficiencyResult != null)
                {
                    var metrics = new
                    {
                        Timestamp = efficiencyResult.Timestamp,
                        DailyEnergy = efficiencyResult.DailyEnergyConsumption,
                        RealtimeCOP = efficiencyResult.SystemCOP,
                        EnergySaving = efficiencyResult.EnergySaving,
                        LoadRate = efficiencyResult.LoadRate
                    };

                    await _hubContext.Clients.Group("Metrics").ReceiveMetricsUpdate(metrics);
                    await _hubContext.Clients.Group("Devices").ReceiveEfficiencyUpdate(efficiencyResult);

                    _logger.LogInformation("系统COP计算完成: {COP}", efficiencyResult.SystemCOP);
                }

                await alarmService.CheckAlarmsAsync(now);

                var activeAlarms = await alarmService.GetActiveAlarmsAsync();
                foreach (var alarm in activeAlarms.Where(a => a.Status == Models.AlarmStatus.Active))
                {
                    var alarmDto = new AlarmDto
                    {
                        Id = alarm.Id,
                        DeviceId = alarm.DeviceId,
                        AlarmLevel = (int)alarm.AlarmLevel,
                        AlarmType = (int)alarm.AlarmType,
                        Message = alarm.Message,
                        StartTime = alarm.StartTime,
                        DurationMinutes = alarm.DurationMinutes,
                        ParameterName = alarm.ParameterName,
                        ParameterValue = alarm.ParameterValue,
                        ThresholdValue = alarm.ThresholdValue
                    };
                    await _hubContext.Clients.Group("Alarms").ReceiveAlarmUpdate(alarmDto);
                }

                if ((now - _lastOptimizationRun) >= _optimizationInterval)
                {
                    _logger.LogInformation("开始执行能效优化推荐");
                    var recommendation = await optimizationService.GenerateOptimizationRecommendationAsync();
                    if (recommendation != null)
                    {
                        _logger.LogInformation("优化推荐已生成，预测COP: {COP}", recommendation.PredictedCOP);
                    }
                    _lastOptimizationRun = now;
                }

                await alarmService.ClearExpiredAlarmsAsync(now);
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
