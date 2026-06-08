using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.FastTree;
using Microsoft.ML.Trainers;
using ChillerPlantOptimization.Models;
using ChillerPlantOptimization.Repositories;

namespace ChillerPlantOptimization.Services;

public interface IOptimizationModelService
{
    Task TrainModelAsync();
    Task<OptimizationRecommendation> GenerateOptimizationRecommendationAsync();
    Task<OptimizationRecommendation?> GetLatestRecommendationAsync();
    Task<IEnumerable<OptimizationRecommendation>> GetRecommendationHistoryAsync(int count = 24);
    Task ApplyRecommendationAsync(long id, string appliedBy);
    Task RejectRecommendationAsync(long id, string rejectedBy);
}

public class EfficiencyPrediction
{
    [ColumnName("Score")]
    public float PredictedCOP { get; set; }
}

public class EfficiencyInput
{
    [LoadColumn(0)]
    public float ChilledWaterSupplyTemp { get; set; }

    [LoadColumn(1)]
    public float CoolingWaterInletTemp { get; set; }

    [LoadColumn(2)]
    public float LoadRate { get; set; }

    [LoadColumn(3)]
    public float CentrifugalCount { get; set; }

    [LoadColumn(4)]
    public float ScrewCount { get; set; }

    [LoadColumn(5)]
    public float PumpCount { get; set; }

    [LoadColumn(6)]
    public float TowerCount { get; set; }

    [LoadColumn(7)]
    public float COP { get; set; }
}

public class DeviceCombination
{
    public List<string> RunningChillers { get; set; } = new();
    public List<string> RunningPumps { get; set; } = new();
    public List<string> RunningTowers { get; set; } = new();
    public decimal PredictedCOP { get; set; }
    public decimal PredictedPower { get; set; }
    public decimal ChilledWaterSetpoint { get; set; }
}

public class OptimizationModelService : IOptimizationModelService
{
    private readonly MLContext _mlContext = new MLContext(seed: 1);
    private ITransformer? _trainedModel;
    private PredictionEngine<EfficiencyInput, EfficiencyPrediction>? _predictionEngine;
    private readonly ITimeSeriesRepository _timeSeriesRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IEfficiencyRepository _efficiencyRepository;
    private readonly IOptimizationRepository _optimizationRepository;
    private readonly ISystemConfigRepository _configRepository;
    private readonly ILogger<OptimizationModelService> _logger;

    public OptimizationModelService(
        ITimeSeriesRepository timeSeriesRepository,
        IDeviceRepository deviceRepository,
        IEfficiencyRepository efficiencyRepository,
        IOptimizationRepository optimizationRepository,
        ISystemConfigRepository configRepository,
        ILogger<OptimizationModelService> logger)
    {
        _timeSeriesRepository = timeSeriesRepository;
        _deviceRepository = deviceRepository;
        _efficiencyRepository = efficiencyRepository;
        _optimizationRepository = optimizationRepository;
        _configRepository = configRepository;
        _logger = logger;
    }

    public async Task TrainModelAsync()
    {
        _logger.LogInformation("开始训练能效优化模型");

        try
        {
            var trainingData = await PrepareTrainingDataAsync();
            if (!trainingData.Any())
            {
                _logger.LogWarning("无足够训练数据，使用内置模型参数");
                await LoadDefaultModel();
                return;
            }

            var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);
            var pipeline = BuildTrainingPipeline();
            var model = pipeline.Fit(dataView);
            _trainedModel = model;
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<EfficiencyInput, EfficiencyPrediction>(model);

            _logger.LogInformation("能效优化模型训练完成，共使用 {Count} 条训练数据", trainingData.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "训练模型失败，使用默认模型");
            await LoadDefaultModel();
        }
    }

    public async Task<OptimizationRecommendation> GenerateOptimizationRecommendationAsync()
    {
        _logger.LogInformation("生成优化推荐方案");

        if (_trainedModel == null || _predictionEngine == null)
        {
            await TrainModelAsync();
        }

        var currentConditions = await GetCurrentOperatingConditionsAsync();
        var possibleCombinations = GeneratePossibleCombinations(currentConditions);

        var bestCombination = possibleCombinations
            .OrderByDescending(c => c.PredictedCOP)
            .First();

        var currentEfficiency = await _efficiencyRepository.GetLatestEfficiencyAsync();
        var currentCOP = currentEfficiency?.SystemCOP ?? 0;

        var expectedSaving = currentCOP > 0
            ? (bestCombination.PredictedCOP - currentCOP) / currentCOP * currentEfficiency?.DailyEnergyConsumption ?? 0
            : 0;

        var expectedSavingPercent = currentCOP > 0
            ? (bestCombination.PredictedCOP - currentCOP) / currentCOP * 100
            : 0;

        var recommendation = new OptimizationRecommendation
        {
            GeneratedAt = DateTime.UtcNow,
            DeviceCombination = FormatCombination(bestCombination),
            RunningChillers = string.Join(",", bestCombination.RunningChillers),
            RunningPumps = string.Join(",", bestCombination.RunningPumps),
            RunningTowers = string.Join(",", bestCombination.RunningTowers),
            PredictedCOP = bestCombination.PredictedCOP,
            PredictedPower = bestCombination.PredictedPower,
            ChilledWaterSetpoint = bestCombination.ChilledWaterSetpoint,
            ExpectedEnergySaving = Math.Max(expectedSaving, 0),
            ExpectedSavingPercent = Math.Max(expectedSavingPercent, 0),
            LoadRate = currentConditions.LoadRate,
            AmbientTemp = currentConditions.AmbientTemp,
            Status = RecommendationStatus.New
        };

        await _optimizationRepository.AddRecommendationAsync(recommendation);
        _logger.LogInformation("优化推荐方案生成完成，预测COP: {PredictedCOP:F2}", recommendation.PredictedCOP);

        return recommendation;
    }

    public async Task<OptimizationRecommendation?> GetLatestRecommendationAsync()
    {
        return await _optimizationRepository.GetLatestRecommendationAsync();
    }

    public async Task<IEnumerable<OptimizationRecommendation>> GetRecommendationHistoryAsync(int count = 24)
    {
        return await _optimizationRepository.GetRecommendationHistoryAsync(count);
    }

    public async Task ApplyRecommendationAsync(long id, string appliedBy)
    {
        await _optimizationRepository.ApplyRecommendationAsync(id, appliedBy);
        _logger.LogInformation("优化方案 {Id} 已被 {User} 应用", id, appliedBy);
    }

    public async Task RejectRecommendationAsync(long id, string rejectedBy)
    {
        await _optimizationRepository.RejectRecommendationAsync(id, rejectedBy);
        _logger.LogInformation("优化方案 {Id} 已被 {User} 拒绝", id, rejectedBy);
    }

    private async Task<IEnumerable<EfficiencyInput>> PrepareTrainingDataAsync()
    {
        var historyData = await _efficiencyRepository.GetEfficiencyTrendAsync(
            DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        var trainingData = new List<EfficiencyInput>();
        var devices = await _deviceRepository.GetAllAsync();
        var deviceList = devices.ToList();

        foreach (var record in historyData)
        {
            var runningChillers = deviceList
                .Where(d => d.DeviceTypeId == DeviceType.CentrifugalChiller || d.DeviceTypeId == DeviceType.ScrewChiller)
                .Where(d => d.Status == DeviceStatus.Running)
                .ToList();

            var centrifugalCount = runningChillers.Count(d => d.DeviceTypeId == DeviceType.CentrifugalChiller);
            var screwCount = runningChillers.Count(d => d.DeviceTypeId == DeviceType.ScrewChiller);

            var runningPumps = deviceList
                .Count(d => (d.DeviceTypeId == DeviceType.ChilledWaterPump || d.DeviceTypeId == DeviceType.CoolingWaterPump)
                           && d.Status == DeviceStatus.Running);

            var runningTowers = deviceList
                .Count(d => d.DeviceTypeId == DeviceType.CoolingTower && d.Status == DeviceStatus.Running);

            if (record.SystemCOP > 0)
            {
                trainingData.Add(new EfficiencyInput
                {
                    ChilledWaterSupplyTemp = (float)record.ChilledWaterSupplyTemp,
                    CoolingWaterInletTemp = (float)record.CoolingWaterReturnTemp,
                    LoadRate = (float)record.LoadRate,
                    CentrifugalCount = centrifugalCount,
                    ScrewCount = screwCount,
                    PumpCount = runningPumps,
                    TowerCount = runningTowers,
                    COP = (float)record.SystemCOP
                });
            }
        }

        return trainingData;
    }

    private EstimatorChain<RegressionPredictionTransformer<FastTreeRegressionModelParameters>> BuildTrainingPipeline()
    {
        var pipeline = _mlContext.Transforms.Concatenate("Features",
                nameof(EfficiencyInput.ChilledWaterSupplyTemp),
                nameof(EfficiencyInput.CoolingWaterInletTemp),
                nameof(EfficiencyInput.LoadRate),
                nameof(EfficiencyInput.CentrifugalCount),
                nameof(EfficiencyInput.ScrewCount),
                nameof(EfficiencyInput.PumpCount),
                nameof(EfficiencyInput.TowerCount))
            .Append(_mlContext.Regression.Trainers.FastTree(
                labelColumnName: nameof(EfficiencyInput.COP),
                featureColumnName: "Features",
                numberOfLeaves: 20,
                numberOfTrees: 100,
                minimumExampleCountPerLeaf: 5));

        return pipeline;
    }

    private async Task LoadDefaultModel()
    {
        var defaultInput = new EfficiencyInput
        {
            ChilledWaterSupplyTemp = 7.0f,
            CoolingWaterInletTemp = 30.0f,
            LoadRate = 0.7f,
            CentrifugalCount = 2,
            ScrewCount = 1,
            PumpCount = 4,
            TowerCount = 4
        };

        _predictionEngine = _mlContext.Model.CreatePredictionEngine<EfficiencyInput, EfficiencyPrediction>(
            BuildTrainingPipeline().Fit(_mlContext.Data.LoadFromEnumerable(new[] { defaultInput })));
    }

    private async Task<dynamic> GetCurrentOperatingConditionsAsync()
    {
        var latestEfficiency = await _efficiencyRepository.GetLatestEfficiencyAsync();
        var chilledSupplyTemp = latestEfficiency?.ChilledWaterSupplyTemp ?? 7.0m;
        var coolingInletTemp = latestEfficiency?.CoolingWaterReturnTemp ?? 30.0m;
        var loadRate = latestEfficiency?.LoadRate ?? 0.7m;

        return new
        {
            ChilledWaterSupplyTemp = chilledSupplyTemp,
            CoolingWaterInletTemp = coolingInletTemp,
            LoadRate = loadRate,
            AmbientTemp = coolingInletTemp - 2.0m
        };
    }

    private IEnumerable<DeviceCombination> GeneratePossibleCombinations(dynamic currentConditions)
    {
        var devices = _deviceRepository.GetAllAsync().Result;
        var deviceList = devices.ToList();

        var allChillers = deviceList
            .Where(d => d.DeviceTypeId == DeviceType.CentrifugalChiller || d.DeviceTypeId == DeviceType.ScrewChiller)
            .Select(d => d.Id)
            .ToList();

        var allPumps = deviceList
            .Where(d => d.DeviceTypeId == DeviceType.ChilledWaterPump || d.DeviceTypeId == DeviceType.CoolingWaterPump)
            .Select(d => d.Id)
            .ToList();

        var allTowers = deviceList
            .Where(d => d.DeviceTypeId == DeviceType.CoolingTower)
            .Select(d => d.Id)
            .ToList();

        var combinations = new List<DeviceCombination>();
        var loadRate = (decimal)currentConditions.LoadRate;
        var centrifugalCapacity = 6200m;
        var screwCapacity = 3000m;
        var totalCapacity = 3 * centrifugalCapacity + 2 * screwCapacity;
        var requiredCapacity = totalCapacity * loadRate;

        var chillerCombos = GetChillerCombinations(allChillers, requiredCapacity, centrifugalCapacity, screwCapacity);

        foreach (var chillerCombo in chillerCombos)
        {
            var centrifugalCount = chillerCombo.Count(c => c.StartsWith("CH-C-"));
            var screwCount = chillerCombo.Count(c => c.StartsWith("CH-S-"));

            var pumpCount = Math.Max(centrifugalCount + screwCount, 2) * 2;
            var pumpCombo = allPumps.Take(Math.Min(pumpCount, allPumps.Count)).ToList();

            var towerCount = Math.Max(centrifugalCount + screwCount, 2);
            var towerCombo = allTowers.Take(Math.Min(towerCount, allTowers.Count)).ToList();

            for (var setpoint = 6.0m; setpoint <= 10.0m; setpoint += 0.5m)
            {
                var input = new EfficiencyInput
                {
                    ChilledWaterSupplyTemp = (float)setpoint,
                    CoolingWaterInletTemp = (float)currentConditions.CoolingWaterInletTemp,
                    LoadRate = (float)loadRate,
                    CentrifugalCount = centrifugalCount,
                    ScrewCount = screwCount,
                    PumpCount = pumpCombo.Count,
                    TowerCount = towerCombo.Count
                };

                var prediction = _predictionEngine?.Predict(input) ?? new EfficiencyPrediction { PredictedCOP = 5.0f };

                var combo = new DeviceCombination
                {
                    RunningChillers = chillerCombo,
                    RunningPumps = pumpCombo,
                    RunningTowers = towerCombo,
                    PredictedCOP = (decimal)prediction.PredictedCOP,
                    PredictedPower = CalculatePredictedPower(chillerCombo, pumpCombo, towerCombo, deviceList),
                    ChilledWaterSetpoint = setpoint
                };

                combinations.Add(combo);
            }
        }

        return combinations;
    }

    private List<List<string>> GetChillerCombinations(List<string> allChillers, decimal requiredCapacity,
        decimal centrifugalCapacity, decimal screwCapacity)
    {
        var result = new List<List<string>>();
        var centrifugals = allChillers.Where(c => c.StartsWith("CH-C-")).ToList();
        var screws = allChillers.Where(c => c.StartsWith("CH-S-")).ToList();

        for (int c = 0; c <= centrifugals.Count; c++)
        {
            for (int s = 0; s <= screws.Count; s++)
            {
                if (c == 0 && s == 0) continue;

                var capacity = c * centrifugalCapacity + s * screwCapacity;
                if (capacity >= requiredCapacity * 0.9m && capacity <= requiredCapacity * 1.3m)
                {
                    var combo = new List<string>();
                    combo.AddRange(centrifugals.Take(c));
                    combo.AddRange(screws.Take(s));
                    result.Add(combo);
                }
            }
        }

        if (!result.Any())
        {
            result.Add(centrifugals.Take(2).Concat(screws.Take(1)).ToList());
        }

        return result;
    }

    private decimal CalculatePredictedPower(List<string> chillers, List<string> pumps, List<string> towers, List<Device> allDevices)
    {
        var chillerPower = chillers.Sum(c => allDevices.FirstOrDefault(d => d.Id == c)?.RatedPower ?? 1000);
        var pumpPower = pumps.Sum(p => allDevices.FirstOrDefault(d => d.Id == p)?.RatedPower ?? 90);
        var towerPower = towers.Sum(t => allDevices.FirstOrDefault(d => d.Id == t)?.RatedPower ?? 22);
        return chillerPower + pumpPower + towerPower;
    }

    private string FormatCombination(DeviceCombination combo)
    {
        return $"冷水机组: {string.Join(",", combo.RunningChillers)}; 水泵: {string.Join(",", combo.RunningPumps)}; 冷却塔: {string.Join(",", combo.RunningTowers)}; 冷冻水设定温度: {combo.ChilledWaterSetpoint:F1}°C";
    }
}
