using BacnetSimulator.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BacnetSimulator.Services;

public class SimulatorBackgroundService : BackgroundService
{
    private readonly ILogger<SimulatorBackgroundService> _logger;
    private readonly SimulatorConfig _config;
    private readonly DeviceDataGenerator _dataGenerator;
    private readonly BacnetProtocolSimulator _protocolSimulator;
    private int _iterationCount = 0;

    public SimulatorBackgroundService(
        ILogger<SimulatorBackgroundService> logger,
        IOptions<SimulatorConfig> config,
        DeviceDataGenerator dataGenerator,
        BacnetProtocolSimulator protocolSimulator)
    {
        _logger = logger;
        _config = config.Value;
        _dataGenerator = dataGenerator;
        _protocolSimulator = protocolSimulator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("=");
        _logger.LogInformation("  BACnet Device Simulator Starting");
        _logger.LogInformation("=");
        _logger.LogInformation("Target: {Address}:{Port}", _config.TargetAddress, _config.TargetPort);
        _logger.LogInformation("Interval: {Interval}s", _config.SendIntervalSeconds);
        _logger.LogInformation("Devices: {Centrifugal} centrifugal, {Screw} screw, {Tower} cooling tower, {ChilledPump} chilled pump, {CoolingPump} cooling pump",
            _config.CentrifugalCount,
            _config.ScrewCount,
            _config.CoolingTowerCount,
            _config.ChilledPumpCount,
            _config.CoolingPumpCount);
        _logger.LogInformation("Total devices: {Total}", 
            _config.CentrifugalCount + _config.ScrewCount + _config.CoolingTowerCount + 
            _config.ChilledPumpCount + _config.CoolingPumpCount);
        _logger.LogInformation("Load factor: {LoadFactor:P}", _config.LoadFactor);
        _logger.LogInformation("Ambient temp: {Temp}°C", _config.AmbientTemp);
        _logger.LogInformation("Random noise: {Noise:P}", _config.RandomNoise);
        _logger.LogInformation("=");

        await Task.Delay(5000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _iterationCount++;
                var startTime = DateTime.Now;
                
                _logger.LogInformation("--- Iteration {Iteration} starting at {Time}", 
                    _iterationCount, startTime.ToString("yyyy-MM-dd HH:mm:ss"));

                var devices = _dataGenerator.GenerateAllDeviceData();

                var sendTasks = devices.Select(d => _protocolSimulator.SendDeviceDataAsync(d));
                await Task.WhenAll(sendTasks);

                var elapsed = DateTime.Now - startTime;
                var totalPower = devices.Sum(d => d.CurrentPower);
                var avgCOP = devices
                    .Where(d => d.DeviceType == DeviceType.CentrifugalChiller || d.DeviceType == DeviceType.ScrewChiller)
                    .Average(d => d.COP);

                _logger.LogInformation("--- Iteration {Iteration} completed in {Elapsed:F1}ms", _iterationCount, elapsed.TotalMilliseconds);
                _logger.LogInformation("    Total power: {Power:F1}kW, Avg chiller COP: {COP:F2}", totalPower, avgCOP);

                var delay = TimeSpan.FromSeconds(_config.SendIntervalSeconds) - elapsed;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in simulator iteration {Iteration}", _iterationCount);
                await Task.Delay(TimeSpan.FromSeconds(_config.SendIntervalSeconds), stoppingToken);
            }
        }

        _logger.LogInformation("BACnet Device Simulator Stopped");
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping BACnet Device Simulator...");
        _protocolSimulator.Dispose();
        return base.StopAsync(cancellationToken);
    }
}
