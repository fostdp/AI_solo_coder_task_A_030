using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChillerPlant.Models;

namespace ChillerPlant.Data.Repositories
{
    public interface IDeviceRepository
    {
        Task<List<Device>> GetAllDevicesAsync();
        Task<List<Device>> GetDevicesByTypeAsync(int deviceTypeId);
        Task<Device> GetDeviceByIdAsync(int deviceId);
        Task<Device> GetDeviceByBacnetInstanceAsync(int bacnetInstance);
        Task<List<DeviceStatusDto>> GetDeviceStatusListAsync();
        Task<DeviceData> InsertDeviceDataAsync(BacnetDataDto data);
        Task<List<DeviceTrendDataDto>> GetDevice24HourTrendAsync(int deviceId);
        Task<List<PipeConnectionDto>> GetAllPipeConnectionsAsync();
        Task UpdateDeviceStatusAsync(int deviceId, int status);
    }
}
