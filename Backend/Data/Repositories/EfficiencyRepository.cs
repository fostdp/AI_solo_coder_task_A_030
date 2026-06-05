using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Dapper;
using ChillerPlant.Models;

namespace ChillerPlant.Data.Repositories
{
    public class EfficiencyRepository : IEfficiencyRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;
        private readonly decimal _designSystemCOP;

        public EfficiencyRepository(ApplicationDbContext context, string connectionString, decimal designSystemCOP)
        {
            _context = context;
            _connectionString = connectionString;
            _designSystemCOP = designSystemCOP;
        }

        public async Task<SystemEfficiency> InsertSystemEfficiencyAsync(SystemEfficiency efficiency)
        {
            efficiency.DesignCOP = _designSystemCOP;
            efficiency.COPRatio = efficiency.SystemCOP / _designSystemCOP;
            efficiency.CreatedAt = DateTime.Now;
            
            _context.SystemEfficiencies.Add(efficiency);
            await _context.SaveChangesAsync();
            return efficiency;
        }

        public async Task<SystemEfficiency> GetLatestSystemEfficiencyAsync()
        {
            return await _context.SystemEfficiencies
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefaultAsync();
        }

        public async Task<RealtimeDashboardDto> GetRealtimeDashboardAsync()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            
            var dailyEnergy = await _context.EnergyConsumptions
                .Where(e => e.Date == today && e.Hour == null && e.DeviceId == null)
                .FirstOrDefaultAsync();

            var latestEfficiency = await GetLatestSystemEfficiencyAsync();
            var deviceStatus = await new DeviceRepository(_context, _connectionString).GetDeviceStatusListAsync();
            var activeAlarms = await new AlarmRepository(_context, _connectionString).GetActiveAlarmsAsync();

            var totalEnergyToday = await _context.DeviceData
                .Where(d => d.Timestamp >= today && d.Timestamp < tomorrow)
                .SumAsync(d => (decimal?)d.Power) ?? 0;
            totalEnergyToday = totalEnergyToday / 120;

            var baselineEnergy = totalEnergyToday * _designSystemCOP / 
                (latestEfficiency?.SystemCOP ?? _designSystemCOP);
            var energySaving = Math.Max(0, baselineEnergy - totalEnergyToday);
            var energySavingPercent = baselineEnergy > 0 ? energySaving / baselineEnergy * 100 : 0;

            return new RealtimeDashboardDto
            {
                DailyTotalEnergy = Math.Round(totalEnergyToday, 2),
                RealtimeCOP = Math.Round(latestEfficiency?.SystemCOP ?? 0, 2),
                DesignCOP = _designSystemCOP,
                COPRatio = Math.Round((latestEfficiency?.COPRatio ?? 0) * 100, 1),
                TotalEnergySaving = Math.Round(energySaving, 2),
                EnergySavingPercent = Math.Round(energySavingPercent, 1),
                TotalCoolingCapacity = Math.Round(latestEfficiency?.TotalCoolingCapacity ?? 0, 2),
                TotalPowerConsumption = Math.Round(latestEfficiency?.TotalPowerConsumption ?? 0, 2),
                UpdateTime = latestEfficiency?.Timestamp ?? DateTime.Now,
                DeviceStatusList = deviceStatus,
                ActiveAlarms = activeAlarms
            };
        }

        public async Task<EnergyStatisticsDto> GetDailyEnergyStatisticsAsync(DateTime? date = null)
        {
            var targetDate = date ?? DateTime.Today;
            var nextDate = targetDate.AddDays(1);

            var data = await _context.EnergyConsumptions
                .Where(e => e.Date == targetDate && e.Hour == null && e.DeviceId.HasValue)
                .Include(e => e.Device)
                .ToListAsync();

            var avgCOP = await _context.SystemEfficiencies
                .Where(s => s.Timestamp >= targetDate && s.Timestamp < nextDate)
                .AverageAsync(s => (decimal?)s.SystemCOP);

            var peakPower = await _context.SystemEfficiencies
                .Where(s => s.Timestamp >= targetDate && s.Timestamp < nextDate)
                .MaxAsync(s => (decimal?)s.TotalPowerConsumption);

            return new EnergyStatisticsDto
            {
                Date = targetDate,
                TotalEnergy = Math.Round(data.Sum(e => e.EnergyConsumed), 2),
                ChillerEnergy = Math.Round(data.Where(e => e.Device.DeviceTypeId == 1 || e.Device.DeviceTypeId == 2)
                    .Sum(e => e.EnergyConsumed), 2),
                ChillerPumpEnergy = Math.Round(data.Where(e => e.Device.DeviceTypeId == 4)
                    .Sum(e => e.EnergyConsumed), 2),
                CoolingPumpEnergy = Math.Round(data.Where(e => e.Device.DeviceTypeId == 5)
                    .Sum(e => e.EnergyConsumed), 2),
                TowerEnergy = Math.Round(data.Where(e => e.Device.DeviceTypeId == 3)
                    .Sum(e => e.EnergyConsumed), 2),
                AvgCOP = Math.Round(avgCOP ?? 0, 2),
                PeakPower = Math.Round(peakPower ?? 0, 2)
            };
        }

        public async Task<List<EnergyStatisticsDto>> GetEnergyStatisticsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            var result = new List<EnergyStatisticsDto>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var stat = await GetDailyEnergyStatisticsAsync(date);
                result.Add(stat);
            }
            return result;
        }

        public async Task InsertEnergyConsumptionAsync(EnergyConsumption consumption)
        {
            _context.EnergyConsumptions.Add(consumption);
            await _context.SaveChangesAsync();
        }

        public async Task<EnergyDiagnosisReport> GenerateEnergyDiagnosisReportAsync(DateTime? reportDate = null)
        {
            var targetDate = reportDate ?? DateTime.Today.AddDays(-1);
            var nextDate = targetDate.AddDays(1);

            var avgCOP = await _context.SystemEfficiencies
                .Where(s => s.Timestamp >= targetDate && s.Timestamp < nextDate)
                .AverageAsync(s => (decimal?)s.SystemCOP) ?? 0;

            var totalEnergy = await _context.DeviceData
                .Where(d => d.Timestamp >= targetDate && d.Timestamp < nextDate)
                .SumAsync(d => (decimal?)d.Power) ?? 0;
            totalEnergy = totalEnergy / 120;

            var copRatio = avgCOP / _designSystemCOP * 100;
            var benchmarkEnergy = avgCOP > 0 ? totalEnergy * _designSystemCOP / avgCOP : totalEnergy;
            var savingPotential = Math.Max(0, totalEnergy - benchmarkEnergy);

            var lowEfficiencyDevices = await _context.DeviceData
                .Where(d => d.Timestamp >= targetDate && d.Timestamp < nextDate && d.COP.HasValue)
                .GroupBy(d => d.DeviceId)
                .Select(g => new { DeviceId = g.Key, AvgCOP = g.Average(d => d.COP) })
                .Join(_context.Devices, g => g.DeviceId, d => d.DeviceId, (g, d) => new 
                { 
                    d.DeviceName, 
                    g.AvgCOP, 
                    d.DesignCOP,
                    d.DeviceTypeId
                })
                .Where(x => x.AvgCOP < x.DesignCOP * 0.7m && (x.DeviceTypeId == 1 || x.DeviceTypeId == 2))
                .Select(x => x.DeviceName)
                .ToListAsync();

            var findings = $"系统平均COP: {avgCOP:F2}，设计COP: {_designSystemCOP}，COP比值: {copRatio:F1}%";
            if (copRatio < 70)
                findings += "；系统能效低于基准值70%，需重点关注。";
            else
                findings += "；系统运行状况良好。";
            
            if (lowEfficiencyDevices.Any())
                findings += $" 低效设备包括: {string.Join(", ", lowEfficiencyDevices)}";

            var recommendations = "1. 检查冷冻水/冷却水温度设定是否最优；2. 优化设备启停组合；3. 清理冷凝器管道；4. 检查水泵变频器运行状态；5. 定期清洗冷却塔填料";

            var report = new EnergyDiagnosisReport
            {
                ReportDate = targetDate,
                SystemAvgCOP = avgCOP,
                DesignCOP = _designSystemCOP,
                COPRatio = copRatio,
                TotalEnergyConsumption = totalEnergy,
                BenchmarkEnergyConsumption = benchmarkEnergy,
                EnergySavingPotential = savingPotential,
                DiagnosisFindings = findings,
                Recommendations = recommendations,
                LowEfficiencyDevices = lowEfficiencyDevices.Any() ? string.Join(", ", lowEfficiencyDevices) : null,
                GeneratedAt = DateTime.Now
            };

            _context.EnergyDiagnosisReports.Add(report);
            await _context.SaveChangesAsync();

            return report;
        }

        public async Task<List<EnergyDiagnosisReport>> GetRecentDiagnosisReportsAsync(int count = 10)
        {
            return await _context.EnergyDiagnosisReports
                .OrderByDescending(r => r.ReportDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task UpdateHourlyEnergyConsumptionAsync()
        {
            var now = DateTime.Now;
            var hourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            var hourEnd = hourStart.AddHours(1);
            var previousHourStart = hourStart.AddHours(-1);
            var previousHourEnd = hourStart;

            var deviceIds = await _context.Devices.Select(d => d.DeviceId).ToListAsync();

            foreach (var deviceId in deviceIds)
            {
                var data = await _context.DeviceData
                    .Where(d => d.DeviceId == deviceId && d.Timestamp >= previousHourStart && d.Timestamp < previousHourEnd)
                    .ToListAsync();

                if (!data.Any()) continue;

                var avgPower = data.Average(d => d.Power);
                var energy = avgPower / 2;
                var avgCOP = data.Where(d => d.COP.HasValue).Average(d => d.COP);
                var peakPower = data.Max(d => d.Power);
                var runtime = data.Count(d => d.Status == 1) / 2;

                var consumption = new EnergyConsumption
                {
                    DeviceId = deviceId,
                    Date = previousHourStart.Date,
                    Hour = previousHourStart.Hour,
                    EnergyConsumed = Math.Round(energy, 2),
                    AvgCOP = Math.Round(avgCOP ?? 0, 2),
                    PeakPower = Math.Round(peakPower, 2),
                    Runtime = runtime,
                    CreatedAt = now
                };

                var existing = await _context.EnergyConsumptions
                    .FirstOrDefaultAsync(e => e.DeviceId == deviceId && e.Date == consumption.Date && e.Hour == consumption.Hour);

                if (existing == null)
                {
                    _context.EnergyConsumptions.Add(consumption);
                }
                else
                {
                    existing.EnergyConsumed = consumption.EnergyConsumed;
                    existing.AvgCOP = consumption.AvgCOP;
                    existing.PeakPower = consumption.PeakPower;
                    existing.Runtime = consumption.Runtime;
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateDailyEnergyConsumptionAsync()
        {
            var yesterday = DateTime.Today.AddDays(-1);
            var today = DateTime.Today;
            var now = DateTime.Now;

            var deviceIds = await _context.Devices.Select(d => d.DeviceId).ToListAsync();

            foreach (var deviceId in deviceIds)
            {
                var hourlyData = await _context.EnergyConsumptions
                    .Where(e => e.DeviceId == deviceId && e.Date == yesterday && e.Hour.HasValue)
                    .ToListAsync();

                if (!hourlyData.Any()) continue;

                var dailyConsumption = new EnergyConsumption
                {
                    DeviceId = deviceId,
                    Date = yesterday,
                    Hour = null,
                    EnergyConsumed = Math.Round(hourlyData.Sum(e => e.EnergyConsumed), 2),
                    AvgCOP = Math.Round(hourlyData.Where(e => e.AvgCOP > 0).Average(e => e.AvgCOP) ?? 0, 2),
                    PeakPower = Math.Round(hourlyData.Max(e => e.PeakPower), 2),
                    Runtime = hourlyData.Sum(e => e.Runtime),
                    CreatedAt = now
                };

                var existing = await _context.EnergyConsumptions
                    .FirstOrDefaultAsync(e => e.DeviceId == deviceId && e.Date == yesterday && e.Hour == null);

                if (existing == null)
                {
                    _context.EnergyConsumptions.Add(dailyConsumption);
                }
                else
                {
                    existing.EnergyConsumed = dailyConsumption.EnergyConsumed;
                    existing.AvgCOP = dailyConsumption.AvgCOP;
                    existing.PeakPower = dailyConsumption.PeakPower;
                    existing.Runtime = dailyConsumption.Runtime;
                }
            }

            var totalDaily = new EnergyConsumption
            {
                DeviceId = null,
                Date = yesterday,
                Hour = null,
                EnergyConsumed = Math.Round(await _context.EnergyConsumptions
                    .Where(e => e.Date == yesterday && e.Hour == null && e.DeviceId.HasValue)
                    .SumAsync(e => e.EnergyConsumed), 2),
                CreatedAt = now
            };

            var existingTotal = await _context.EnergyConsumptions
                .FirstOrDefaultAsync(e => e.DeviceId == null && e.Date == yesterday && e.Hour == null);

            if (existingTotal == null)
            {
                _context.EnergyConsumptions.Add(totalDaily);
            }
            else
            {
                existingTotal.EnergyConsumed = totalDaily.EnergyConsumed;
            }

            await _context.SaveChangesAsync();
        }
    }
}
