using ChillerPlantOptimization.Models;
using ChillerPlantOptimization.Repositories;

namespace ChillerPlantOptimization.Services;

public interface IDeviceDataService
{
    Task<IEnumerable<Device>> GetAllDevicesAsync();
    Task<Device?> GetDeviceByIdAsync(string id);
    Task<IEnumerable<Device>> GetDevicesByTypeAsync(DeviceType type);
    Task<DeviceData?> GetLatestDeviceDataAsync(string deviceId);
    Task<IEnumerable<DeviceData>> GetDeviceTrendDataAsync(string deviceId, DateTime startTime, DateTime endTime);
    Task AddDeviceDataAsync(DeviceData data);
    Task AddRangeDeviceDataAsync(IEnumerable<DeviceData> data);
    Task UpdateDeviceStatusAsync(string deviceId, DeviceStatus status);
}

public class DeviceDataService : IDeviceDataService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly ITimeSeriesRepository _timeSeriesRepository;
    private readonly ILogger<DeviceDataService> _logger;

    public DeviceDataService(
        IDeviceRepository deviceRepository,
        ITimeSeriesRepository timeSeriesRepository,
        ILogger<DeviceDataService> logger)
    {
        _deviceRepository = deviceRepository;
        _timeSeriesRepository = timeSeriesRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<Device>> GetAllDevicesAsync()
    {
        return await _deviceRepository.GetAllAsync();
    }

    public async Task<Device?> GetDeviceByIdAsync(string id)
    {
        return await _deviceRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Device>> GetDevicesByTypeAsync(DeviceType type)
    {
        return await _deviceRepository.GetByTypeAsync(type);
    }

    public async Task<DeviceData?> GetLatestDeviceDataAsync(string deviceId)
    {
        return await _timeSeriesRepository.GetLatestDataAsync(deviceId);
    }

    public async Task<IEnumerable<DeviceData>> GetDeviceTrendDataAsync(string deviceId, DateTime startTime, DateTime endTime)
    {
        return await _timeSeriesRepository.GetTrendDataAsync(deviceId, startTime, endTime);
    }

    public async Task AddDeviceDataAsync(DeviceData data)
    {
        try
        {
            await _timeSeriesRepository.AddDeviceDataAsync(data);
            _logger.LogInformation("设备数据已保存: {DeviceId}, 时间: {Timestamp}", data.DeviceId, data.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存设备数据失败: {DeviceId}", data.DeviceId);
            throw;
        }
    }

    public async Task AddRangeDeviceDataAsync(IEnumerable<DeviceData> data)
    {
        try
        {
            await _timeSeriesRepository.AddRangeDeviceDataAsync(data);
            _logger.LogInformation("批量保存设备数据: {Count} 条", data.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量保存设备数据失败");
            throw;
        }
    }

    public async Task UpdateDeviceStatusAsync(string deviceId, DeviceStatus status)
    {
        await _deviceRepository.UpdateStatusAsync(deviceId, status);
    }
}
