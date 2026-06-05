using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Dapper;
using ChillerPlant.Models;

namespace ChillerPlant.Data.Repositories
{
    public class DeviceRepository : IDeviceRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;

        public DeviceRepository(ApplicationDbContext context, string connectionString)
        {
            _context = context;
            _connectionString = connectionString;
        }

        public async Task<List<Device>> GetAllDevicesAsync()
        {
            return await _context.Devices
                .Include(d => d.DeviceType)
                .OrderBy(d => d.DeviceId)
                .ToListAsync();
        }

        public async Task<List<Device>> GetDevicesByTypeAsync(int deviceTypeId)
        {
            return await _context.Devices
                .Include(d => d.DeviceType)
                .Where(d => d.DeviceTypeId == deviceTypeId)
                .OrderBy(d => d.DeviceId)
                .ToListAsync();
        }

        public async Task<Device> GetDeviceByIdAsync(int deviceId)
        {
            return await _context.Devices
                .Include(d => d.DeviceType)
                .FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        }

        public async Task<Device> GetDeviceByBacnetInstanceAsync(int bacnetInstance)
        {
            return await _context.Devices
                .FirstOrDefaultAsync(d => d.BacnetInstance == bacnetInstance);
        }

        public async Task<List<DeviceStatusDto>> GetDeviceStatusListAsync()
        {
            var devices = await _context.Devices
                .Include(d => d.DeviceType)
                .ToListAsync();
            
            var latestData = await _context.DeviceData
                .GroupBy(d => d.DeviceId)
                .Select(g => new
                {
                    DeviceId = g.Key,
                    LatestData = g.OrderByDescending(d => d.Timestamp).FirstOrDefault()
                })
                .ToDictionaryAsync(d => d.DeviceId, d => d.LatestData);

            var result = new List<DeviceStatusDto>();
            foreach (var device in devices)
            {
                var data = latestData.ContainsKey(device.DeviceId) ? latestData[device.DeviceId] : null;
                var efficiencyStatus = GetEfficiencyStatus(data?.COP, device.DesignCOP);
                
                result.Add(new DeviceStatusDto
                {
                    DeviceId = device.DeviceId,
                    DeviceName = device.DeviceName,
                    DeviceCode = device.DeviceCode,
                    DeviceTypeId = device.DeviceTypeId,
                    TypeName = device.DeviceType?.TypeName,
                    Status = device.Status,
                    CurrentPower = data?.Power,
                    CurrentLoadRate = data?.LoadRate,
                    CurrentCOP = data?.COP,
                    DesignCOP = device.DesignCOP,
                    EfficiencyStatus = efficiencyStatus.Status,
                    StatusColor = efficiencyStatus.Color,
                    X = device.X,
                    Y = device.Y,
                    LastUpdateTime = data?.Timestamp
                });
            }

            return result;
        }

        private (string Status, string Color) GetEfficiencyStatus(decimal? cop, decimal designCop)
        {
            if (!cop.HasValue) return ("未知", "#95a5a6");
            
            var ratio = cop.Value / designCop;
            if (ratio >= 0.9m) return ("高效", "#27ae60");
            if (ratio >= 0.7m) return ("正常", "#2ecc71");
            if (ratio >= 0.5m) return ("效率偏低", "#f39c12");
            return ("低效", "#e74c3c");
        }

        public async Task<DeviceData> InsertDeviceDataAsync(BacnetDataDto data)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var parameters = new
                {
                    DeviceId = 0,
                    data.Power,
                    data.SupplyWaterTemp,
                    data.ReturnWaterTemp,
                    data.CoolingWaterInTemp,
                    data.CoolingWaterOutTemp,
                    data.FlowRate,
                    data.SupplyPressure,
                    data.ReturnPressure,
                    data.LoadRate,
                    data.Frequency,
                    data.Vibration,
                    data.Current,
                    data.Voltage,
                    data.RunningHours,
                    data.Status,
                    COP = CalculateCOP(data)
                };

                var device = await GetDeviceByBacnetInstanceAsync(data.BacnetInstance);
                if (device == null) return null;

                var deviceData = new DeviceData
                {
                    DeviceId = device.DeviceId,
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
                    COP = CalculateCOP(data),
                    Timestamp = data.Timestamp
                };

                _context.DeviceData.Add(deviceData);
                await _context.SaveChangesAsync();

                device.Status = data.Status;
                device.UpdatedAt = DateTime.Now;
                _context.Update(device);
                await _context.SaveChangesAsync();

                return deviceData;
            }
        }

        private decimal? CalculateCOP(BacnetDataDto data)
        {
            if (data.Power <= 0 || !data.FlowRate.HasValue || 
                !data.SupplyWaterTemp.HasValue || !data.ReturnWaterTemp.HasValue)
                return null;

            var deltaT = Math.Abs(data.ReturnWaterTemp.Value - data.SupplyWaterTemp.Value);
            if (deltaT <= 0) return null;

            var coolingCapacity = data.FlowRate.Value * deltaT * 1.163m;
            return coolingCapacity / data.Power;
        }

        public async Task<List<DeviceTrendDataDto>> GetDevice24HourTrendAsync(int deviceId)
        {
            var startTime = DateTime.Now.AddHours(-24);
            return await _context.DeviceData
                .Where(d => d.DeviceId == deviceId && d.Timestamp >= startTime)
                .OrderBy(d => d.Timestamp)
                .Select(d => new DeviceTrendDataDto
                {
                    Timestamp = d.Timestamp,
                    Power = d.Power,
                    SupplyWaterTemp = d.SupplyWaterTemp,
                    ReturnWaterTemp = d.ReturnWaterTemp,
                    CoolingWaterInTemp = d.CoolingWaterInTemp,
                    CoolingWaterOutTemp = d.CoolingWaterOutTemp,
                    FlowRate = d.FlowRate,
                    LoadRate = d.LoadRate,
                    COP = d.COP
                })
                .ToListAsync();
        }

        public async Task<List<PipeConnectionDto>> GetAllPipeConnectionsAsync()
        {
            return await _context.PipeConnections
                .Include(p => p.FromDevice)
                .Include(p => p.ToDevice)
                .Select(p => new PipeConnectionDto
                {
                    ConnectionId = p.ConnectionId,
                    FromDeviceId = p.FromDeviceId,
                    ToDeviceId = p.ToDeviceId,
                    PipeType = p.PipeType,
                    Color = p.Color,
                    FlowDirection = p.FlowDirection,
                    FromX = p.FromDevice.X,
                    FromY = p.FromDevice.Y,
                    ToX = p.ToDevice.X,
                    ToY = p.ToDevice.Y
                })
                .ToListAsync();
        }

        public async Task UpdateDeviceStatusAsync(int deviceId, int status)
        {
            var device = await _context.Devices.FindAsync(deviceId);
            if (device != null)
            {
                device.Status = status;
                device.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }
    }
}
