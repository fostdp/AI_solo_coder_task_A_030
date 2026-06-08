using System.Net;
using System.Net.Sockets;
using System.Text;
using ChillerPlantOptimization.Models;
using ChillerPlantOptimization.Repositories;

namespace ChillerPlantOptimization.Services;

public interface IBACnetDataCollectionService
{
    Task StartCollectionAsync(CancellationToken cancellationToken);
    Task<DeviceData?> ReadDeviceDataAsync(string deviceId);
}

public class BACnetDataPoint
{
    public int ObjectType { get; set; }
    public int ObjectInstance { get; set; }
    public int PropertyId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class BACnetDeviceConfig
{
    public string DeviceId { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 47808;
    public int InstanceNumber { get; set; }
}

public class BACnetDataCollectionService : IBACnetDataCollectionService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly ITimeSeriesRepository _timeSeriesRepository;
    private readonly IDeviceDataService _deviceDataService;
    private readonly ILogger<BACnetDataCollectionService> _logger;

    private readonly Dictionary<string, BACnetDataPoint[]> _dataPoints = new()
    {
        ["centrifugal_chiller"] = new[]
        {
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 0, PropertyId = 85, Name = "Power" },
            new BACnetDataPoint { ObjectType = 0, ObjectInstance = 0, PropertyId = 85, Name = "SupplyTemperature" },
            new BACnetDataPoint { ObjectType = 0, ObjectInstance = 1, PropertyId = 85, Name = "ReturnTemperature" },
            new BACnetDataPoint { ObjectType = 2, ObjectInstance = 0, PropertyId = 85, Name = "Pressure" },
            new BACnetDataPoint { ObjectType = 1, ObjectInstance = 0, PropertyId = 85, Name = "FlowRate" },
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 1, PropertyId = 85, Name = "Current" },
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 2, PropertyId = 85, Name = "Voltage" },
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 3, PropertyId = 85, Name = "Frequency" }
        },
        ["screw_chiller"] = new[]
        {
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 0, PropertyId = 85, Name = "Power" },
            new BACnetDataPoint { ObjectType = 0, ObjectInstance = 0, PropertyId = 85, Name = "SupplyTemperature" },
            new BACnetDataPoint { ObjectType = 0, ObjectInstance = 1, PropertyId = 85, Name = "ReturnTemperature" },
            new BACnetDataPoint { ObjectType = 2, ObjectInstance = 0, PropertyId = 85, Name = "Pressure" },
            new BACnetDataPoint { ObjectType = 1, ObjectInstance = 0, PropertyId = 85, Name = "FlowRate" },
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 1, PropertyId = 85, Name = "Current" },
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 2, PropertyId = 85, Name = "Voltage" },
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 3, PropertyId = 85, Name = "Frequency" }
        },
        ["cooling_tower"] = new[]
        {
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 0, PropertyId = 85, Name = "Power" },
            new BACnetDataPoint { ObjectType = 0, ObjectInstance = 0, PropertyId = 85, Name = "InletTemperature" },
            new BACnetDataPoint { ObjectType = 0, ObjectInstance = 1, PropertyId = 85, Name = "OutletTemperature" },
            new BACnetDataPoint { ObjectType = 1, ObjectInstance = 0, PropertyId = 85, Name = "FlowRate" },
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 1, PropertyId = 85, Name = "FanSpeed" },
            new BACnetDataPoint { ObjectType = 2, ObjectInstance = 0, PropertyId = 85, Name = "Pressure" }
        },
        ["chilled_water_pump"] = new[]
        {
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 0, PropertyId = 85, Name = "Power" },
            new BACnetDataPoint { ObjectType = 0, ObjectInstance = 0, PropertyId = 85, Name = "SupplyTemperature" },
            new BACnetDataPoint { ObjectType = 0, ObjectInstance = 1, PropertyId = 85, Name = "ReturnTemperature" },
            new BACnetDataPoint { ObjectType = 2, ObjectInstance = 0, PropertyId = 85, Name = "Pressure" },
            new BACnetDataPoint { ObjectType = 1, ObjectInstance = 0, PropertyId = 85, Name = "FlowRate" },
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 1, PropertyId = 85, Name = "Frequency" },
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 2, PropertyId = 85, Name = "Current" },
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 3, PropertyId = 85, Name = "Voltage" }
        },
        ["cooling_water_pump"] = new[]
        {
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 0, PropertyId = 85, Name = "Power" },
            new BACnetDataPoint { ObjectType = 0, ObjectInstance = 0, PropertyId = 85, Name = "SupplyTemperature" },
            new BACnetDataPoint { ObjectType = 0, ObjectInstance = 1, PropertyId = 85, Name = "ReturnTemperature" },
            new BACnetDataPoint { ObjectType = 2, ObjectInstance = 0, PropertyId = 85, Name = "Pressure" },
            new BACnetDataPoint { ObjectType = 1, ObjectInstance = 0, PropertyId = 85, Name = "FlowRate" },
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 1, PropertyId = 85, Name = "Frequency" },
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 2, PropertyId = 85, Name = "Current" },
            new BACnetDataPoint { ObjectType = 4, ObjectInstance = 3, PropertyId = 85, Name = "Voltage" }
        }
    };

    private readonly Random _random = new Random(42);

    public BACnetDataCollectionService(
        IDeviceRepository deviceRepository,
        ITimeSeriesRepository timeSeriesRepository,
        IDeviceDataService deviceDataService,
        ILogger<BACnetDataCollectionService> logger)
    {
        _deviceRepository = deviceRepository;
        _timeSeriesRepository = timeSeriesRepository;
        _deviceDataService = deviceDataService;
        _logger = logger;
    }

    public async Task StartCollectionAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BACnet数据采集服务已启动");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var devices = await _deviceRepository.GetAllAsync();
                var deviceList = devices.ToList();
                var allDeviceData = new List<DeviceData>();
                var timestamp = DateTime.UtcNow;

                foreach (var device in deviceList)
                {
                    if (device.Status == DeviceStatus.Fault)
                    {
                        continue;
                    }

                    var data = await ReadDeviceDataInternalAsync(device, timestamp);
                    if (data != null)
                    {
                        allDeviceData.Add(data);
                    }
                }

                if (allDeviceData.Any())
                {
                    await _deviceDataService.AddRangeDeviceDataAsync(allDeviceData);
                    _logger.LogInformation("已采集 {Count} 台设备数据", allDeviceData.Count);
                }

                await Task.Delay(30000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BACnet数据采集发生错误");
                await Task.Delay(5000, cancellationToken);
            }
        }

        _logger.LogInformation("BACnet数据采集服务已停止");
    }

    public async Task<DeviceData?> ReadDeviceDataAsync(string deviceId)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null) return null;

        return await ReadDeviceDataInternalAsync(device, DateTime.UtcNow);
    }

    private async Task<DeviceData?> ReadDeviceDataInternalAsync(Device device, DateTime timestamp)
    {
        try
        {
            var deviceTypeKey = GetDeviceTypeKey(device.DeviceTypeId);
            if (!_dataPoints.ContainsKey(deviceTypeKey)) return null;

            var dataPoints = _dataPoints[deviceTypeKey];
            var data = new Dictionary<string, decimal>();

            foreach (var point in dataPoints)
            {
                var value = await SimulateBACnetReadAsync(device, point);
                data[point.Name] = value;
            }

            var deviceData = new DeviceData
            {
                DeviceId = device.Id,
                Timestamp = timestamp,
                Power = data.GetValueOrDefault("Power", 0),
                SupplyTemperature = data.GetValueOrDefault("SupplyTemperature", data.GetValueOrDefault("OutletTemperature", 0)),
                ReturnTemperature = data.GetValueOrDefault("ReturnTemperature", data.GetValueOrDefault("InletTemperature", 0)),
                Pressure = data.GetValueOrDefault("Pressure", 0),
                FlowRate = data.GetValueOrDefault("FlowRate", 0),
                Frequency = data.GetValueOrDefault("Frequency", 50),
                Current = data.GetValueOrDefault("Current", 0),
                Voltage = data.GetValueOrDefault("Voltage", 380),
                InletTemperature = data.GetValueOrDefault("InletTemperature", null),
                OutletTemperature = data.GetValueOrDefault("OutletTemperature", null),
                FanSpeed = data.GetValueOrDefault("FanSpeed", null)
            };

            return deviceData;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取设备 {DeviceId} 数据失败", device.Id);
            return null;
        }
    }

    private async Task<decimal> SimulateBACnetReadAsync(Device device, BACnetDataPoint point)
    {
        await Task.Delay(1);

        var isRunning = device.Status == DeviceStatus.Running;

        var baseValue = GetBaseValue(device.DeviceTypeId, point.Name);
        var variation = (decimal)(_random.NextDouble() * 0.1 - 0.05) * baseValue;

        return isRunning ? Math.Max(0, baseValue + variation) : 0;
    }

    private decimal GetBaseValue(DeviceType deviceType, string parameterName)
    {
        var baseValues = new Dictionary<(DeviceType, string), decimal>
        {
            [(DeviceType.CentrifugalChiller, "Power")] = 850,
            [(DeviceType.CentrifugalChiller, "SupplyTemperature")] = 7.0m,
            [(DeviceType.CentrifugalChiller, "ReturnTemperature")] = 14.0m,
            [(DeviceType.CentrifugalChiller, "Pressure")] = 1.2m,
            [(DeviceType.CentrifugalChiller, "FlowRate")] = 900,
            [(DeviceType.CentrifugalChiller, "Current")] = 150,
            [(DeviceType.CentrifugalChiller, "Voltage")] = 380,
            [(DeviceType.CentrifugalChiller, "Frequency")] = 48,

            [(DeviceType.ScrewChiller, "Power")] = 520,
            [(DeviceType.ScrewChiller, "SupplyTemperature")] = 7.2m,
            [(DeviceType.ScrewChiller, "ReturnTemperature")] = 13.8m,
            [(DeviceType.ScrewChiller, "Pressure")] = 1.15m,
            [(DeviceType.ScrewChiller, "FlowRate")] = 450,
            [(DeviceType.ScrewChiller, "Current")] = 95,
            [(DeviceType.ScrewChiller, "Voltage")] = 380,
            [(DeviceType.ScrewChiller, "Frequency")] = 47,

            [(DeviceType.CoolingTower, "Power")] = 18,
            [(DeviceType.CoolingTower, "InletTemperature")] = 32.0m,
            [(DeviceType.CoolingTower, "OutletTemperature")] = 28.0m,
            [(DeviceType.CoolingTower, "FlowRate")] = 900,
            [(DeviceType.CoolingTower, "FanSpeed")] = 1200,
            [(DeviceType.CoolingTower, "Pressure")] = 0.3m,

            [(DeviceType.ChilledWaterPump, "Power")] = 75,
            [(DeviceType.ChilledWaterPump, "SupplyTemperature")] = 7.0m,
            [(DeviceType.ChilledWaterPump, "ReturnTemperature")] = 14.0m,
            [(DeviceType.ChilledWaterPump, "Pressure")] = 1.5m,
            [(DeviceType.ChilledWaterPump, "FlowRate")] = 450,
            [(DeviceType.ChilledWaterPump, "Frequency")] = 45,
            [(DeviceType.ChilledWaterPump, "Current")] = 135,
            [(DeviceType.ChilledWaterPump, "Voltage")] = 380,

            [(DeviceType.CoolingWaterPump, "Power")] = 78,
            [(DeviceType.CoolingWaterPump, "SupplyTemperature")] = 28.0m,
            [(DeviceType.CoolingWaterPump, "ReturnTemperature")] = 33.0m,
            [(DeviceType.CoolingWaterPump, "Pressure")] = 1.4m,
            [(DeviceType.CoolingWaterPump, "FlowRate")] = 500,
            [(DeviceType.CoolingWaterPump, "Frequency")] = 46,
            [(DeviceType.CoolingWaterPump, "Current")] = 140,
            [(DeviceType.CoolingWaterPump, "Voltage")] = 380,
        };

        return baseValues.TryGetValue((deviceType, parameterName), out var value) ? value : 0;
    }

    private string GetDeviceTypeKey(DeviceType deviceType)
    {
        return deviceType switch
        {
            DeviceType.CentrifugalChiller => "centrifugal_chiller",
            DeviceType.ScrewChiller => "screw_chiller",
            DeviceType.CoolingTower => "cooling_tower",
            DeviceType.ChilledWaterPump => "chilled_water_pump",
            DeviceType.CoolingWaterPump => "cooling_water_pump",
            _ => "unknown"
        };
    }
}
