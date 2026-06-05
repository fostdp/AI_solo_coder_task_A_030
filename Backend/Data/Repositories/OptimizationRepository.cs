using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ChillerPlant.Models;
using ChillerPlant.Services;

namespace ChillerPlant.Data.Repositories
{
    public class OptimizationRepository : IOptimizationRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;
        private readonly AppSettings _appSettings;
        private readonly NeuralNetworkModel _neuralNetwork;
        private readonly DecisionTreeModel _decisionTree;
        private readonly string _modelPath;
        private readonly object _modelLock = new object();

        public OptimizationRepository(ApplicationDbContext context, string connectionString, 
            IOptions<AppSettings> appSettings)
        {
            _context = context;
            _connectionString = connectionString;
            _appSettings = appSettings.Value;
            _neuralNetwork = new NeuralNetworkModel();
            _decisionTree = new DecisionTreeModel();
            _modelPath = Path.Combine(AppContext.BaseDirectory, "Data", "neural_model.txt");
            
            if (!Directory.Exists(Path.GetDirectoryName(_modelPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_modelPath));
            }
            
            LoadModel();
        }

        private void LoadModel()
        {
            lock (_modelLock)
            {
                if (File.Exists(_modelPath))
                {
                    try
                    {
                        _neuralNetwork.LoadModel(_modelPath);
                    }
                    catch
                    {
                        InitializePretrainedModel();
                    }
                }
                else
                {
                    InitializePretrainedModel();
                }
            }
        }

        private void InitializePretrainedModel()
        {
            var trainingData = GenerateSyntheticTrainingData();
            _neuralNetwork.TrainBatch(trainingData, 200);
            _neuralNetwork.SaveModel(_modelPath);
        }

        private List<TrainingSample> GenerateSyntheticTrainingData()
        {
            var samples = new List<TrainingSample>();
            var random = new Random(42);

            for (int i = 0; i < 1000; i++)
            {
                var outdoorTemp = random.NextDouble() * 35 + 5;
                var wetBulbTemp = outdoorTemp - random.NextDouble() * 8 - 2;
                var loadRate = random.NextDouble() * 90 + 10;
                var chillerCount = loadRate > 70 ? 3 : loadRate > 50 ? 2 : 1;
                var supplyTemp = random.NextDouble() * 4 + 5;
                var coolingInTemp = random.NextDouble() * 10 + 25;

                double baseCOP = 6.5;
                if (chillerCount == 1) baseCOP = 5.8;
                if (chillerCount == 2) baseCOP = 6.2;
                if (chillerCount == 3) baseCOP = 6.0;

                double tempEffect = (7.0 - supplyTemp) * 0.15;
                double coolingEffect = (30.0 - coolingInTemp) * 0.08;
                double loadEffect = 1.0 - Math.Abs(loadRate - 70) / 100.0;
                double outdoorEffect = (25.0 - outdoorTemp) * 0.03;

                double actualCOP = baseCOP * loadEffect + tempEffect + coolingEffect + outdoorEffect;
                actualCOP += random.NextDouble() * 0.4 - 0.2;
                actualCOP = Math.Max(2.0, Math.Min(8.0, actualCOP));

                samples.Add(new TrainingSample
                {
                    OutdoorTemp = outdoorTemp,
                    WetBulbTemp = wetBulbTemp,
                    ChillerCount = chillerCount,
                    SupplyWaterTemp = supplyTemp,
                    CoolingWaterInTemp = coolingInTemp,
                    LoadRate = loadRate,
                    ActualCOP = actualCOP
                });
            }

            return samples;
        }

        public async Task<OptimizationRecommendationDto> GetLatestRecommendationAsync()
        {
            var recommendation = await _context.OptimizationRecommendations
                .OrderByDescending(r => r.RecommendationTime)
                .FirstOrDefaultAsync();

            if (recommendation == null) return null;

            return new OptimizationRecommendationDto
            {
                RecommendationId = recommendation.RecommendationId,
                RecommendationTime = recommendation.RecommendationTime,
                CurrentLoadRate = recommendation.CurrentLoadRate,
                OutdoorTemp = recommendation.OutdoorTemp,
                WetBulbTemp = recommendation.WetBulbTemp,
                RecommendedChillerCombination = recommendation.RecommendedChillerCombination,
                RecommendedSupplyWaterTemp = recommendation.RecommendedSupplyWaterTemp,
                PredictedCOP = recommendation.PredictedCOP,
                CurrentCOP = recommendation.CurrentCOP,
                ExpectedEnergySaving = recommendation.ExpectedEnergySaving,
                ExpectedEnergySavingPercent = recommendation.ExpectedEnergySavingPercent,
                OptimizationStrategy = recommendation.OptimizationStrategy,
                IsImplemented = recommendation.IsImplemented
            };
        }

        public async Task<List<OptimizationRecommendationDto>> GetRecommendationHistoryAsync(int count = 20)
        {
            return await _context.OptimizationRecommendations
                .OrderByDescending(r => r.RecommendationTime)
                .Take(count)
                .Select(r => new OptimizationRecommendationDto
                {
                    RecommendationId = r.RecommendationId,
                    RecommendationTime = r.RecommendationTime,
                    CurrentLoadRate = r.CurrentLoadRate,
                    OutdoorTemp = r.OutdoorTemp,
                    WetBulbTemp = r.WetBulbTemp,
                    RecommendedChillerCombination = r.RecommendedChillerCombination,
                    RecommendedSupplyWaterTemp = r.RecommendedSupplyWaterTemp,
                    PredictedCOP = r.PredictedCOP,
                    CurrentCOP = r.CurrentCOP,
                    ExpectedEnergySaving = r.ExpectedEnergySaving,
                    ExpectedEnergySavingPercent = r.ExpectedEnergySavingPercent,
                    OptimizationStrategy = r.OptimizationStrategy,
                    IsImplemented = r.IsImplemented
                })
                .ToListAsync();
        }

        public async Task<OptimizationRecommendation> GenerateOptimizationAsync()
        {
            var now = DateTime.Now;
            
            var systemEfficiency = await _context.SystemEfficiencies
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefaultAsync();

            var totalCapacity = await _context.Devices
                .Where(d => d.DeviceTypeId == 1 || d.DeviceTypeId == 2)
                .SumAsync(d => d.RatedCapacity);

            var runningChillers = await _context.DeviceData
                .Where(d => d.Timestamp >= now.AddMinutes(30))
                .GroupBy(d => d.DeviceId)
                .Select(g => new { DeviceId = g.Key, AvgLoad = g.Average(d => d.LoadRate) })
                .Join(_context.Devices, g => g.DeviceId, d => d.DeviceId, (g, d) => new
                {
                    d.DeviceId,
                    d.DeviceName,
                    d.DeviceTypeId,
                    d.RatedCapacity,
                    g.AvgLoad
                })
                .Where(x => x.DeviceTypeId == 1 || x.DeviceTypeId == 2)
                .ToListAsync();

            var currentLoad = runningChillers.Sum(x => (x.AvgLoad ?? 0) * x.RatedCapacity / 100);
            var loadRate = totalCapacity > 0 ? currentLoad / totalCapacity * 100 : 50;

            var outdoorTemp = systemEfficiency?.OutdoorTemp ?? 28;
            var wetBulbTemp = systemEfficiency?.WetBulbTemp ?? 25;

            var optimalCombination = await FindOptimalCombinationAsync(
                (double)loadRate, 
                (double)outdoorTemp, 
                (double)wetBulbTemp);

            var currentCombination = runningChillers.Where(x => x.AvgLoad > 5).Select(x => x.DeviceName).ToList();
            var currentCombinationStr = string.Join(", ", currentCombination);

            var predictedCOP = optimalCombination.PredictedCOP;
            var currentCOP = systemEfficiency?.SystemCOP ?? 4.5m;
            var currentPower = systemEfficiency?.TotalPowerConsumption ?? 2000m;
            var expectedPower = optimalCombination.ExpectedPower;
            var energySaving = Math.Max(0, (double)currentPower - expectedPower);
            var energySavingPercent = currentPower > 0 ? energySaving / (double)currentPower * 100 : 0;

            var strategy = GenerateOptimizationStrategy(
                loadRate, 
                optimalCombination, 
                currentCombinationStr, 
                (double)currentCOP, 
                predictedCOP);

            var chillerIds = optimalCombination.ChillerIds;
            var chillerNames = new List<string>();
            foreach (var id in chillerIds)
            {
                var chiller = await _context.Devices.FindAsync(id);
                if (chiller != null) chillerNames.Add(chiller.DeviceName);
            }

            var recommendation = new OptimizationRecommendation
            {
                RecommendationTime = now,
                CurrentLoadRate = (decimal)loadRate,
                OutdoorTemp = outdoorTemp,
                WetBulbTemp = wetBulbTemp,
                RecommendedChillerCombination = string.Join(", ", chillerNames),
                RecommendedSupplyWaterTemp = (decimal)optimalCombination.SupplyTemp,
                PredictedCOP = (decimal)predictedCOP,
                CurrentCOP = currentCOP,
                ExpectedEnergySaving = (decimal)energySaving,
                ExpectedEnergySavingPercent = (decimal)energySavingPercent,
                OptimizationStrategy = strategy,
                IsImplemented = false,
                CreatedAt = now
            };

            _context.OptimizationRecommendations.Add(recommendation);
            await _context.SaveChangesAsync();

            await SaveTrainingDataAsync();

            return recommendation;
        }

        private string GenerateOptimizationStrategy(decimal loadRate, ChillerCombination combination, 
            string currentCombination, double currentCOP, double predictedCOP)
        {
            var parts = new List<string>();

            if (loadRate > 80)
            {
                parts.Add("当前处于高负荷工况");
                parts.Add("建议投入3台冷水机组并行运行");
            }
            else if (loadRate > 60)
            {
                parts.Add("当前处于中高负荷工况");
                parts.Add("建议投入2台冷水机组，优先运行高效率的离心机");
            }
            else if (loadRate > 40)
            {
                parts.Add("当前处于中等负荷工况");
                parts.Add("建议投入2台冷水机组，可考虑一台离心机+一台螺杆机组合");
            }
            else if (loadRate > 25)
            {
                parts.Add("当前处于中低负荷工况");
                parts.Add("建议投入1台离心机运行");
            }
            else
            {
                parts.Add("当前处于低负荷工况");
                parts.Add("建议投入1台螺杆机运行，避免离心机喘振");
            }

            if (predictedCOP > currentCOP)
            {
                parts.Add($"预期COP从{currentCOP:F2}提升至{predictedCOP:F2}");
                parts.Add($"每小时预计节电约{combination.ExpectedPower * (1 - currentCOP / predictedCOP):F0} kWh");
            }

            parts.Add($"建议冷冻水供水温度设定为{combination.SupplyTemp:F1}°C");
            parts.Add($"对应配置冷冻水泵{combination.PumpIds.Count}台、冷却塔{combination.TowerIds.Count}台");

            return string.Join("；", parts);
        }

        public async Task<ChillerCombination> FindOptimalCombinationAsync(double loadRate, double outdoorTemp, double wetBulbTemp)
        {
            var chillers = await _context.Devices
                .Where(d => d.DeviceTypeId == 1 || d.DeviceTypeId == 2)
                .OrderByDescending(d => d.DesignCOP)
                .ToListAsync();

            var pumps = await _context.Devices
                .Where(d => d.DeviceTypeId == 4)
                .ToListAsync();

            var towers = await _context.Devices
                .Where(d => d.DeviceTypeId == 3)
                .ToListAsync();

            var allCombinations = new List<ChillerCombination>();

            for (int chillerCount = 1; chillerCount <= Math.Min(3, chillers.Count); chillerCount++)
            {
                for (double supplyTemp = 5.0; supplyTemp <= 8.0; supplyTemp += 0.5)
                {
                    for (int pumpCount = 2; pumpCount <= Math.Min(6, pumps.Count); pumpCount += 2)
                    {
                        for (int towerCount = 2; towerCount <= Math.Min(6, towers.Count); towerCount += 2)
                        {
                            var sample = new TrainingSample
                            {
                                OutdoorTemp = outdoorTemp,
                                WetBulbTemp = wetBulbTemp,
                                ChillerCount = chillerCount,
                                SupplyWaterTemp = supplyTemp,
                                CoolingWaterInTemp = 28 + (outdoorTemp - 25) * 0.5,
                                LoadRate = loadRate
                            };

                            double nnCOP = 0;
                            lock (_modelLock)
                            {
                                nnCOP = _neuralNetwork.Predict(sample.GetInputs());
                            }
                            double dtCOP = _decisionTree.Predict(sample);
                            double predictedCOP = (nnCOP * 0.7 + dtCOP * 0.3);

                            var totalCapacity = chillers.Take(chillerCount).Sum(c => c.RatedCapacity) * (decimal)(loadRate / 100);
                            var expectedPower = (double)totalCapacity / predictedCOP;

                            var combination = new ChillerCombination
                            {
                                ChillerIds = chillers.Take(chillerCount).Select(c => c.DeviceId).ToList(),
                                PumpIds = pumps.Take(pumpCount).Select(p => p.DeviceId).ToList(),
                                TowerIds = towers.Take(towerCount).Select(t => t.DeviceId).ToList(),
                                PredictedCOP = predictedCOP,
                                ExpectedPower = expectedPower,
                                ExpectedCapacity = (double)totalCapacity,
                                SupplyTemp = supplyTemp
                            };

                            allCombinations.Add(combination);
                        }
                    }
                }
            }

            return allCombinations
                .OrderByDescending(c => c.PredictedCOP)
                .ThenBy(c => c.ExpectedPower)
                .FirstOrDefault();
        }

        public async Task<List<double>> PredictAllCombinationsCOP(double loadRate, double outdoorTemp, double wetBulbTemp)
        {
            var results = new List<double>();

            for (int chillerCount = 1; chillerCount <= 3; chillerCount++)
            {
                for (double supplyTemp = 5.0; supplyTemp <= 8.0; supplyTemp += 1.0)
                {
                    var sample = new TrainingSample
                    {
                        OutdoorTemp = outdoorTemp,
                        WetBulbTemp = wetBulbTemp,
                        ChillerCount = chillerCount,
                        SupplyWaterTemp = supplyTemp,
                        CoolingWaterInTemp = 28 + (outdoorTemp - 25) * 0.5,
                        LoadRate = loadRate
                    };

                    double predictedCOP;
                    lock (_modelLock)
                    {
                        predictedCOP = _neuralNetwork.Predict(sample.GetInputs());
                    }
                    results.Add(predictedCOP);
                }
            }

            return results;
        }

        public async Task ImplementRecommendationAsync(long recommendationId)
        {
            var recommendation = await _context.OptimizationRecommendations.FindAsync(recommendationId);
            if (recommendation != null)
            {
                recommendation.IsImplemented = true;
                recommendation.ImplementedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task TrainModelAsync()
        {
            var trainingData = await _context.Set<ModelTrainingData>()
                .Where(t => !t.IsUsedForTraining)
                .OrderByDescending(t => t.Timestamp)
                .Take(5000)
                .ToListAsync();

            if (trainingData.Count < 100)
            {
                var syntheticData = GenerateSyntheticTrainingData();
                lock (_modelLock)
                {
                    _neuralNetwork.TrainBatch(syntheticData, 100);
                    _neuralNetwork.SaveModel(_modelPath);
                }
                return;
            }

            var samples = trainingData.Select(t => new TrainingSample
            {
                OutdoorTemp = (double)(t.OutdoorTemp ?? 25),
                WetBulbTemp = (double)(t.WetBulbTemp ?? 22),
                ChillerCount = t.ChillerCount ?? 2,
                SupplyWaterTemp = (double)(t.SupplyWaterTemp ?? 7),
                CoolingWaterInTemp = (double)(t.CoolingWaterInTemp ?? 28),
                LoadRate = (double)(t.LoadRate ?? 50),
                ActualCOP = (double)(t.ActualCOP ?? 5)
            }).ToList();

            lock (_modelLock)
            {
                _neuralNetwork.TrainBatch(samples, 50);
                _neuralNetwork.SaveModel(_modelPath);
            }

            foreach (var data in trainingData)
            {
                data.IsUsedForTraining = true;
            }
            await _context.SaveChangesAsync();
        }

        private async Task SaveTrainingDataAsync()
        {
            var now = DateTime.Now;
            var startTime = now.AddHours(-1);

            var systemData = await _context.SystemEfficiencies
                .Where(s => s.Timestamp >= startTime)
                .OrderByDescending(s => s.Timestamp)
                .Take(120)
                .ToListAsync();

            if (!systemData.Any()) return;

            var latest = systemData.First();
            var chillerCount = await _context.DeviceData
                .Where(d => d.Timestamp >= startTime && d.Status == 1)
                .Join(_context.Devices, d => d.DeviceId, dev => dev.DeviceId, (d, dev) => new
                {
                    d.DeviceId,
                    dev.DeviceTypeId
                })
                .Where(x => x.DeviceTypeId == 1 || x.DeviceTypeId == 2)
                .GroupBy(x => x.DeviceId)
                .CountAsync();

            var avgSupplyTemp = systemData.Any(s => s.TotalCoolingCapacity > 0)
                ? await _context.DeviceData
                    .Where(d => d.Timestamp >= startTime && d.SupplyWaterTemp.HasValue)
                    .Join(_context.Devices, d => d.DeviceId, dev => dev.DeviceId, (d, dev) => new
                    {
                        d.SupplyWaterTemp,
                        dev.DeviceTypeId
                    })
                    .Where(x => x.DeviceTypeId == 1 || x.DeviceTypeId == 2)
                    .AverageAsync(x => x.SupplyWaterTemp)
                : null;

            var avgCoolingInTemp = systemData.Any()
                ? await _context.DeviceData
                    .Where(d => d.Timestamp >= startTime && d.CoolingWaterInTemp.HasValue)
                    .Join(_context.Devices, d => d.DeviceId, dev => dev.DeviceId, (d, dev) => new
                    {
                        d.CoolingWaterInTemp,
                        dev.DeviceTypeId
                    })
                    .Where(x => x.DeviceTypeId == 1 || x.DeviceTypeId == 2)
                    .AverageAsync(x => x.CoolingWaterInTemp)
                : null;

            var totalCapacity = latest.TotalCoolingCapacity ?? 0;
            var ratedCapacity = await _context.Devices
                .Where(d => d.DeviceTypeId == 1 || d.DeviceTypeId == 2)
                .SumAsync(d => d.RatedCapacity);
            var loadRate = ratedCapacity > 0 ? totalCapacity / ratedCapacity * 100 : 0;

            var trainingData = new ModelTrainingData
            {
                Timestamp = now,
                OutdoorTemp = latest.OutdoorTemp,
                WetBulbTemp = latest.WetBulbTemp,
                ChillerCombination = null,
                ChillerCount = chillerCount,
                SupplyWaterTemp = avgSupplyTemp,
                CoolingWaterInTemp = avgCoolingInTemp,
                LoadRate = loadRate > 0 ? loadRate : (decimal?)null,
                TotalPower = latest.TotalPowerConsumption,
                TotalCooling = latest.TotalCoolingCapacity,
                ActualCOP = latest.SystemCOP,
                IsUsedForTraining = false,
                CreatedAt = now
            };

            _context.Add(trainingData);
            await _context.SaveChangesAsync();
        }
    }

    public class ModelTrainingData
    {
        public long TrainingId { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal? OutdoorTemp { get; set; }
        public decimal? WetBulbTemp { get; set; }
        public string ChillerCombination { get; set; }
        public int? ChillerCount { get; set; }
        public decimal? SupplyWaterTemp { get; set; }
        public decimal? CoolingWaterInTemp { get; set; }
        public decimal? LoadRate { get; set; }
        public decimal? TotalPower { get; set; }
        public decimal? TotalCooling { get; set; }
        public decimal? ActualCOP { get; set; }
        public bool IsUsedForTraining { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public static class ChillerCombinationExtensions
    {
        public static double SupplyTemp { get; set; }
    }
}
