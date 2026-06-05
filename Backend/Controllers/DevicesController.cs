using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ChillerPlant.Data.Repositories;
using ChillerPlant.Models;
using ChillerPlant.Services;

namespace ChillerPlant.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DevicesController : ControllerBase
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly IEfficiencyRepository _efficiencyRepository;
        private readonly IHubContext<RealtimeHub> _hubContext;

        public DevicesController(IDeviceRepository deviceRepository, 
            IEfficiencyRepository efficiencyRepository,
            IHubContext<RealtimeHub> hubContext)
        {
            _deviceRepository = deviceRepository;
            _efficiencyRepository = efficiencyRepository;
            _hubContext = hubContext;
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
            var status = await _deviceRepository.GetDeviceStatusListAsync();
            return Ok(status);
        }

        [HttpGet("{id}/trend")]
        public async Task<ActionResult<List<DeviceTrendDataDto>>> GetDeviceTrend(int id)
        {
            var trend = await _deviceRepository.GetDevice24HourTrendAsync(id);
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

            var deviceData = await _deviceRepository.InsertDeviceDataAsync(data);
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

            var results = new List<object>();
            foreach (var data in dataList)
            {
                var deviceData = await _deviceRepository.InsertDeviceDataAsync(data);
                if (deviceData != null)
                {
                    results.Add(new { data.BacnetInstance, deviceData.DataId });
                }
            }

            return Ok(new { Success = true, Processed = results.Count, Results = results });
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<RealtimeDashboardDto>> GetDashboard()
        {
            var dashboard = await _efficiencyRepository.GetRealtimeDashboardAsync();
            return Ok(dashboard);
        }
    }
}
