using ChillerPlantOptimization.Models;
using ChillerPlantOptimization.Repositories;

namespace ChillerPlantOptimization.Services;

public interface IEfficiencyService
{
    Task<EfficiencyRecord> CalculateAndSaveSystemCOPAsync(DateTime timestamp);
    Task<EfficiencyRecord?> GetLatestEfficiencyAsync();
    Task<IEnumerable<EfficiencyRecord>> GetEfficiencyTrendAsync(DateTime startTime, DateTime endTime);
    Task<SystemMetric?> GetLatestSystemMetricAsync();
    Task<IEnumerable<SystemMetric>> GetSystemMetricsTrendAsync(DateTime startTime, DateTime endTime);
    Task<DiagnosisReport?> GetLatestDiagnosisReportAsync();
    Task<DiagnosisReport> GenerateDiagnosisReportAsync(DateTime reportDate);
    Task UpdateDeviceEfficiencyStatusAsync();
}

public class EfficiencyService : IEfficiencyService
{
    private readonly IEfficiencyRepository _efficiencyRepository;
    private readonly ITimeSeriesRepository _timeSeriesRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ISystemConfigRepository _configRepository;
    private readonly ILogger<EfficiencyService> _logger;

    public EfficiencyService(
        IEfficiencyRepository efficiencyRepository,
        ITimeSeriesRepository timeSeriesRepository,
        IDeviceRepository deviceRepository,
        ISystemConfigRepository configRepository,
        ILogger<EfficiencyService> logger)
    {
        _efficiencyRepository = efficiencyRepository;
        _timeSeriesRepository = timeSeriesRepository;
        _deviceRepository = deviceRepository;
        _configRepository = configRepository;
        _logger = logger;
    }

    public async Task<EfficiencyRecord> CalculateAndSaveSystemCOPAsync(DateTime timestamp)
    {
        try
        {
            var designCOP = await GetDesignCOP();
            var recentData = await _timeSeriesRepository.GetRecentDataAsync(TimeSpan.FromMinutes(1));
            var dataList = recentData.ToList();

            if (!dataList.Any())
            {
                _logger.LogWarning("计算COP时无可用数据");
                return CreateEmptyRecord(timestamp, designCOP);
            }

            var totalPower = dataList.Sum(d => d.Power);
            var chilledPumpsData = dataList.Where(d => d.DeviceId.StartsWith("CHWP-")).ToList();
            var coolingPumpsData = dataList.Where(d => d.DeviceId.StartsWith("CWP-")).ToList();

            var chilledSupplyTemp = chilledPumpsData.Any() ? chilledPumpsData.Average(d => d.SupplyTemperature) : 0;
            var chilledReturnTemp = chilledPumpsData.Any() ? chilledPumpsData.Average(d => d.ReturnTemperature) : 0;
            var coolingSupplyTemp = coolingPumpsData.Any() ? coolingPumpsData.Average(d => d.SupplyTemperature) : 0;
            var coolingReturnTemp = coolingPumpsData.Any() ? coolingPumpsData.Average(d => d.ReturnTemperature) : 0;
            var totalFlowRate = chilledPumpsData.Sum(d => d.FlowRate);

            decimal totalCoolingCapacity = 0;
            decimal systemCOP = 0;
            decimal loadRate = 0;

            if (totalFlowRate > 0 && chilledReturnTemp > chilledSupplyTemp)
            {
                totalCoolingCapacity = totalFlowRate * 4.186m * (chilledReturnTemp - chilledSupplyTemp) / 3600;
                systemCOP = totalPower > 0 ? totalCoolingCapacity / totalPower : 0;

                var runningChillers = await _deviceRepository.GetAllAsync();
                var totalRatedCooling = runningChillers
                    .Where(d => d.DeviceTypeId == DeviceType.CentrifugalChiller || d.DeviceTypeId == DeviceType.ScrewChiller)
                    .Where(d => d.Status == DeviceStatus.Running)
                    .Sum(d => d.RatedCoolingCapacity);

                loadRate = totalRatedCooling > 0 ? totalCoolingCapacity / totalRatedCooling : 0;
            }

            var dailyEnergy = await CalculateDailyEnergy(timestamp);
            var energySaving = dailyEnergy * 0.15m;

            var record = new EfficiencyRecord
            {
                Timestamp = timestamp,
                SystemCOP = systemCOP,
                DesignCOP = designCOP,
                DesignCOPRatio = designCOP > 0 ? systemCOP / designCOP : 0,
                TotalPower = totalPower,
                TotalCoolingCapacity = totalCoolingCapacity,
                ChilledWaterSupplyTemp = chilledSupplyTemp,
                ChilledWaterReturnTemp = chilledReturnTemp,
                CoolingWaterSupplyTemp = coolingSupplyTemp,
                CoolingWaterReturnTemp = coolingReturnTemp,
                LoadRate = loadRate,
                DailyEnergyConsumption = dailyEnergy,
                EnergySaving = energySaving
            };

            await _efficiencyRepository.AddEfficiencyRecordAsync(record);
            _logger.LogInformation("系统COP计算完成: {SystemCOP:F2}, 负荷率: {LoadRate:F2}%", systemCOP, loadRate * 100);

            return record;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算系统COP失败");
            throw;
        }
    }

    public async Task UpdateDeviceEfficiencyStatusAsync()
    {
        var designCOP = await GetDesignCOP();
        var devices = await _deviceRepository.GetAllAsync();

        foreach (var device in devices)
        {
            if (device.Status == DeviceStatus.Fault)
            {
                await _deviceRepository.UpdateEfficiencyStatusAsync(device.Id, EfficiencyStatus.Fault, null);
                continue;
            }

            if (device.Status != DeviceStatus.Running)
            {
                continue;
            }

            var latestData = await _timeSeriesRepository.GetLatestDataAsync(device.Id);
            if (latestData == null) continue;

            decimal? deviceCOP = null;

            if (device.DeviceTypeId == DeviceType.CentrifugalChiller || device.DeviceTypeId == DeviceType.ScrewChiller)
            {
                if (latestData.Power > 0 && latestData.ReturnTemperature > latestData.SupplyTemperature && latestData.FlowRate > 0)
                {
                    var coolingCapacity = latestData.FlowRate * 4.186m * (latestData.ReturnTemperature - latestData.SupplyTemperature) / 3600;
                    deviceCOP = coolingCapacity / latestData.Power;
                }
            }

            EfficiencyStatus status;
            if (deviceCOP.HasValue)
            {
                var ratio = deviceCOP.Value / designCOP;
                if (ratio >= 0.9m)
                    status = EfficiencyStatus.High;
                else if (ratio >= 0.7m)
                    status = EfficiencyStatus.Normal;
                else
                    status = EfficiencyStatus.Low;
            }
            else
            {
                status = EfficiencyStatus.High;
            }

            await _deviceRepository.UpdateEfficiencyStatusAsync(device.Id, status, deviceCOP);
        }
    }

    public async Task<EfficiencyRecord?> GetLatestEfficiencyAsync()
    {
        return await _efficiencyRepository.GetLatestEfficiencyAsync();
    }

    public async Task<IEnumerable<EfficiencyRecord>> GetEfficiencyTrendAsync(DateTime startTime, DateTime endTime)
    {
        return await _efficiencyRepository.GetEfficiencyTrendAsync(startTime, endTime);
    }

    public async Task<SystemMetric?> GetLatestSystemMetricAsync()
    {
        return await _efficiencyRepository.GetLatestSystemMetricAsync();
    }

    public async Task<IEnumerable<SystemMetric>> GetSystemMetricsTrendAsync(DateTime startTime, DateTime endTime)
    {
        return await _efficiencyRepository.GetSystemMetricsTrendAsync(startTime, endTime);
    }

    public async Task<DiagnosisReport?> GetLatestDiagnosisReportAsync()
    {
        return await _efficiencyRepository.GetLatestDiagnosisReportAsync();
    }

    public async Task<DiagnosisReport> GenerateDiagnosisReportAsync(DateTime reportDate)
    {
        _logger.LogInformation("生成节能诊断报告: {ReportDate}", reportDate.Date);
        return await _efficiencyRepository.GenerateDiagnosisReportAsync(reportDate);
    }

    private async Task<decimal> GetDesignCOP()
    {
        var value = await _configRepository.GetSettingAsync("SystemDesignCOP");
        return value != null ? decimal.Parse(value) : 5.5m;
    }

    private async Task<decimal> CalculateDailyEnergy(DateTime timestamp)
    {
        var todayStart = timestamp.Date;
        var todayData = await _timeSeriesRepository.GetRecentDataAsync(timestamp - todayStart);
        var dataList = todayData.ToList();

        if (!dataList.Any()) return 0;

        var groupedByDevice = dataList
            .GroupBy(d => d.DeviceId)
            .Select(g => new
            {
                DeviceId = g.Key,
                AvgPower = g.Average(d => d.Power),
                Hours = (timestamp - todayStart).TotalHours
            })
            .ToList();

        return groupedByDevice.Sum(d => d.AvgPower * (decimal)d.Hours);
    }

    private EfficiencyRecord CreateEmptyRecord(DateTime timestamp, decimal designCOP)
    {
        return new EfficiencyRecord
        {
            Timestamp = timestamp,
            SystemCOP = 0,
            DesignCOP = designCOP,
            DesignCOPRatio = 0,
            TotalPower = 0,
            TotalCoolingCapacity = 0,
            ChilledWaterSupplyTemp = 0,
            ChilledWaterReturnTemp = 0,
            CoolingWaterSupplyTemp = 0,
            CoolingWaterReturnTemp = 0,
            LoadRate = 0,
            DailyEnergyConsumption = 0,
            EnergySaving = 0
        };
    }
}
