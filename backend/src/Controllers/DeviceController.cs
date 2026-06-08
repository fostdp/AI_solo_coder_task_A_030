using Microsoft.AspNetCore.Mvc;
using ChillerPlantOptimization.DTOs;
using ChillerPlantOptimization.Models;
using ChillerPlantOptimization.Services;

namespace ChillerPlantOptimization.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeviceController : ControllerBase
{
    private readonly IDeviceDataService _deviceDataService;
    private readonly ILogger<DeviceController> _logger;

    public DeviceController(
        IDeviceDataService deviceDataService,
        ILogger<DeviceController> logger)
    {
        _deviceDataService = deviceDataService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeviceDto>>> GetAllDevices()
    {
        var devices = await _deviceDataService.GetAllDevicesAsync();
        var dtos = devices.Select(d => new DeviceDto
        {
            Id = d.Id,
            Name = d.Name,
            DeviceType = (int)d.DeviceTypeId,
            DeviceTypeName = d.DeviceTypeId.ToString(),
            DesignCOP = d.DesignCOP,
            RatedPower = d.RatedPower,
            Status = (int)d.Status,
            EfficiencyStatus = (int)d.EfficiencyStatus,
            CurrentCOP = d.CurrentCOP,
            PositionX = d.PositionX,
            PositionY = d.PositionY,
            OperatingHours = d.OperatingHours
        });

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DeviceDto>> GetDeviceById(string id)
    {
        var device = await _deviceDataService.GetDeviceByIdAsync(id);
        if (device == null)
        {
            return NotFound();
        }

        var dto = new DeviceDto
        {
            Id = device.Id,
            Name = device.Name,
            DeviceType = (int)device.DeviceTypeId,
            DeviceTypeName = device.DeviceTypeId.ToString(),
            DesignCOP = device.DesignCOP,
            RatedPower = device.RatedPower,
            Status = (int)device.Status,
            EfficiencyStatus = (int)device.EfficiencyStatus,
            CurrentCOP = device.CurrentCOP,
            PositionX = device.PositionX,
            PositionY = device.PositionY,
            OperatingHours = device.OperatingHours
        };

        return Ok(dto);
    }

    [HttpGet("type/{deviceType}")]
    public async Task<ActionResult<IEnumerable<DeviceDto>>> GetDevicesByType(int deviceType)
    {
        var devices = await _deviceDataService.GetDevicesByTypeAsync((DeviceType)deviceType);
        var dtos = devices.Select(d => new DeviceDto
        {
            Id = d.Id,
            Name = d.Name,
            DeviceType = (int)d.DeviceTypeId,
            DeviceTypeName = d.DeviceTypeId.ToString(),
            DesignCOP = d.DesignCOP,
            RatedPower = d.RatedPower,
            Status = (int)d.Status,
            EfficiencyStatus = (int)d.EfficiencyStatus,
            CurrentCOP = d.CurrentCOP,
            PositionX = d.PositionX,
            PositionY = d.PositionY,
            OperatingHours = d.OperatingHours
        });

        return Ok(dtos);
    }

    [HttpGet("{id}/realtime")]
    public async Task<ActionResult<DeviceRealtimeDataDto>> GetDeviceRealtimeData(string id)
    {
        var data = await _deviceDataService.GetLatestDeviceDataAsync(id);
        if (data == null)
        {
            return NotFound();
        }

        var dto = new DeviceRealtimeDataDto
        {
            DeviceId = data.DeviceId,
            Timestamp = data.Timestamp,
            Power = data.Power,
            SupplyTemperature = data.SupplyTemperature,
            ReturnTemperature = data.ReturnTemperature,
            Pressure = data.Pressure,
            FlowRate = data.FlowRate,
            Frequency = data.Frequency,
            Current = data.Current,
            Voltage = data.Voltage,
            InletTemperature = data.InletTemperature,
            OutletTemperature = data.OutletTemperature,
            FanSpeed = data.FanSpeed
        };

        return Ok(dto);
    }

    [HttpGet("{id}/trend")]
    public async Task<ActionResult<IEnumerable<DeviceTrendDataDto>>> GetDeviceTrendData(string id, [FromQuery] DateTime? startTime = null, [FromQuery] DateTime? endTime = null)
    {
        var actualStartTime = startTime ?? DateTime.UtcNow.AddHours(-24);
        var actualEndTime = endTime ?? DateTime.UtcNow;

        var data = await _deviceDataService.GetDeviceTrendDataAsync(id, actualStartTime, actualEndTime);
        var dataList = data.ToList();

        if (!dataList.Any())
        {
            return Ok(new List<DeviceTrendDataDto>());
        }

        var trendData = new List<DeviceTrendDataDto>
        {
            new()
            {
                DeviceId = id,
                ParameterName = "Power",
                DataPoints = dataList.Select(d => new TrendDataPointDto
                {
                    Timestamp = d.Timestamp,
                    Value = d.Power
                }).ToList()
            },
            new()
            {
                DeviceId = id,
                ParameterName = "SupplyTemperature",
                DataPoints = dataList.Select(d => new TrendDataPointDto
                {
                    Timestamp = d.Timestamp,
                    Value = d.SupplyTemperature
                }).ToList()
            },
            new()
            {
                DeviceId = id,
                ParameterName = "ReturnTemperature",
                DataPoints = dataList.Select(d => new TrendDataPointDto
                {
                    Timestamp = d.Timestamp,
                    Value = d.ReturnTemperature
                }).ToList()
            },
            new()
            {
                DeviceId = id,
                ParameterName = "Pressure",
                DataPoints = dataList.Select(d => new TrendDataPointDto
                {
                    Timestamp = d.Timestamp,
                    Value = d.Pressure
                }).ToList()
            },
            new()
            {
                DeviceId = id,
                ParameterName = "FlowRate",
                DataPoints = dataList.Select(d => new TrendDataPointDto
                {
                    Timestamp = d.Timestamp,
                    Value = d.FlowRate
                }).ToList()
            }
        };

        return Ok(trendData);
    }

    [HttpPost("{id}/data")]
    public async Task<ActionResult> PostDeviceData(string id, [FromBody] DeviceRealtimeDataDto data)
    {
        var deviceData = new DeviceData
        {
            DeviceId = id,
            Timestamp = data.Timestamp,
            Power = data.Power,
            SupplyTemperature = data.SupplyTemperature,
            ReturnTemperature = data.ReturnTemperature,
            Pressure = data.Pressure,
            FlowRate = data.FlowRate,
            Frequency = data.Frequency,
            Current = data.Current,
            Voltage = data.Voltage,
            InletTemperature = data.InletTemperature,
            OutletTemperature = data.OutletTemperature,
            FanSpeed = data.FanSpeed
        };

        await _deviceDataService.AddDeviceDataAsync(deviceData);
        return CreatedAtAction(nameof(GetDeviceRealtimeData), new { id }, data);
    }

    [HttpPut("{id}/status/{status}")]
    public async Task<ActionResult> UpdateDeviceStatus(string id, int status)
    {
        await _deviceDataService.UpdateDeviceStatusAsync(id, (DeviceStatus)status);
        return NoContent();
    }
}
