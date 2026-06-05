using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Xunit;
using ChillerPlant.Modules.EfficiencyOptimizer.Configuration;
using ChillerPlant.Modules.EfficiencyOptimizer.Handlers;
using ChillerPlant.Modules.EfficiencyOptimizer.Services;
using ChillerPlant.Modules.EfficiencyOptimizer.Models;
using ChillerPlant.Modules.Shared.Commands;
using ChillerPlant.Services;

namespace ChillerPlant.Tests
{
    public class EfficiencyOptimizerTests : TestBase
    {
        private readonly Mock<IMediator> _mediatorMock;
        private readonly Mock<ILogger<NeuralNetworkOptimizationService>> _nnLogger;
        private readonly Mock<ILogger<CalculateSystemEfficiencyHandler>> _calcHandlerLogger;
        private readonly Mock<ILogger<GenerateOptimizationHandler>> _genHandlerLogger;
        private readonly Mock<ILogger<TrainOptimizationModelHandler>> _trainHandlerLogger;
        private readonly string _testModelPath;

        public EfficiencyOptimizerTests()
        {
            _mediatorMock = new Mock<IMediator>();
            _nnLogger = CreateLoggerMock<NeuralNetworkOptimizationService>();
            _calcHandlerLogger = CreateLoggerMock<CalculateSystemEfficiencyHandler>();
            _genHandlerLogger = CreateLoggerMock<GenerateOptimizationHandler>();
            _trainHandlerLogger = CreateLoggerMock<TrainOptimizationModelHandler>();
            _testModelPath = Path.Combine(Path.GetTempPath(), $"test_model_{Guid.NewGuid()}.txt");
        }

        [Fact]
        public void OptimizationSettings_ShouldHaveDefaultValues()
        {
            var settings = new OptimizationSettings();

            Assert.Equal("Data/neural_network_model.txt", settings.ModelWeightsPath);
            Assert.Equal(300, settings.AutoRetrainIntervalMinutes);
            Assert.Equal(30, settings.EfficiencyCalcIntervalSeconds);
            Assert.Equal(200, settings.TrainingEpochs);
            Assert.Equal(50, settings.MinTrainingSamples);
            Assert.Equal(72, settings.TrainingDataHours);
        }

        [Fact]
        public void NeuralNetworkOptimizationService_ShouldLoadModelFromConfig()
        {
            var settings = new OptimizationSettings
            {
                ModelWeightsPath = _testModelPath
            };
            var options = CreateOptionsMock(settings);

            var service = new NeuralNetworkOptimizationService(options, _nnLogger.Object);

            Assert.NotNull(service);
            Assert.True(service.IsModelReady());
            _nnLogger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(_testModelPath)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void NeuralNetworkOptimizationService_ShouldPredictCOP()
        {
            var settings = new OptimizationSettings { ModelWeightsPath = _testModelPath };
            var options = CreateOptionsMock(settings);
            var service = new NeuralNetworkOptimizationService(options, _nnLogger.Object);

            var input = new OptimizationInput
            {
                OutdoorTemp = 28,
                WetBulbTemp = 25,
                ChillerCount = 2,
                SupplyWaterTemp = 7,
                CoolingWaterInTemp = 30,
                LoadRate = 60
            };

            var cop = service.PredictCOP(input);

            Assert.True(cop > 0);
            Assert.True(cop < 10);
        }

        [Fact]
        public void NeuralNetworkOptimizationService_ShouldGenerateRecommendation()
        {
            var settings = new OptimizationSettings { ModelWeightsPath = _testModelPath };
            var options = CreateOptionsMock(settings);
            var service = new NeuralNetworkOptimizationService(options, _nnLogger.Object);

            var input = new OptimizationInput
            {
                OutdoorTemp = 28,
                WetBulbTemp = 25,
                ChillerCount = 3,
                SupplyWaterTemp = 6.5,
                CoolingWaterInTemp = 30,
                LoadRate = 40
            };

            var recommendation = service.GenerateRecommendation(input);

            Assert.NotNull(recommendation);
            Assert.True(recommendation.CurrentCOP > 0);
            Assert.True(recommendation.OptimalChillerCount >= 1);
            Assert.True(recommendation.OptimalChillerCount <= 3);
            Assert.True(recommendation.OptimalSupplyTemp >= 5);
            Assert.True(recommendation.OptimalSupplyTemp <= 8.5);
            Assert.NotNull(recommendation.RecommendationType);
            Assert.NotNull(recommendation.Description);
        }

        [Fact]
        public void NeuralNetworkOptimizationService_ShouldTrainModel()
        {
            var settings = new OptimizationSettings { ModelWeightsPath = _testModelPath, MinTrainingSamples = 5 };
            var options = CreateOptionsMock(settings);
            var service = new NeuralNetworkOptimizationService(options, _nnLogger.Object);

            var samples = new List<TrainingSample>();
            var random = new Random();
            for (int i = 0; i < 50; i++)
            {
                samples.Add(new TrainingSample
                {
                    OutdoorTemp = 25 + random.NextDouble() * 10,
                    WetBulbTemp = 22 + random.NextDouble() * 6,
                    ChillerCount = random.Next(1, 4),
                    SupplyWaterTemp = 5 + random.NextDouble() * 3.5,
                    CoolingWaterInTemp = 25 + random.NextDouble() * 10,
                    LoadRate = 20 + random.NextDouble() * 70,
                    ActualCOP = 4 + random.NextDouble() * 2
                });
            }

            service.TrainModel(samples, 50);

            Assert.True(File.Exists(_testModelPath));
            var fileInfo = new FileInfo(_testModelPath);
            Assert.True(fileInfo.Length > 0);
        }

        [Fact]
        public async Task CalculateSystemEfficiencyHandler_ShouldCalculateEfficiency()
        {
            var handler = new CalculateSystemEfficiencyHandler(_context, _mediatorMock.Object, _calcHandlerLogger.Object);
            var command = new CalculateSystemEfficiencyCommand();

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.Equal(Unit.Value, result);
            _mediatorMock.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
            
            var efficiencies = await _context.SystemEfficiencies.ToListAsync();
            Assert.True(efficiencies.Count >= 3);
        }

        [Fact]
        public async Task GenerateOptimizationHandler_ShouldGenerateRecommendation()
        {
            var nnServiceLogger = CreateLoggerMock<NeuralNetworkOptimizationService>();
            var settings = new OptimizationSettings { ModelWeightsPath = _testModelPath };
            var options = CreateOptionsMock(settings);
            var nnService = new NeuralNetworkOptimizationService(options, nnServiceLogger.Object);

            var handler = new GenerateOptimizationHandler(_context, nnService, _mediatorMock.Object, _genHandlerLogger.Object);
            var command = new GenerateOptimizationCommand();

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.CurrentCOP > 0);
            Assert.True(result.OptimalChillerCount >= 1);
            Assert.NotNull(result.Description);
            _mediatorMock.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TrainOptimizationModelHandler_WithInsufficientData_ShouldReturnFalse()
        {
            var nnServiceLogger = CreateLoggerMock<NeuralNetworkOptimizationService>();
            var settings = new OptimizationSettings { ModelWeightsPath = _testModelPath, MinTrainingSamples = 1000 };
            var options = CreateOptionsMock(settings);
            var nnService = new NeuralNetworkOptimizationService(options, nnServiceLogger.Object);

            var handler = new TrainOptimizationModelHandler(_context, nnService, _trainHandlerLogger.Object);
            var command = new TrainOptimizationModelCommand { Epochs = 10 };

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.False(result);
        }

        [Fact]
        public void StandardScaler_ShouldNormalizeData()
        {
            var scaler = new StandardScaler(2);
            var data = new List<double[]>
            {
                new double[] { 10, 20 },
                new double[] { 20, 40 },
                new double[] { 30, 60 },
                new double[] { 40, 80 },
                new double[] { 50, 100 }
            };

            scaler.Fit(data);

            Assert.True(scaler.IsFitted);
            Assert.Equal(30, scaler.Means[0], 1);
            Assert.Equal(60, scaler.Means[1], 1);

            var transformed = scaler.Transform(new double[] { 30, 60 });
            Assert.Equal(0, transformed[0], 2);
            Assert.Equal(0, transformed[1], 2);
        }

        [Fact]
        public void NeuralNetworkModel_ShouldSaveAndLoadModel()
        {
            var model = new NeuralNetworkModel();
            var samples = new List<TrainingSample>();
            var random = new Random();
            for (int i = 0; i < 100; i++)
            {
                samples.Add(new TrainingSample
                {
                    OutdoorTemp = 25 + random.NextDouble() * 10,
                    WetBulbTemp = 22 + random.NextDouble() * 6,
                    ChillerCount = random.Next(1, 4),
                    SupplyWaterTemp = 5 + random.NextDouble() * 3.5,
                    CoolingWaterInTemp = 25 + random.NextDouble() * 10,
                    LoadRate = 20 + random.NextDouble() * 70,
                    ActualCOP = 4 + random.NextDouble() * 2
                });
            }
            model.TrainBatch(samples, 10);

            model.SaveModel(_testModelPath);
            Assert.True(File.Exists(_testModelPath));

            var loadedModel = new NeuralNetworkModel();
            loadedModel.LoadModel(_testModelPath);

            var testInput = new double[] { 28, 25, 2, 7, 30, 60 };
            var prediction1 = model.Predict(testInput);
            var prediction2 = loadedModel.Predict(testInput);

            Assert.Equal(prediction1, prediction2, 2);
        }

        [Fact]
        public void DecisionTreeModel_ShouldPredictBasedOnLoadRate()
        {
            var model = new DecisionTreeModel();

            var highLoadSample = new TrainingSample { LoadRate = 90, SupplyWaterTemp = 7, CoolingWaterInTemp = 30, OutdoorTemp = 28 };
            var highPrediction = model.Predict(highLoadSample);
            var highOptimization = model.GetOptimization(highLoadSample);
            Assert.Equal(3, highOptimization.ChillerCount);

            var mediumLoadSample = new TrainingSample { LoadRate = 50, SupplyWaterTemp = 7, CoolingWaterInTemp = 30, OutdoorTemp = 28 };
            var mediumPrediction = model.Predict(mediumLoadSample);
            var mediumOptimization = model.GetOptimization(mediumLoadSample);
            Assert.Equal(2, mediumOptimization.ChillerCount);

            var lowLoadSample = new TrainingSample { LoadRate = 20, SupplyWaterTemp = 7, CoolingWaterInTemp = 30, OutdoorTemp = 28 };
            var lowPrediction = model.Predict(lowLoadSample);
            var lowOptimization = model.GetOptimization(lowLoadSample);
            Assert.Equal(1, lowOptimization.ChillerCount);
        }

        public override void Dispose()
        {
            if (File.Exists(_testModelPath))
            {
                try { File.Delete(_testModelPath); } catch { }
            }
            base.Dispose();
        }
    }
}
