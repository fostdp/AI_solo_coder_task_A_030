using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ChillerPlant.Data.Repositories;
using ChillerPlant.Models;
using ChillerPlant.Services;
using ChillerPlant.Modules.Shared.Commands;

namespace ChillerPlant.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DevicesController : ControllerBase
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly IHubContext<RealtimeHub> _hubContext;
        private readonly IMediator _mediator;

        public DevicesController(IDeviceRepository deviceRepository, 
            IHubContext<RealtimeHub> hubContext,
            IMediator mediator)
        {
            _deviceRepository = deviceRepository;
            _hubContext = hubContext;
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<List<Device>>> GetDevices()
        {
            var devices = await _deviceRepository.GetAllDevicesAsync();
            return Ok(devices);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Device>> GetDevice(int id)
        {
            var device = await _deviceRepository.GetDeviceByIdAsync(id);
            if (device == null) return NotFound();
            return Ok(device);
        }

        [HttpGet("type/{typeId}")]
        public async Task<ActionResult<List<Device>>> GetDevicesByType(int typeId)
        {
            var devices = await _deviceRepository.GetDevicesByTypeAsync(typeId);
            return Ok(devices);
        }

        [HttpGet("status")]
        public async Task<ActionResult<List<DeviceStatusDto>>> GetDeviceStatus()
        {
            var status = await _mediator.Send(new GetDeviceStatusCommand());
            return Ok(status);
        }

        [HttpGet("{id}/trend")]
        public async Task<ActionResult<List<DeviceTrendDataDto>>> GetDeviceTrend(int id, [FromQuery] int hours = 24)
        {
            var trend = await _mediator.Send(new GetDeviceTrendDataCommand { DeviceId = id, Hours = hours });
            return Ok(trend);
        }

        [HttpGet("pipes")]
        public async Task<ActionResult<List<PipeConnectionDto>>> GetPipeConnections()
        {
            var connections = await _deviceRepository.GetAllPipeConnectionsAsync();
            return Ok(connections);
        }

        [HttpPost("data")]
        public async Task<ActionResult> ReceiveDeviceData([FromBody] BacnetDataDto data)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var command = new InsertDeviceDataCommand
            {
                BacnetInstance = data.BacnetInstance,
                Power = data.Power,
                SupplyWaterTemp = data.SupplyWaterTemp,
                ReturnWaterTemp = data.ReturnWaterTemp,
                CoolingWaterInTemp = data.CoolingWaterInTemp,
                CoolingWaterOutTemp = data.CoolingWaterOutTemp,
                FlowRate = data.FlowRate,
                SupplyPressure = data.SupplyPressure,
                ReturnPressure = data.ReturnPressure,
                LoadRate = data.LoadRate,
                Frequency = data.Frequency,
                Vibration = data.Vibration,
                Current = data.Current,
                Voltage = data.Voltage,
                RunningHours = data.RunningHours,
                Status = data.Status,
                Timestamp = data.Timestamp
            };

            var deviceData = await _mediator.Send(command);
            if (deviceData == null) return NotFound($"Device not found for BACnet instance {data.BacnetInstance}");

            await _hubContext.Clients.All.SendAsync("DeviceDataUpdated", new
            {
                deviceData.DeviceId,
                deviceData.Power,
                deviceData.COP,
                deviceData.LoadRate,
                deviceData.Timestamp
            });

            return Ok(new { Success = true, DataId = deviceData.DataId });
        }

        [HttpPost("data/batch")]
        public async Task<ActionResult> ReceiveBatchData([FromBody] List<BacnetDataDto> dataList)
        {
            if (!ModelState.IsValid || dataList == null || !dataList.Any()) 
                return BadRequest(ModelState);

            var batchCommand = new InsertBatchDeviceDataCommand();
            foreach (var data in dataList)
            {
                batchCommand.DataList.Add(new InsertDeviceDataCommand
                {
                    BacnetInstance = data.BacnetInstance,
                    Power = data.Power,
                    SupplyWaterTemp = data.SupplyWaterTemp,
                    ReturnWaterTemp = data.ReturnWaterTemp,
                    CoolingWaterInTemp = data.CoolingWaterInTemp,
                    CoolingWaterOutTemp = data.CoolingWaterOutTemp,
                    FlowRate = data.FlowRate,
                    SupplyPressure = data.SupplyPressure,
                    ReturnPressure = data.ReturnPressure,
                    LoadRate = data.LoadRate,
                    Frequency = data.Frequency,
                    Vibration = data.Vibration,
                    Current = data.Current,
                    Voltage = data.Voltage,
                    RunningHours = data.RunningHours,
                    Status = data.Status,
                    Timestamp = data.Timestamp
                });
            }

            var processed = await _mediator.Send(batchCommand);
            return Ok(new { Success = true, Processed = processed });
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<RealtimeDashboardDto>> GetDashboard()
        {
            var dashboard = await _mediator.Send(new GetRealtimeDashboardCommand());
            return Ok(dashboard);
        }
    }
}
