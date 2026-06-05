using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ChillerPlant.Modules.Shared.Commands;

namespace ChillerPlant.Modules.AlarmManager.Services
{
    public class AlarmBackgroundService : BackgroundService
    {
        private readonly ILogger<AlarmBackgroundService> _logger;
        private readonly IMediator _mediator;
        private readonly int _evaluationIntervalSeconds = 60;

        public AlarmBackgroundService(
            ILogger<AlarmBackgroundService> logger,
            IMediator mediator)
        {
            _logger = logger;
            _mediator = mediator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"AlarmBackgroundService started, evaluation interval: {_evaluationIntervalSeconds}s");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _mediator.Send(new CheckAlarmsCommand(), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in alarm background service: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(_evaluationIntervalSeconds), stoppingToken);
            }

            _logger.LogInformation("AlarmBackgroundService stopped");
        }
    }
}
