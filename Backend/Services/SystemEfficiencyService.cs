using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ChillerPlant.Data;
using ChillerPlant.Data.Repositories;
using ChillerPlant.Models;

namespace ChillerPlant.Services
{
    public class SystemEfficiencyService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SystemEfficiencyService> _logger;
        private readonly AppSettings _appSettings;
        private int _executionCount = 0;

        public SystemEfficiencyService(IServiceProvider serviceProvider,
            ILogger<SystemEfficiencyService> logger,
            IOptions<AppSettings> appSettings)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _appSettings = appSettings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("System Efficiency Service started.");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _executionCount++;
                    
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var efficiencyRepository = scope.ServiceProvider.GetRequiredService<IEfficiencyRepository>();
                        var alarmRepository = scope.ServiceProvider.GetRequiredService<IAlarmRepository>();
                        var optimizationRepository = scope.ServiceProvider.GetRequiredService<IOptimizationRepository>();
                        var hubContext = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<RealtimeHub>>();

                        await CalculateSystemEfficiencyAsync(context);

                        if (_executionCount % 2 == 0)
                        {
                            await alarmRepository.CheckAndCreateAlarmsAsync();
                        }

                        if (_executionCount % (60 / _appSettings.DataReportIntervalSeconds * _appSettings.OptimizationIntervalMinutes) == 0)
                        {
                            await optimizationRepository.GenerateOptimizationAsync();
                        }

                        if (_executionCount % 120 == 0)
                        {
                            await efficiencyRepository.UpdateHourlyEnergyConsumptionAsync();
                        }

                        var now = DateTime.Now;
                        if (now.Hour == 1 && now.Minute < 5 && _executionCount % 10 == 0)
                        {
                            await efficiencyRepository.UpdateDailyEnergyConsumptionAsync();
                            await efficiencyRepository.GenerateEnergyDiagnosisReportAsync(DateTime.Today.AddDays(-1));
                        }

                        if (_executionCount % 600 == 0)
                        {
                            await optimizationRepository.TrainModelAsync();
                        }

                        var dashboard = await efficiencyRepository.GetRealtimeDashboardAsync();
                        await hubContext.Clients.All.SendAsync("DashboardUpdated", dashboard);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while executing system efficiency service.");
                }

                await Task.Delay(TimeSpan.FromSeconds(_appSettings.DataReportIntervalSeconds), stoppingToken);
            }

            _logger.LogInformation("System Efficiency Service stopped.");
        }

        private async Task CalculateSystemEfficiencyAsync(ApplicationDbContext context)
        {
            var now = DateTime.Now;
            var startTime = now.AddSeconds(-_appSettings.DataReportIntervalSeconds * 2);

            var recentData = await context.DeviceData
                .Where(d => d.Timestamp >= startTime)
                .Include(d => d.Device)
                .ToListAsync();

            if (!recentData.Any()) return;

            var latestByDevice = recentData
                .GroupBy(d => d.DeviceId)
                .Select(g => g.OrderByDescending(d => d.Timestamp).First())
                .ToList();

            var chillerData = latestByDevice.Where(d => d.Device.DeviceTypeId == 1 || d.Device.DeviceTypeId == 2).ToList();
            var pumpData = latestByDevice.Where(d => d.Device.DeviceTypeId == 4 || d.Device.DeviceTypeId == 5).ToList();
            var towerData = latestByDevice.Where(d => d.Device.DeviceTypeId == 3).ToList();

            var totalCooling = 0m;
            foreach (var chiller in chillerData.Where(c => c.Status == 1))
            {
                if (chiller.FlowRate.HasValue && chiller.ReturnWaterTemp.HasValue && chiller.SupplyWaterTemp.HasValue)
                {
                    var deltaT = Math.Abs(chiller.ReturnWaterTemp.Value - chiller.SupplyWaterTemp.Value);
                    totalCooling += chiller.FlowRate.Value * deltaT * 1.163m;
                }
                else if (chiller.LoadRate.HasValue)
                {
                    totalCooling += chiller.Device.RatedCapacity * chiller.LoadRate.Value / 100;
                }
            }

            var totalPower = latestByDevice.Where(d => d.Status == 1).Sum(d => d.Power);
            var chillerPower = chillerData.Where(d => d.Status == 1).Sum(d => d.Power);
            var pumpPower = pumpData.Where(d => d.Status == 1).Sum(d => d.Power);
            var towerPower = towerData.Where(d => d.Status == 1).Sum(d => d.Power);

            var systemCOP = totalPower > 0 ? totalCooling / totalPower : 0;
            var totalFlow = pumpData.Where(d => d.Status == 1).Sum(d => d.FlowRate ?? 0);

            var random = new Random();
            var outdoorTemp = 25 + random.NextDouble() * 10;
            var wetBulbTemp = 22 + random.NextDouble() * 5;

            var efficiency = new SystemEfficiency
            {
                Timestamp = now,
                TotalCoolingCapacity = Math.Round(totalCooling, 2),
                TotalPowerConsumption = Math.Round(totalPower, 2),
                SystemCOP = Math.Round(systemCOP, 2),
                DesignCOP = _appSettings.DesignSystemCOP,
                COPRatio = Math.Round(systemCOP / _appSettings.DesignSystemCOP, 4),
                ChillerPower = Math.Round(chillerPower, 2),
                PumpPower = Math.Round(pumpPower, 2),
                TowerPower = Math.Round(towerPower, 2),
                OutdoorTemp = Math.Round((decimal)outdoorTemp, 1),
                WetBulbTemp = Math.Round((decimal)wetBulbTemp, 1),
                TotalFlowRate = Math.Round(totalFlow, 2),
                CreatedAt = now
            };

            context.SystemEfficiencies.Add(efficiency);
            await context.SaveChangesAsync();
        }
    }
}
