using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ChillerPlant.Modules.EfficiencyOptimizer.Configuration;
using ChillerPlant.Modules.Shared.Commands;

namespace ChillerPlant.Modules.EfficiencyOptimizer.Services
{
    public class EfficiencyBackgroundService : BackgroundService
    {
        private readonly ILogger<EfficiencyBackgroundService> _logger;
        private readonly IMediator _mediator;
        private readonly OptimizationSettings _settings;
        private DateTime _lastModelTrainingTime = DateTime.MinValue;

        public EfficiencyBackgroundService(
            ILogger<EfficiencyBackgroundService> logger,
            IMediator mediator,
            IOptions<OptimizationSettings> settings)
        {
            _logger = logger;
            _mediator = mediator;
            _settings = settings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"EfficiencyBackgroundService started, calc interval: {_settings.EfficiencyCalcIntervalSeconds}s, auto-retrain: {_settings.AutoRetrainIntervalMinutes}min");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _mediator.Send(new CalculateSystemEfficiencyCommand(), stoppingToken);

                    if ((DateTime.Now - _lastModelTrainingTime).TotalMinutes >= _settings.AutoRetrainIntervalMinutes)
                    {
                        try
                        {
                            await _mediator.Send(new TrainOptimizationModelCommand 
                            { 
                                Epochs = _settings.TrainingEpochs 
                            }, stoppingToken);
                            _lastModelTrainingTime = DateTime.Now;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Auto model training failed: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in efficiency background service: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(_settings.EfficiencyCalcIntervalSeconds), stoppingToken);
            }

            _logger.LogInformation("EfficiencyBackgroundService stopped");
        }
    }
}
