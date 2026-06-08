using Microsoft.EntityFrameworkCore;
using ChillerPlantOptimization.Data;
using ChillerPlantOptimization.Models;

namespace ChillerPlantOptimization.Repositories;

public class AlarmRepository : IAlarmRepository
{
    private readonly AppDbContext _context;

    public AlarmRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Alarm>> GetActiveAlarmsAsync()
    {
        return await _context.Alarms
            .AsNoTracking()
            .Include(a => a.Device)
            .Where(a => a.Status == AlarmStatus.Active || a.Status == AlarmStatus.Acknowledged)
            .OrderByDescending(a => a.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Alarm>> GetAlarmsAsync(DateTime startTime, DateTime endTime, int? level = null)
    {
        var query = _context.Alarms
            .AsNoTracking()
            .Include(a => a.Device)
            .Where(a => a.StartTime >= startTime && a.StartTime <= endTime);

        if (level.HasValue)
        {
            query = query.Where(a => (int)a.AlarmLevel == level.Value);
        }

        return await query
            .OrderByDescending(a => a.StartTime)
            .ToListAsync();
    }

    public async Task<Alarm?> GetAlarmByIdAsync(long id)
    {
        return await _context.Alarms
            .AsNoTracking()
            .Include(a => a.Device)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task AddAlarmAsync(Alarm alarm)
    {
        await _context.Alarms.AddAsync(alarm);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAlarmAsync(Alarm alarm)
    {
        _context.Alarms.Update(alarm);
        await _context.SaveChangesAsync();
    }

    public async Task AcknowledgeAlarmAsync(long id, string acknowledgedBy)
    {
        var alarm = await _context.Alarms.FindAsync(id);
        if (alarm != null)
        {
            alarm.Acknowledged = true;
            alarm.AcknowledgedBy = acknowledgedBy;
            alarm.AcknowledgedAt = DateTime.UtcNow;
            alarm.Status = AlarmStatus.Acknowledged;
            await _context.SaveChangesAsync();
        }
    }

    public async Task ResolveAlarmAsync(long id, string resolvedBy)
    {
        var alarm = await _context.Alarms.FindAsync(id);
        if (alarm != null)
        {
            alarm.Status = AlarmStatus.Resolved;
            alarm.EndTime = DateTime.UtcNow;
            alarm.DurationMinutes = (int)Math.Round((alarm.EndTime.Value - alarm.StartTime).TotalMinutes);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<AlarmThreshold>> GetAllThresholdsAsync()
    {
        return await _context.AlarmThresholds
            .AsNoTracking()
            .Where(t => t.Enabled)
            .ToListAsync();
    }
}

public class WorkOrderRepository : IWorkOrderRepository
{
    private readonly AppDbContext _context;

    public WorkOrderRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<WorkOrder>> GetWorkOrdersAsync(WorkOrderStatus? status = null)
    {
        var query = _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Alarm)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(w => w.Status == status.Value);
        }

        return await query
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<WorkOrder?> GetWorkOrderByIdAsync(long id)
    {
        return await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Alarm)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder)
    {
        workOrder.WorkOrderNo = GenerateWorkOrderNo();
        await _context.WorkOrders.AddAsync(workOrder);
        await _context.SaveChangesAsync();
        return workOrder;
    }

    public async Task UpdateWorkOrderAsync(WorkOrder workOrder)
    {
        _context.WorkOrders.Update(workOrder);
        await _context.SaveChangesAsync();
    }

    public async Task ProcessWorkOrderAsync(long id, string processor, string resolution)
    {
        var workOrder = await _context.WorkOrders.FindAsync(id);
        if (workOrder != null)
        {
            workOrder.Status = WorkOrderStatus.Completed;
            workOrder.CompletedAt = DateTime.UtcNow;
            workOrder.CompletedBy = processor;
            workOrder.Resolution = resolution;
            await _context.SaveChangesAsync();

            if (workOrder.AlarmId.HasValue)
            {
                var alarm = await _context.Alarms.FindAsync(workOrder.AlarmId.Value);
                if (alarm != null)
                {
                    alarm.Status = AlarmStatus.Resolved;
                    alarm.EndTime = DateTime.UtcNow;
                    alarm.DurationMinutes = (int)Math.Round((alarm.EndTime.Value - alarm.StartTime).TotalMinutes);
                    await _context.SaveChangesAsync();
                }
            }
        }
    }

    private string GenerateWorkOrderNo()
    {
        return $"WO{DateTime.Now:yyyyMMdd}{_context.WorkOrders.Count() + 1:D6}";
    }
}

public class OptimizationRepository : IOptimizationRepository
{
    private readonly AppDbContext _context;

    public OptimizationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<OptimizationRecommendation?> GetLatestRecommendationAsync()
    {
        return await _context.OptimizationRecommendations
            .AsNoTracking()
            .OrderByDescending(o => o.GeneratedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<OptimizationRecommendation>> GetRecommendationHistoryAsync(int count = 24)
    {
        return await _context.OptimizationRecommendations
            .AsNoTracking()
            .OrderByDescending(o => o.GeneratedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task AddRecommendationAsync(OptimizationRecommendation recommendation)
    {
        await _context.OptimizationRecommendations.AddAsync(recommendation);
        await _context.SaveChangesAsync();
    }

    public async Task ApplyRecommendationAsync(long id, string appliedBy)
    {
        var recommendation = await _context.OptimizationRecommendations.FindAsync(id);
        if (recommendation != null)
        {
            recommendation.Status = RecommendationStatus.Applied;
            recommendation.AppliedAt = DateTime.UtcNow;
            recommendation.AppliedBy = appliedBy;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RejectRecommendationAsync(long id, string rejectedBy)
    {
        var recommendation = await _context.OptimizationRecommendations.FindAsync(id);
        if (recommendation != null)
        {
            recommendation.Status = RecommendationStatus.Rejected;
            recommendation.AppliedBy = rejectedBy;
            await _context.SaveChangesAsync();
        }
    }
}

public class SystemConfigRepository : ISystemConfigRepository
{
    private readonly AppDbContext _context;

    public SystemConfigRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Dictionary<string, string>> GetAllSettingsAsync()
    {
        return await _context.SystemSettings
            .AsNoTracking()
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        var setting = await _context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SettingKey == key);
        return setting?.SettingValue;
    }

    public async Task UpdateSettingAsync(string key, string value)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
        if (setting != null)
        {
            setting.SettingValue = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            await _context.SystemSettings.AddAsync(new SystemSetting
            {
                SettingKey = key,
                SettingValue = value
            });
        }
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AlarmThreshold>> GetAlarmThresholdsAsync()
    {
        return await _context.AlarmThresholds
            .AsNoTracking()
            .Where(t => t.Enabled)
            .ToListAsync();
    }
}
