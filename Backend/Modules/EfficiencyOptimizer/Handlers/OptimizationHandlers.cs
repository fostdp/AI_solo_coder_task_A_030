using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ChillerPlant.Data;
using ChillerPlant.Models;
using ChillerPlant.Modules.Shared.Commands;
using ChillerPlant.Modules.Shared.Events;
using ChillerPlant.Modules.EfficiencyOptimizer.Services;
using ChillerPlant.Modules.EfficiencyOptimizer.Models;
using ChillerPlant.Services;
using EffModels = ChillerPlant.Modules.EfficiencyOptimizer.Models;

namespace ChillerPlant.Modules.EfficiencyOptimizer.Handlers
{
    public class CalculateSystemEfficiencyHandler : IRequestHandler<CalculateSystemEfficiencyCommand, Unit>
    {
        private readonly ApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<CalculateSystemEfficiencyHandler> _logger;

        public CalculateSystemEfficiencyHandler(
            ApplicationDbContext context,
            IMediator mediator,
            ILogger<CalculateSystemEfficiencyHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Unit> Handle(CalculateSystemEfficiencyCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var startTime = DateTime.Now.AddMinutes(-5);
                var deviceTypes = await _context.DeviceTypes.ToListAsync(cancellationToken);
                
                var chillerTypeId = deviceTypes.FirstOrDefault(dt => dt.TypeName == "冷水机组")?.DeviceTypeId ?? 1;
                var pumpTypeId = deviceTypes.FirstOrDefault(dt => dt.TypeName == "冷冻水泵")?.DeviceTypeId ?? 2;
                var towerTypeId = deviceTypes.FirstOrDefault(dt => dt.TypeName == "冷却塔")?.DeviceTypeId ?? 3;

                var chillerData = await GetLatestDeviceData(chillerTypeId, startTime, cancellationToken);
                var pumpData = await GetLatestDeviceData(pumpTypeId, startTime, cancellationToken);
                var towerData = await GetLatestDeviceData(towerTypeId, startTime, cancellationToken);

                var totalPower = chillerData.Sum(d => d.Power) + 
                               pumpData.Sum(d => d.Power) + 
                               towerData.Sum(d => d.Power);

                var totalCooling = chillerData
                    .Where(d => d.COP.HasValue && d.Power > 0)
                    .Sum(d => d.Power * d.COP.Value);

                var designCOP = await _context.Devices
                    .Where(d => d.DeviceTypeId == chillerTypeId)
                    .AverageAsync(d => d.DesignCOP, cancellationToken);

                var systemCOP = totalPower > 0 ? totalCooling / totalPower : 0;
                var copRatio = designCOP > 0 ? (decimal)(systemCOP / (double)designCOP) : 0;

                var efficiencyData = new SystemEfficiency
                {
                    Timestamp = DateTime.Now,
                    SystemCOP = (decimal)systemCOP,
                    DesignCOP = designCOP,
                    COPRatio = copRatio,
                    TotalPower = totalPower,
                    TotalCooling = totalCooling,
                    RunningChillerCount = chillerData.Count(d => d.Status == 1),
                    RunningPumpCount = pumpData.Count(d => d.Status == 1),
                    RunningTowerCount = towerData.Count(d => d.Status == 1),
                    OutdoorTemp = 28,
                    WetBulbTemp = 25
                };

                _context.SystemEfficiencies.Add(efficiencyData);
                await _context.SaveChangesAsync(cancellationToken);

                await _mediator.Publish(new SystemEfficiencyCalculatedEvent
                {
                    Timestamp = efficiencyData.Timestamp,
                    SystemCOP = efficiencyData.SystemCOP,
                    DesignCOP = efficiencyData.DesignCOP,
                    COPRatio = efficiencyData.COPRatio,
                    TotalPower = efficiencyData.TotalPower,
                    TotalCooling = efficiencyData.TotalCooling
                }, cancellationToken);

                _logger.LogInformation($"System efficiency calculated: COP={systemCOP:F2}, TotalPower={totalPower:F2}kW");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating system efficiency: {ex.Message}");
            }

            return Unit.Value;
        }

        private async Task<List<DeviceData>> GetLatestDeviceData(int deviceTypeId, DateTime startTime, CancellationToken cancellationToken)
        {
            var deviceIds = await _context.Devices
                .Where(d => d.DeviceTypeId == deviceTypeId)
                .Select(d => d.DeviceId)
                .ToListAsync(cancellationToken);

            var latestData = new List<DeviceData>();
            foreach (var deviceId in deviceIds)
            {
                var data = await _context.DeviceData
                    .Where(d => d.DeviceId == deviceId && d.Timestamp >= startTime)
                    .OrderByDescending(d => d.Timestamp)
                    .FirstOrDefaultAsync(cancellationToken);
                if (data != null)
                    latestData.Add(data);
            }
            return latestData;
        }
    }

    public class GenerateOptimizationHandler : IRequestHandler<GenerateOptimizationCommand, OptimizationRecommendationDto>
    {
        private readonly ApplicationDbContext _context;
        private readonly NeuralNetworkOptimizationService _optimizationService;
        private readonly IMediator _mediator;
        private readonly ILogger<GenerateOptimizationHandler> _logger;

        public GenerateOptimizationHandler(
            ApplicationDbContext context,
            NeuralNetworkOptimizationService optimizationService,
            IMediator mediator,
            ILogger<GenerateOptimizationHandler> logger)
        {
            _context = context;
            _optimizationService = optimizationService;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<OptimizationRecommendationDto> Handle(GenerateOptimizationCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var startTime = DateTime.Now.AddMinutes(-10);
                var chillerType = await _context.DeviceTypes
                    .FirstOrDefaultAsync(dt => dt.TypeName == "冷水机组", cancellationToken);
                
                var chillers = await _context.Devices
                    .Where(d => d.DeviceTypeId == chillerType.DeviceTypeId)
                    .ToListAsync(cancellationToken);

                var latestData = await _context.DeviceData
                    .Where(d => chillers.Select(c => c.DeviceId).Contains(d.DeviceId) 
                        && d.Timestamp >= startTime && d.Status == 1)
                    .GroupBy(d => d.DeviceId)
                    .Select(g => g.OrderByDescending(d => d.Timestamp).FirstOrDefault())
                    .ToListAsync(cancellationToken);

                if (!latestData.Any())
                {
                    _logger.LogWarning("No running chiller data available for optimization");
                    return null;
                }

                var avgSupplyTemp = latestData.Average(d => d.SupplyWaterTemp ?? 7);
                var avgCoolingInTemp = latestData.Average(d => d.CoolingWaterInTemp ?? 30);
                var avgLoadRate = latestData.Average(d => d.LoadRate ?? 50);
                var runningCount = latestData.Count;

                var input = new EffModels.OptimizationInput
                {
                    OutdoorTemp = 28,
                    WetBulbTemp = 25,
                    ChillerCount = runningCount,
                    SupplyWaterTemp = (double)avgSupplyTemp,
                    CoolingWaterInTemp = (double)avgCoolingInTemp,
                    LoadRate = (double)avgLoadRate
                };

                var recommendation = _optimizationService.GenerateRecommendation(input);

                var entity = new ChillerPlant.Models.OptimizationRecommendation
                {
                    RecommendationTime = recommendation.GeneratedAt,
                    CurrentLoadRate = (decimal)input.LoadRate,
                    OutdoorTemp = (decimal)input.OutdoorTemp,
                    WetBulbTemp = (decimal)input.WetBulbTemp,
                    RecommendedChillerCombination = recommendation.OptimalChillerCount.ToString(),
                    RecommendedSupplyWaterTemp = (decimal)recommendation.OptimalSupplyTemp,
                    PredictedCOP = (decimal)recommendation.PredictedOptimalCOP,
                    CurrentCOP = (decimal)recommendation.CurrentCOP,
                    ExpectedEnergySaving = (decimal)recommendation.ExpectedEnergySaving,
                    ExpectedEnergySavingPercent = (decimal)recommendation.ExpectedEnergySaving,
                    OptimizationStrategy = recommendation.RecommendationType,
                    Description = recommendation.Description,
                    IsImplemented = false
                };

                _context.OptimizationRecommendations.Add(entity);
                await _context.SaveChangesAsync(cancellationToken);
                recommendation.RecommendationId = entity.RecommendationId;

                await _mediator.Publish(new OptimizationRecommendationGeneratedEvent
                {
                    RecommendationId = entity.RecommendationId,
                    GeneratedAt = entity.RecommendationTime,
                    PredictedCOP = (double)entity.PredictedCOP,
                    ExpectedEnergySaving = (double)entity.ExpectedEnergySaving,
                    RecommendedSupplyTemp = (double)entity.RecommendedSupplyWaterTemp
                }, cancellationToken);

                return new OptimizationRecommendationDto
                {
                    RecommendationId = entity.RecommendationId,
                    RecommendationTime = entity.RecommendationTime,
                    GeneratedAt = entity.RecommendationTime,
                    CurrentLoadRate = entity.CurrentLoadRate,
                    OutdoorTemp = entity.OutdoorTemp,
                    WetBulbTemp = entity.WetBulbTemp,
                    RecommendedChillerCombination = entity.RecommendedChillerCombination,
                    RecommendedSupplyWaterTemp = entity.RecommendedSupplyWaterTemp,
                    PredictedCOP = entity.PredictedCOP,
                    PredictedOptimalCOP = entity.PredictedCOP,
                    CurrentCOP = entity.CurrentCOP,
                    CurrentSupplyTemp = (decimal)recommendation.CurrentSupplyTemp,
                    OptimalSupplyTemp = entity.RecommendedSupplyWaterTemp,
                    CurrentChillerCount = recommendation.CurrentChillerCount,
                    OptimalChillerCount = recommendation.OptimalChillerCount,
                    ExpectedEnergySaving = entity.ExpectedEnergySaving,
                    ExpectedEnergySavingPercent = entity.ExpectedEnergySavingPercent,
                    OptimizationStrategy = entity.OptimizationStrategy,
                    RecommendationType = recommendation.RecommendationType,
                    Description = recommendation.Description,
                    IsImplemented = entity.IsImplemented
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating optimization: {ex.Message}");
                return null;
            }
        }
    }

    public class TrainOptimizationModelHandler : IRequestHandler<TrainOptimizationModelCommand, bool>
    {
        private readonly ApplicationDbContext _context;
        private readonly NeuralNetworkOptimizationService _optimizationService;
        private readonly ILogger<TrainOptimizationModelHandler> _logger;

        public TrainOptimizationModelHandler(
            ApplicationDbContext context,
            NeuralNetworkOptimizationService optimizationService,
            ILogger<TrainOptimizationModelHandler> logger)
        {
            _context = context;
            _optimizationService = optimizationService;
            _logger = logger;
        }

        public async Task<bool> Handle(TrainOptimizationModelCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var startTime = DateTime.Now.AddHours(-72);
                var chillerType = await _context.DeviceTypes
                    .FirstOrDefaultAsync(dt => dt.TypeName == "冷水机组", cancellationToken);

                var trainingData = await _context.DeviceData
                    .Where(d => d.Device.DeviceTypeId == chillerType.DeviceTypeId 
                        && d.Timestamp >= startTime 
                        && d.Status == 1 
                        && d.COP.HasValue
                        && d.COP > 2
                        && d.LoadRate > 0
                        && d.Power > 0)
                    .Select(d => new
                    {
                        d.Timestamp,
                        d.Power,
                        d.COP,
                        d.LoadRate,
                        d.SupplyWaterTemp,
                        d.CoolingWaterInTemp,
                        d.FlowRate
                    })
                    .OrderByDescending(d => d.Timestamp)
                    .Take(500)
                    .ToListAsync(cancellationToken);

                var samples = new List<TrainingSample>();
                var random = new Random();

                foreach (var data in trainingData)
                {
                    var sample = new TrainingSample
                    {
                        OutdoorTemp = 25 + random.NextDouble() * 8,
                        WetBulbTemp = 22 + random.NextDouble() * 5,
                        ChillerCount = 1 + random.Next(0, 3),
                        SupplyWaterTemp = (double)(data.SupplyWaterTemp ?? 7),
                        CoolingWaterInTemp = (double)(data.CoolingWaterInTemp ?? 30),
                        LoadRate = (double)(data.LoadRate ?? 50),
                        ActualCOP = (double)(data.COP ?? 5)
                    };
                    samples.Add(sample);
                }

                if (samples.Count < 50)
                {
                    _logger.LogWarning($"Insufficient training samples: {samples.Count}, required at least 50");
                    return false;
                }

                _optimizationService.TrainModel(samples, request.Epochs);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error training model: {ex.Message}");
                return false;
            }
        }
    }
}
