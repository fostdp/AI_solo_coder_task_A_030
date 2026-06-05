using System.Text.Json;
using BacnetSimulator.Models;
using BacnetSimulator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureAppConfiguration((context, config) =>
{
    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
          .AddEnvironmentVariables("SIMULATOR_")
          .AddEnvironmentVariables()
          .AddCommandLine(args);
});

builder.ConfigureServices((context, services) =>
{
    var config = new SimulatorConfig();
    context.Configuration.GetSection("Simulator").Bind(config);
    context.Configuration.Bind(config);

    var performanceCurvesJson = Environment.GetEnvironmentVariable("SIMULATOR_PERFORMANCE_CURVES");
    if (!string.IsNullOrEmpty(performanceCurvesJson))
    {
        try
        {
            var curves = JsonSerializer.Deserialize<Dictionary<string, DevicePerformanceCurve>>(
                performanceCurvesJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

            if (curves != null && curves.Count > 0)
            {
                config.PerformanceCurves = curves;
                Console.WriteLine($"Loaded {curves.Count} custom performance curves from environment variable");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to parse SIMULATOR_PERFORMANCE_CURVES: {ex.Message}");
            Console.WriteLine("Using default performance curves");
        }
    }

    services.Configure<SimulatorConfig>(options =>
    {
        options.TargetAddress = config.TargetAddress;
        options.TargetPort = config.TargetPort;
        options.SendIntervalSeconds = config.SendIntervalSeconds;
        options.StartInstance = config.StartInstance;
        options.CentrifugalCount = config.CentrifugalCount;
        options.ScrewCount = config.ScrewCount;
        options.CoolingTowerCount = config.CoolingTowerCount;
        options.ChilledPumpCount = config.ChilledPumpCount;
        options.CoolingPumpCount = config.CoolingPumpCount;
        options.LoadFactor = config.LoadFactor;
        options.AmbientTemp = config.AmbientTemp;
        options.WetBulbTemp = config.WetBulbTemp;
        options.RandomNoise = config.RandomNoise;
        options.PerformanceCurves = config.PerformanceCurves;
    });
    
    services.AddSingleton<DeviceDataGenerator>();
    services.AddSingleton<BacnetProtocolSimulator>();
    services.AddHostedService<SimulatorBackgroundService>();
});

var host = builder.Build();

Console.WriteLine("========================================");
Console.WriteLine("  BACnet Device Simulator");
Console.WriteLine("========================================");
Console.WriteLine("Press Ctrl+C to stop...");
Console.WriteLine("");

await host.RunAsync();
