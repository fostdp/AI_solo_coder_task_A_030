using Microsoft.EntityFrameworkCore;
using ChillerPlantOptimization.Data;
using ChillerPlantOptimization.Models;

namespace ChillerPlantOptimization.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly AppDbContext _context;

    public DeviceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Device>> GetAllAsync()
    {
        return await _context.Devices
            .AsNoTracking()
            .OrderBy(d => d.DeviceTypeId)
            .ThenBy(d => d.Name)
            .ToListAsync();
    }

    public async Task<Device?> GetByIdAsync(string id)
    {
        return await _context.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<IEnumerable<Device>> GetByTypeAsync(DeviceType type)
    {
        return await _context.Devices
            .AsNoTracking()
            .Where(d => d.DeviceTypeId == type)
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    public async Task UpdateAsync(Device device)
    {
        device.UpdatedAt = DateTime.UtcNow;
        _context.Devices.Update(device);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(string deviceId, DeviceStatus status)
    {
        var device = await _context.Devices.FindAsync(deviceId);
        if (device != null)
        {
            device.Status = status;
            device.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateEfficiencyStatusAsync(string deviceId, EfficiencyStatus status, decimal? currentCOP)
    {
        var device = await _context.Devices.FindAsync(deviceId);
        if (device != null)
        {
            device.EfficiencyStatus = status;
            device.CurrentCOP = currentCOP;
            device.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}

public class TimeSeriesRepository : ITimeSeriesRepository
{
    private readonly AppDbContext _context;

    public TimeSeriesRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddDeviceDataAsync(DeviceData data)
    {
        await _context.DeviceData.AddAsync(data);
        await _context.SaveChangesAsync();
    }

    public async Task AddRangeDeviceDataAsync(IEnumerable<DeviceData> data)
    {
        await _context.DeviceData.AddRangeAsync(data);
        await _context.SaveChangesAsync();
    }

    public async Task<DeviceData?> GetLatestDataAsync(string deviceId)
    {
        return await _context.DeviceData
            .AsNoTracking()
            .Where(d => d.DeviceId == deviceId)
            .OrderByDescending(d => d.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<DeviceData>> GetTrendDataAsync(string deviceId, DateTime startTime, DateTime endTime)
    {
        return await _context.DeviceData
            .AsNoTracking()
            .Where(d => d.DeviceId == deviceId && d.Timestamp >= startTime && d.Timestamp <= endTime)
            .OrderBy(d => d.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<DeviceData>> GetRecentDataAsync(TimeSpan timeSpan)
    {
        var startTime = DateTime.UtcNow - timeSpan;
        return await _context.DeviceData
            .AsNoTracking()
            .Where(d => d.Timestamp >= startTime)
            .OrderBy(d => d.Timestamp)
            .ToListAsync();
    }
}
