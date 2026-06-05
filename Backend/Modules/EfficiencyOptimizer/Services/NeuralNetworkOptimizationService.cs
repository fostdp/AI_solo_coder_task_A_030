using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ChillerPlant.Modules.EfficiencyOptimizer.Configuration;
using ChillerPlant.Modules.EfficiencyOptimizer.Models;
using ChillerPlant.Services;

namespace ChillerPlant.Modules.EfficiencyOptimizer.Services
{
    public class NeuralNetworkOptimizationService
    {
        private readonly NeuralNetworkModel _neuralNetwork;
        private readonly DecisionTreeModel _decisionTree;
        private readonly OptimizationSettings _settings;
        private readonly ILogger<NeuralNetworkOptimizationService> _logger;
        private readonly string _modelFilePath;

        public NeuralNetworkOptimizationService(
            IOptions<OptimizationSettings> settings,
            ILogger<NeuralNetworkOptimizationService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _neuralNetwork = new NeuralNetworkModel();
            _decisionTree = new DecisionTreeModel();
            
            _modelFilePath = GetModelFilePath();
            _logger.LogInformation($"NeuralNetworkOptimizationService initialized, model path: {_modelFilePath}");
            
            LoadModel();
        }

        private string GetModelFilePath()
        {
            var path = _settings.ModelWeightsPath;
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), path);
            }
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return path;
        }

        public void LoadModel()
        {
            try
            {
                _neuralNetwork.LoadModel(_modelFilePath);
                _logger.LogInformation($"Model loaded successfully from: {_modelFilePath}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to load model from {_modelFilePath}: {ex.Message}, using default weights");
            }
        }

        public void SaveModel()
        {
            try
            {
                _neuralNetwork.SaveModel(_modelFilePath);
                _logger.LogInformation($"Model saved successfully to: {_modelFilePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save model to {_modelFilePath}: {ex.Message}");
            }
        }

        public double PredictCOP(OptimizationInput input)
        {
            var features = new[]
            {
                input.OutdoorTemp,
                input.WetBulbTemp,
                input.ChillerCount,
                input.SupplyWaterTemp,
                input.CoolingWaterInTemp,
                input.LoadRate
            };

            var nnPrediction = _neuralNetwork.Predict(features);
            var dtPrediction = _decisionTree.Predict(new TrainingSample
            {
                OutdoorTemp = input.OutdoorTemp,
                WetBulbTemp = input.WetBulbTemp,
                ChillerCount = input.ChillerCount,
                SupplyWaterTemp = input.SupplyWaterTemp,
                CoolingWaterInTemp = input.CoolingWaterInTemp,
                LoadRate = input.LoadRate
            });

            var blended = nnPrediction * 0.7 + dtPrediction * 0.3;
            _logger.LogDebug($"COP Prediction: NN={nnPrediction:F3}, DT={dtPrediction:F3}, Blended={blended:F3}");
            return blended;
        }

        public OptimizationRecommendation GenerateRecommendation(OptimizationInput currentInput)
        {
            var recommendation = new OptimizationRecommendation
            {
                GeneratedAt = DateTime.Now,
                CurrentCOP = PredictCOP(currentInput),
                CurrentSupplyTemp = currentInput.SupplyWaterTemp,
                CurrentChillerCount = currentInput.ChillerCount,
                ExpectedEnergySaving = 0
            };

            double bestCOP = recommendation.CurrentCOP;
            var bestChillerCount = currentInput.ChillerCount;
            var bestSupplyTemp = currentInput.SupplyWaterTemp;

            for (int chillerCount = 1; chillerCount <= 3; chillerCount++)
            {
                for (double supplyTemp = 5.0; supplyTemp <= 8.5; supplyTemp += 0.5)
                {
                    var testInput = new OptimizationInput
                    {
                        OutdoorTemp = currentInput.OutdoorTemp,
                        WetBulbTemp = currentInput.WetBulbTemp,
                        ChillerCount = chillerCount,
                        SupplyWaterTemp = supplyTemp,
                        CoolingWaterInTemp = currentInput.CoolingWaterInTemp,
                        LoadRate = currentInput.LoadRate
                    };

                    var predictedCOP = PredictCOP(testInput);
                    if (predictedCOP > bestCOP * 1.02)
                    {
                        bestCOP = predictedCOP;
                        bestChillerCount = chillerCount;
                        bestSupplyTemp = supplyTemp;
                    }
                }
            }

            if (bestCOP > recommendation.CurrentCOP * 1.02)
            {
                recommendation.OptimalChillerCount = bestChillerCount;
                recommendation.OptimalSupplyTemp = bestSupplyTemp;
                recommendation.PredictedOptimalCOP = bestCOP;
                recommendation.ExpectedEnergySaving = (bestCOP - recommendation.CurrentCOP) / recommendation.CurrentCOP * 100;
                recommendation.RecommendationType = "optimization";
                recommendation.Description = $"建议将冷水机组数量调整为{bestChillerCount}台，冷冻水出水温度调整为{bestSupplyTemp:F1}°C，预计COP提升{recommendation.ExpectedEnergySaving:F1}%";
            }
            else
            {
                recommendation.OptimalChillerCount = currentInput.ChillerCount;
                recommendation.OptimalSupplyTemp = currentInput.SupplyWaterTemp;
                recommendation.PredictedOptimalCOP = recommendation.CurrentCOP;
                recommendation.RecommendationType = "maintain";
                recommendation.Description = "当前运行参数已接近最优，建议保持";
            }

            _logger.LogInformation($"Recommendation generated: Type={recommendation.RecommendationType}, Saving={recommendation.ExpectedEnergySaving:F1}%");
            return recommendation;
        }

        public void TrainModel(List<TrainingSample> samples, int epochs = 200)
        {
            if (samples.Count < _settings.MinTrainingSamples)
            {
                _logger.LogWarning($"Insufficient training samples: {samples.Count}, required: {_settings.MinTrainingSamples}");
                return;
            }

            _logger.LogInformation($"Starting model training with {samples.Count} samples, {epochs} epochs");
            _neuralNetwork.TrainBatch(samples, epochs);
            SaveModel();
            _logger.LogInformation("Model training completed");
        }

        public bool IsModelReady()
        {
            return _neuralNetwork != null;
        }

        public (double InputMean, double InputStd)[] GetScalerParameters()
        {
            var parameters = new (double, double)[6];
            var scalerField = _neuralNetwork.GetType().GetField("_inputScaler", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var scaler = scalerField?.GetValue(_neuralNetwork) as StandardScaler;
            
            for (int i = 0; i < 6; i++)
            {
                parameters[i] = (
                    scaler != null && scaler.IsFitted ? scaler.Means[i] : 0,
                    scaler != null && scaler.IsFitted ? scaler.StandardDeviations[i] : 1
                );
            }
            return parameters;
        }
    }
}
