using BacnetSimulator.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BacnetSimulator.Services;

public class DeviceDataGenerator
{
    private readonly ILogger<DeviceDataGenerator> _logger;
    private readonly SimulatorConfig _config;
    private readonly Random _random = new();
    private readonly List<SimulatedDevice> _devices = new();
    private DateTime _startTime = DateTime.Now;

    public DeviceDataGenerator(
        ILogger<DeviceDataGenerator> logger,
        IOptions<SimulatorConfig> config)
    {
        _logger = logger;
        _config = config.Value;
        InitializeDevices();
    }

    private void InitializeDevices()
    {
        _logger.LogInformation("Initializing simulated devices...");
        
        var instance = _config.StartInstance;
        
        InitializeDefaultPerformanceCurves();
        
        for (int i = 0; i < _config.CentrifugalCount; i++)
        {
            _devices.Add(CreateDevice(instance++, DeviceType.CentrifugalChiller, $"离心机-{i + 1:00}"));
        }
        
        for (int i = 0; i < _config.ScrewCount; i++)
        {
            _devices.Add(CreateDevice(instance++, DeviceType.ScrewChiller, $"螺杆机-{i + 1:00}"));
        }
        
        for (int i = 0; i < _config.CoolingTowerCount; i++)
        {
            _devices.Add(CreateDevice(instance++, DeviceType.CoolingTower, $"冷却塔-{i + 1:00}"));
        }
        
        for (int i = 0; i < _config.ChilledPumpCount; i++)
        {
            _devices.Add(CreateDevice(instance++, DeviceType.ChilledPump, $"冷冻水泵-{i + 1:00}"));
        }
        
        for (int i = 0; i < _config.CoolingPumpCount; i++)
        {
            _devices.Add(CreateDevice(instance++, DeviceType.CoolingPump, $"冷却水泵-{i + 1:00}"));
        }
        
        _logger.LogInformation("Initialized {Count} simulated devices", _devices.Count);
        foreach (var device in _devices)
        {
            _logger.LogInformation("  - {Instance}: {Name} ({Type})", 
                device.BacnetInstance, device.DeviceName, device.DeviceType);
        }
    }

    private void InitializeDefaultPerformanceCurves()
    {
        if (_config.PerformanceCurves.Count == 0)
        {
            _config.PerformanceCurves["centrifugal"] = new DevicePerformanceCurve
            {
                A = -2.5,
                B = 3.5,
                C = 3.0,
                RatedPower = 800,
                DesignCOP = 5.8
            };
            
            _config.PerformanceCurves["screw"] = new DevicePerformanceCurve
            {
                A = -1.8,
                B = 2.8,
                C = 2.5,
                RatedPower = 500,
                DesignCOP = 5.2
            };
            
            _config.PerformanceCurves["cooling_tower"] = new DevicePerformanceCurve
            {
                A = -0.5,
                B = 1.2,
                C = 3.0,
                RatedPower = 30,
                DesignCOP = 4.5
            };
            
            _config.PerformanceCurves["chilled_pump"] = new DevicePerformanceCurve
            {
                A = 0,
                B = 0.85,
                C = 0,
                RatedPower = 75,
                Efficiency = 0.85
            };
            
            _config.PerformanceCurves["cooling_pump"] = new DevicePerformanceCurve
            {
                A = 0,
                B = 0.85,
                C = 0,
                RatedPower = 45,
                Efficiency = 0.85
            };
        }
    }

    private SimulatedDevice CreateDevice(int instance, DeviceType type, string name)
    {
        var curveKey = type switch
        {
            DeviceType.CentrifugalChiller => "centrifugal",
            DeviceType.ScrewChiller => "screw",
            DeviceType.CoolingTower => "cooling_tower",
            DeviceType.ChilledPump => "chilled_pump",
            DeviceType.CoolingPump => "cooling_pump",
            _ => "centrifugal"
        };
        
        var curve = _config.PerformanceCurves.TryGetValue(curveKey, out var c) ? c : new DevicePerformanceCurve();
        
        return new SimulatedDevice
        {
            BacnetInstance = instance,
            DeviceType = type,
            DeviceName = name,
            PerformanceCurve = curve,
            RunningHours = _random.Next(1000, 10000),
            Voltage = 380
        };
    }

    public List<SimulatedDevice> GenerateAllDeviceData()
    {
        var timeFactor = CalculateTimeFactor();
        var dynamicLoad = _config.LoadFactor * timeFactor;
        
        foreach (var device in _devices)
        {
            GenerateDeviceData(device, dynamicLoad);
        }
        
        return _devices.ToList();
    }

    private double CalculateTimeFactor()
    {
        var now = DateTime.Now;
        var hour = now.Hour + now.Minute / 60.0;
        
        double timeFactor;
        if (hour >= 8 && hour <= 18)
        {
            timeFactor = 0.8 + 0.2 * Math.Sin((hour - 8) / 10 * Math.PI);
        }
        else if (hour >= 18 && hour <= 22)
        {
            timeFactor = 0.9 - 0.4 * (hour - 18) / 4;
        }
        else
        {
            timeFactor = 0.4 + 0.1 * Math.Sin((hour + 2) / 8 * Math.PI);
        }
        
        return Math.Clamp(timeFactor, 0.3, 1.0);
    }

    private void GenerateDeviceData(SimulatedDevice device, double systemLoad)
    {
        var now = DateTime.Now;
        device.Timestamp = now;
        
        var noise = 1 + (_random.NextDouble() * 2 - 1) * _config.RandomNoise;
        var load = systemLoad * noise;
        device.LoadRate = Math.Clamp(load * 100, 20, 100);
        
        var curve = device.PerformanceCurve;
        
        switch (device.DeviceType)
        {
            case DeviceType.CentrifugalChiller:
            case DeviceType.ScrewChiller:
                GenerateChillerData(device, curve, load, noise);
                break;
            case DeviceType.CoolingTower:
                GenerateCoolingTowerData(device, curve, load, noise);
                break;
            case DeviceType.ChilledPump:
            case DeviceType.CoolingPump:
                GeneratePumpData(device, curve, load, noise);
                break;
        }
        
        device.RunningHours += (long)(_config.SendIntervalSeconds / 3600.0 * 10) / 10.0;
    }

    private void GenerateChillerData(SimulatedDevice device, DevicePerformanceCurve curve, double load, double noise)
    {
        var loadFactor = load / 100.0;
        
        device.CurrentLoad = loadFactor * curve.RatedPower;
        device.CurrentPower = device.CurrentLoad / (curve.Efficiency * noise);
        device.COP = CalculateCOP(curve, loadFactor) * noise;
        
        var tempNoise = (_random.NextDouble() * 2 - 1) * 0.5;
        device.SupplyWaterTemp = 7 + tempNoise + (1 - loadFactor) * 0.5;
        device.ReturnWaterTemp = 12 + tempNoise + loadFactor * 1.5;
        device.CoolingWaterInTemp = _config.AmbientTemp + 1 + tempNoise;
        device.CoolingWaterOutTemp = _config.WetBulbTemp + 4 + tempNoise + loadFactor * 2;
        
        device.FlowRate = 80 + loadFactor * 40 + (_random.NextDouble() * 2 - 1) * 2;
        device.SupplyPressure = 0.6 + loadFactor * 0.2 + (_random.NextDouble() * 2 - 1) * 0.02;
        device.ReturnPressure = 0.3 + loadFactor * 0.1 + (_random.NextDouble() * 2 - 1) * 0.02;
        
        device.Frequency = 50 + (_random.NextDouble() * 2 - 1) * 0.2;
        device.Vibration = 0.5 + loadFactor * 1.5 + (_random.NextDouble() * 2 - 1) * 0.1;
        device.Current = device.CurrentPower / Math.Sqrt(3) / device.Voltage / 0.85 * 1000;
    }

    private void GenerateCoolingTowerData(SimulatedDevice device, DevicePerformanceCurve curve, double load, double noise)
    {
        var loadFactor = load / 100.0;
        
        device.CurrentLoad = loadFactor * curve.RatedPower;
        device.CurrentPower = device.CurrentLoad / (curve.Efficiency * noise);
        device.COP = CalculateCOP(curve, loadFactor) * noise;
        
        var tempNoise = (_random.NextDouble() * 2 - 1) * 0.3;
        device.CoolingWaterInTemp = _config.AmbientTemp + 5 + tempNoise;
        device.CoolingWaterOutTemp = _config.WetBulbTemp + 2 + tempNoise;
        
        device.FlowRate = 200 + loadFactor * 100 + (_random.NextDouble() * 2 - 1) * 5;
        device.Frequency = 50 + (_random.NextDouble() * 2 - 1) * 0.2;
        device.Vibration = 0.3 + loadFactor * 0.7 + (_random.NextDouble() * 2 - 1) * 0.05;
        device.Current = device.CurrentPower / Math.Sqrt(3) / device.Voltage / 0.85 * 1000;
    }

    private void GeneratePumpData(SimulatedDevice device, DevicePerformanceCurve curve, double load, double noise)
    {
        var loadFactor = load / 100.0;
        
        device.CurrentLoad = loadFactor * curve.RatedPower;
        device.CurrentPower = device.CurrentLoad / (curve.Efficiency * noise);
        device.COP = curve.Efficiency;
        
        var pressureNoise = (_random.NextDouble() * 2 - 1) * 0.01;
        device.SupplyPressure = 0.5 + loadFactor * 0.3 + pressureNoise;
        device.ReturnPressure = 0.2 + loadFactor * 0.15 + pressureNoise;
        
        device.FlowRate = device.DeviceType == DeviceType.ChilledPump 
            ? 100 + loadFactor * 50 + (_random.NextDouble() * 2 - 1) * 3
            : 80 + loadFactor * 40 + (_random.NextDouble() * 2 - 1) * 2;
        
        device.Frequency = 30 + loadFactor * 20 + (_random.NextDouble() * 2 - 1) * 0.5;
        device.Vibration = 0.2 + loadFactor * 0.5 + (_random.NextDouble() * 2 - 1) * 0.03;
        device.Current = device.CurrentPower / Math.Sqrt(3) / device.Voltage / 0.85 * 1000;
    }

    private static double CalculateCOP(DevicePerformanceCurve curve, double loadFactor)
    {
        if (loadFactor <= 0) return 0;
        
        var cop = curve.A * loadFactor * loadFactor + 
                  curve.B * loadFactor + 
                  curve.C;
        
        return Math.Clamp(cop, 2.0, curve.DesignCOP * 1.1);
    }

    public List<SimulatedDevice> GetAllDevices() => _devices.ToList();
}
