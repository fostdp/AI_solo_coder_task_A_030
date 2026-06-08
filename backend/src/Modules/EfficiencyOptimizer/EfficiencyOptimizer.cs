using MediatR;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.FastTree;
using Microsoft.Extensions.Options;
using ChillerPlantOptimization.Contracts.Events;
using ChillerPlantOptimization.Models;
using ChillerPlantOptimization.Repositories;

namespace ChillerPlantOptimization.Modules.EfficiencyOptimizer;

public class MLModelSettings
{
    public string ModelPath { get; set; } = "models/optimization_model.zip";
    public string ModelVersion { get; set; } = "1.0.0";
    public bool EnableModelCaching { get; set; } = true;
    public int RetrainIntervalHours { get; set; } = 168;
    public int MinTrainingDataPoints { get; set; } = 100;
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

public class EfficiencyOptimizer : IEfficiencyOptimizer
{
    private readonly MLContext _mlContext = new MLContext(seed: 1);
    private ITransformer? _trainedModel;
    private PredictionEngine<EfficiencyInput, EfficiencyPrediction>? _predictionEngine;
    private readonly object _modelLock = new();
    private readonly SemaphoreSlim _predictionSemaphore = new(1, 1);

    private readonly ITimeSeriesRepository _timeSeriesRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IEfficiencyRepository _efficiencyRepository;
    private readonly IOptimizationRepository _optimizationRepository;
    private readonly ISystemConfigRepository _configRepository;
    private readonly IOptions<MLModelSettings> _modelSettings;
    private readonly IMediator _mediator;
    private readonly ILogger<EfficiencyOptimizer> _logger;

    public EfficiencyOptimizer(
        ITimeSeriesRepository timeSeriesRepository,
        IDeviceRepository deviceRepository,
        IEfficiencyRepository efficiencyRepository,
        IOptimizationRepository optimizationRepository,
        ISystemConfigRepository configRepository,
        IOptions<MLModelSettings> modelSettings,
        IMediator mediator,
        ILogger<EfficiencyOptimizer> logger)
    {
        _timeSeriesRepository = timeSeriesRepository;
        _deviceRepository = deviceRepository;
        _efficiencyRepository = efficiencyRepository;
        _optimizationRepository = optimizationRepository;
        _configRepository = configRepository;
        _modelSettings = modelSettings;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task TrainModelAsync()
    {
        _logger.LogInformation("开始训练能效优化模型，模型路径: {ModelPath}", _modelSettings.Value.ModelPath);

        try
        {
            var trainingData = await PrepareTrainingDataAsync();
            var trainingList = trainingData.ToList();

            if (trainingList.Count < _modelSettings.Value.MinTrainingDataPoints)
            {
                _logger.LogWarning("训练数据不足 ({Count}/{Min})，使用内置模型参数",
                    trainingList.Count, _modelSettings.Value.MinTrainingDataPoints);
                await LoadDefaultModel();
                return;
            }

            var dataView = _mlContext.Data.LoadFromEnumerable(trainingList);
            var pipeline = BuildTrainingPipeline();

            lock (_modelLock)
            {
                var model = pipeline.Fit(dataView);
                _trainedModel = model;
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<EfficiencyInput, EfficiencyPrediction>(model);

                if (_modelSettings.Value.EnableModelCaching)
                {
                    SaveModel(model);
                }
            }

            _logger.LogInformation("能效优化模型训练完成，版本: {Version}，训练数据: {Count} 条",
                _modelSettings.Value.ModelVersion, trainingList.Count);
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

        lock (_modelLock)
        {
            if (_trainedModel == null || _predictionEngine == null)
            {
                var waitTask = TrainModelAsync();
                waitTask.Wait();
            }
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
            Status = RecommendationStatus.New,
            ModelVersion = _modelSettings.Value.ModelVersion
        };

        await _optimizationRepository.AddRecommendationAsync(recommendation);

        await _mediator.Publish(new RecommendationGeneratedEvent
        {
            Recommendation = recommendation,
            GeneratedAt = DateTime.UtcNow
        });

        _logger.LogInformation("优化推荐方案生成完成，预测COP: {PredictedCOP:F2}，版本: {Version}",
            recommendation.PredictedCOP, _modelSettings.Value.ModelVersion);

        return recommendation;
    }

    public async Task<OptimizationRecommendation?> GetLatestRecommendationAsync()
    {
        return await _optimizationRepository.GetLatestRecommendationAsync();
    }

    public async Task<EfficiencyRecord> CalculateAndSaveSystemCOPAsync(DateTime timestamp)
    {
        _logger.LogDebug("计算系统COP，时间: {Time}", timestamp);

        var fiveMinutesAgo = timestamp.AddMinutes(-5);
        var deviceData = await _timeSeriesRepository.GetDeviceDataAsync(fiveMinutesAgo, timestamp);
        var deviceDataList = deviceData.ToList();

        var chillers = deviceDataList
            .Where(d => d.Device?.DeviceTypeId == DeviceType.CentrifugalChiller ||
                        d.Device?.DeviceTypeId == DeviceType.ScrewChiller)
            .GroupBy(d => d.DeviceId)
            .Select(g => g.OrderByDescending(d => d.Timestamp).First())
            .ToList();

        var chilledPumps = deviceDataList
            .Where(d => d.Device?.DeviceTypeId == DeviceType.ChilledWaterPump)
            .GroupBy(d => d.DeviceId)
            .Select(g => g.OrderByDescending(d => d.Timestamp).First())
            .ToList();

        var coolingPumps = deviceDataList
            .Where(d => d.Device?.DeviceTypeId == DeviceType.CoolingWaterPump)
            .GroupBy(d => d.DeviceId)
            .Select(g => g.OrderByDescending(d => d.Timestamp).First())
            .ToList();

        var towers = deviceDataList
            .Where(d => d.Device?.DeviceTypeId == DeviceType.CoolingTower)
            .GroupBy(d => d.DeviceId)
            .Select(g => g.OrderByDescending(d => d.Timestamp).First())
            .ToList();

        var totalChillerPower = chillers.Sum(d => d.Power);
        var totalPumpPower = chilledPumps.Sum(d => d.Power) + coolingPumps.Sum(d => d.Power);
        var totalTowerPower = towers.Sum(d => d.Power);
        var totalPower = totalChillerPower + totalPumpPower + totalTowerPower;

        var totalFlowRate = chilledPumps.Sum(d => d.FlowRate);
        var avgChilledSupplyTemp = chillers.Any() ? chillers.Average(d => d.SupplyTemperature) : 7.0m;
        var avgChilledReturnTemp = chilledPumps.Any() ? chilledPumps.Average(d => d.ReturnTemperature) : 14.0m;
        var avgCoolingReturnTemp = coolingPumps.Any() ? coolingPumps.Average(d => d.SupplyTemperature) : 32.0m;

        var specificHeat = 4.186m;
        var totalCoolingCapacity = totalFlowRate * specificHeat * (avgChilledReturnTemp - avgChilledSupplyTemp) / 3600;
        var systemCOP = totalPower > 0 ? totalCoolingCapacity / (totalPower / 1000) : 0;

        var allDevices = await _deviceRepository.GetAllAsync();
        var deviceList = allDevices.ToList();
        var designCOP = 5.0m;
        var efficiencyRatio = designCOP > 0 ? systemCOP / designCOP : 0;

        var totalCapacity = deviceList
            .Where(d => d.DeviceTypeId == DeviceType.CentrifugalChiller || d.DeviceTypeId == DeviceType.ScrewChiller)
            .Sum(d => d.RatedCapacity ?? 0);
        var currentCapacity = totalCoolingCapacity * 3.517m;
        var loadRate = totalCapacity > 0 ? currentCapacity / totalCapacity : 0;

        var baselineEnergy = totalPower / 1000 * (timestamp - fiveMinutesAgo).TotalHours;
        var optimizedEnergy = baselineEnergy * 0.15m;
        var energySaving = baselineEnergy - optimizedEnergy;

        var efficiencyRecord = new EfficiencyRecord
        {
            Timestamp = timestamp,
            TotalPower = totalPower,
            TotalCoolingCapacity = totalCoolingCapacity,
            SystemCOP = systemCOP,
            DesignCOP = designCOP,
            EfficiencyRatio = efficiencyRatio,
            ChilledWaterSupplyTemp = avgChilledSupplyTemp,
            ChilledWaterReturnTemp = avgChilledReturnTemp,
            CoolingWaterSupplyTemp = avgCoolingReturnTemp,
            CoolingWaterReturnTemp = avgCoolingReturnTemp + 3,
            FlowRate = totalFlowRate,
            LoadRate = loadRate,
            RunningChillerCount = chillers.Count,
            RunningPumpCount = chilledPumps.Count + coolingPumps.Count,
            RunningTowerCount = towers.Count,
            DailyEnergyConsumption = baselineEnergy,
            EnergySaving = energySaving
        };

        await _efficiencyRepository.AddEfficiencyRecordAsync(efficiencyRecord);

        var diagnosisThreshold = 0.7m;
        if (efficiencyRatio < diagnosisThreshold)
        {
            await GenerateDiagnosisReportAsync(efficiencyRecord, deviceList);
        }

        await _mediator.Publish(new EfficiencyUpdatedEvent
        {
            EfficiencyRecord = efficiencyRecord,
            CalculatedAt = timestamp
        });

        _logger.LogDebug("系统COP计算完成: {COP:F2}, 能效比: {Ratio:F2}%", systemCOP, efficiencyRatio * 100);

        return efficiencyRecord;
    }

    public async Task UpdateDeviceEfficiencyStatusAsync()
    {
        var devices = await _deviceRepository.GetAllAsync();
        var deviceList = devices.ToList();

        foreach (var device in deviceList)
        {
            if (device.Status == DeviceStatus.Fault || device.Status == DeviceStatus.Standby)
            {
                continue;
            }

            var latestData = await _timeSeriesRepository.GetLatestDataAsync(device.Id);
            if (latestData == null || device.RatedPower == null) continue;

            var deviceEfficiency = device.RatedPower > 0
                ? (decimal)(latestData.FlowRate * 4.186m * (latestData.ReturnTemperature - latestData.SupplyTemperature) / 3600 / (device.RatedPower / 1000))
                : 0;

            var deviceDesignCOP = device.DeviceTypeId switch
            {
                DeviceType.CentrifugalChiller => 5.8m,
                DeviceType.ScrewChiller => 5.2m,
                DeviceType.CoolingTower => 35.0m,
                DeviceType.ChilledWaterPump => 20.0m,
                DeviceType.CoolingWaterPump => 25.0m,
                _ => 5.0m
            };

            var ratio = deviceDesignCOP > 0 ? deviceEfficiency / deviceDesignCOP : 0;

            var newStatus = ratio switch
            {
                >= 0.9m => EfficiencyStatus.High,
                >= 0.7m => EfficiencyStatus.Medium,
                _ => EfficiencyStatus.Low
            };

            if (device.EfficiencyStatus != newStatus)
            {
                await _deviceRepository.UpdateEfficiencyStatusAsync(device.Id, newStatus);
            }
        }
    }

    public async Task ApplyRecommendationAsync(long id, string appliedBy)
    {
        await _optimizationRepository.ApplyRecommendationAsync(id, appliedBy);
        _logger.LogInformation("优化方案 {Id} 已被 {User} 应用", id, appliedBy);
    }

    private void SaveModel(ITransformer model)
    {
        try
        {
            var modelPath = _modelSettings.Value.ModelPath;
            var directory = Path.GetDirectoryName(modelPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fs = File.Create(modelPath);
            _mlContext.Model.Save(model, null, fs);
            _logger.LogInformation("模型已保存到: {ModelPath}", modelPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存模型失败");
        }
    }

    private async Task LoadDefaultModel()
    {
        var modelPath = _modelSettings.Value.ModelPath;
        if (File.Exists(modelPath))
        {
            try
            {
                using var stream = File.OpenRead(modelPath);
                var loadedModel = _mlContext.Model.Load(stream, out _);

                lock (_modelLock)
                {
                    _trainedModel = loadedModel;
                    _predictionEngine = _mlContext.Model.CreatePredictionEngine<EfficiencyInput, EfficiencyPrediction>(loadedModel);
                }

                _logger.LogInformation("已从 {ModelPath} 加载预训练模型", modelPath);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "加载预训练模型失败，使用默认模型");
            }
        }

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

        lock (_modelLock)
        {
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<EfficiencyInput, EfficiencyPrediction>(
                BuildTrainingPipeline().Fit(_mlContext.Data.LoadFromEnumerable(new[] { defaultInput })));
        }

        _logger.LogInformation("已加载默认模型");
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
            var centrifugalCount = deviceList
                .Count(d => d.DeviceTypeId == DeviceType.CentrifugalChiller && d.Status == DeviceStatus.Running);
            var screwCount = deviceList
                .Count(d => d.DeviceTypeId == DeviceType.ScrewChiller && d.Status == DeviceStatus.Running);
            var pumpCount = deviceList
                .Count(d => (d.DeviceTypeId == DeviceType.ChilledWaterPump || d.DeviceTypeId == DeviceType.CoolingWaterPump)
                           && d.Status == DeviceStatus.Running);
            var towerCount = deviceList
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
                    PumpCount = pumpCount,
                    TowerCount = towerCount,
                    COP = (float)record.SystemCOP
                });
            }
        }

        return trainingData;
    }

    private EstimatorChain<RegressionPredictionTransformer<FastTreeRegressionModelParameters>> BuildTrainingPipeline()
    {
        var inputColumns = new[]
        {
            nameof(EfficiencyInput.ChilledWaterSupplyTemp),
            nameof(EfficiencyInput.CoolingWaterInletTemp),
            nameof(EfficiencyInput.LoadRate),
            nameof(EfficiencyInput.CentrifugalCount),
            nameof(EfficiencyInput.ScrewCount),
            nameof(EfficiencyInput.PumpCount),
            nameof(EfficiencyInput.TowerCount)
        };

        var scaledColumns = inputColumns.Select(col => col + "Scaled").ToArray();

        var pipeline = _mlContext.Transforms.NormalizeMinMax("ChilledWaterSupplyTempScaled", nameof(EfficiencyInput.ChilledWaterSupplyTemp))
            .Append(_mlContext.Transforms.NormalizeMinMax("CoolingWaterInletTempScaled", nameof(EfficiencyInput.CoolingWaterInletTemp)))
            .Append(_mlContext.Transforms.NormalizeMinMax("LoadRateScaled", nameof(EfficiencyInput.LoadRate)))
            .Append(_mlContext.Transforms.NormalizeMinMax("CentrifugalCountScaled", nameof(EfficiencyInput.CentrifugalCount)))
            .Append(_mlContext.Transforms.NormalizeMinMax("ScrewCountScaled", nameof(EfficiencyInput.ScrewCount)))
            .Append(_mlContext.Transforms.NormalizeMinMax("PumpCountScaled", nameof(EfficiencyInput.PumpCount)))
            .Append(_mlContext.Transforms.NormalizeMinMax("TowerCountScaled", nameof(EfficiencyInput.TowerCount)))
            .Append(_mlContext.Transforms.Concatenate("Features", scaledColumns))
            .Append(_mlContext.Regression.Trainers.FastTree(
                labelColumnName: nameof(EfficiencyInput.COP),
                featureColumnName: "Features",
                numberOfLeaves: 20,
                numberOfTrees: 100,
                minimumExampleCountPerLeaf: 5));

        return pipeline;
    }

    private async Task GenerateDiagnosisReportAsync(EfficiencyRecord record, List<Device> devices)
    {
        var runningChillers = devices
            .Where(d => (d.DeviceTypeId == DeviceType.CentrifugalChiller || d.DeviceTypeId == DeviceType.ScrewChiller)
                        && d.Status == DeviceStatus.Running)
            .ToList();

        var lowEfficiencyDevices = runningChillers
            .Where(d => d.EfficiencyStatus == EfficiencyStatus.Low)
            .ToList();

        var issues = new List<string>();
        var recommendations = new List<string>();

        if (record.ChilledWaterSupplyTemp > 9)
        {
            issues.Add("冷冻水供水温度过高");
            recommendations.Add("降低冷冻水设定温度，检查机组运行状态");
        }

        if (record.CoolingWaterReturnTemp > 33)
        {
            issues.Add("冷却水回水温度过高");
            recommendations.Add("增加冷却塔运行台数，检查冷却塔散热效果");
        }

        if (record.LoadRate < 0.3m)
        {
            issues.Add("系统低负荷运行");
            recommendations.Add("优化设备启停组合，减少部分运行台数");
        }

        if (lowEfficiencyDevices.Any())
        {
            issues.Add($"以下设备效率偏低: {string.Join(",", lowEfficiencyDevices.Select(d => d.Name))}");
            recommendations.Add("检查设备维护情况，考虑更换或优化运行策略");
        }

        if (record.RunningPumpCount > record.RunningChillerCount * 2 + 2)
        {
            issues.Add("水泵运行台数偏多");
            recommendations.Add("减少水泵运行台数，优化水泵匹配");
        }

        var report = new DiagnosisReport
        {
            GeneratedAt = DateTime.UtcNow,
            ReportType = "LowEfficiency",
            Severity = record.EfficiencyRatio < 0.5m ? "Critical" : "Warning",
            Issues = string.Join("|", issues),
            Recommendations = string.Join("|", recommendations),
            RelatedDevices = string.Join(",", lowEfficiencyDevices.Select(d => d.Id)),
            SystemCOP = record.SystemCOP,
            DesignCOP = record.DesignCOP,
            EfficiencyRatio = record.EfficiencyRatio,
            Status = "New"
        };

        await _efficiencyRepository.AddDiagnosisReportAsync(report);
        _logger.LogWarning("生成能效诊断报告，严重程度: {Severity}", report.Severity);
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

                EfficiencyPrediction? prediction = null;
                try
                {
                    _predictionSemaphore.Wait();
                    prediction = _predictionEngine?.Predict(input);
                }
                finally
                {
                    _predictionSemaphore.Release();
                }

                var predictedCOP = prediction?.PredictedCOP ?? 5.0f;

                var combo = new DeviceCombination
                {
                    RunningChillers = chillerCombo,
                    RunningPumps = pumpCombo,
                    RunningTowers = towerCombo,
                    PredictedCOP = (decimal)predictedCOP,
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
