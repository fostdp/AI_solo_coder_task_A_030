using Microsoft.EntityFrameworkCore;
using ChillerPlantOptimization.Data;
using ChillerPlantOptimization.Models;

namespace ChillerPlantOptimization.Repositories;

public class EfficiencyRepository : IEfficiencyRepository
{
    private readonly AppDbContext _context;

    public EfficiencyRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddEfficiencyRecordAsync(EfficiencyRecord record)
    {
        await _context.EfficiencyRecords.AddAsync(record);

        var systemMetric = new SystemMetric
        {
            Timestamp = record.Timestamp,
            DailyEnergy = record.DailyEnergyConsumption,
            RealtimeCOP = record.SystemCOP,
            EnergySaving = record.EnergySaving,
            PeakPower = record.TotalPower,
            RunningDeviceCount = await _context.Devices.CountAsync(d => d.Status == DeviceStatus.Running),
            TotalDeviceCount = await _context.Devices.CountAsync(d => d.Status != DeviceStatus.Fault)
        };
        await _context.SystemMetrics.AddAsync(systemMetric);

        await _context.SaveChangesAsync();
    }

    public async Task<EfficiencyRecord?> GetLatestEfficiencyAsync()
    {
        return await _context.EfficiencyRecords
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<EfficiencyRecord>> GetEfficiencyTrendAsync(DateTime startTime, DateTime endTime)
    {
        return await _context.EfficiencyRecords
            .AsNoTracking()
            .Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();
    }

    public async Task<SystemMetric?> GetLatestSystemMetricAsync()
    {
        return await _context.SystemMetrics
            .AsNoTracking()
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<SystemMetric>> GetSystemMetricsTrendAsync(DateTime startTime, DateTime endTime)
    {
        return await _context.SystemMetrics
            .AsNoTracking()
            .Where(s => s.Timestamp >= startTime && s.Timestamp <= endTime)
            .OrderBy(s => s.Timestamp)
            .ToListAsync();
    }

    public async Task<DiagnosisReport?> GetLatestDiagnosisReportAsync()
    {
        return await _context.DiagnosisReports
            .AsNoTracking()
            .OrderByDescending(d => d.GeneratedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<DiagnosisReport> GenerateDiagnosisReportAsync(DateTime reportDate)
    {
        var designCOP = await GetDesignCOP();
        var records = await _context.EfficiencyRecords
            .AsNoTracking()
            .Where(e => e.Timestamp >= reportDate.Date && e.Timestamp < reportDate.Date.AddDays(1))
            .ToListAsync();

        if (!records.Any())
        {
            throw new InvalidOperationException("当日无能效数据，无法生成诊断报告");
        }

        var avgCOP = records.Average(r => r.SystemCOP);
        var avgRatio = records.Average(r => r.DesignCOPRatio);
        var totalEnergy = records.Max(r => r.DailyEnergyConsumption);
        var totalSaving = records.Sum(r => r.EnergySaving);

        var lowEfficiencyDevices = await _context.Devices
            .AsNoTracking()
            .Where(d => d.DeviceTypeId == DeviceType.CentrifugalChiller || d.DeviceTypeId == DeviceType.ScrewChiller)
            .Where(d => d.Status == DeviceStatus.Running)
            .Where(d => d.CurrentCOP < designCOP * 0.7m)
            .Select(d => d.Name)
            .ToListAsync();

        var diagnosis = BuildDiagnosisContent(reportDate, avgCOP, designCOP, avgRatio, totalEnergy, totalSaving, lowEfficiencyDevices);
        var recommendations = BuildRecommendations(totalEnergy, avgRatio);

        var report = new DiagnosisReport
        {
            ReportDate = reportDate.Date,
            SystemAverageCOP = avgCOP,
            DesignCOPRatio = avgRatio,
            TotalEnergyConsumption = totalEnergy,
            TotalEnergySaving = totalSaving,
            LowEfficiencyDevices = lowEfficiencyDevices.Any() ? string.Join(", ", lowEfficiencyDevices) : null,
            DiagnosisContent = diagnosis,
            Recommendations = recommendations
        };

        await _context.DiagnosisReports.AddAsync(report);
        await _context.SaveChangesAsync();

        return report;
    }

    private async Task<decimal> GetDesignCOP()
    {
        var setting = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.SettingKey == "SystemDesignCOP");
        return setting != null ? decimal.Parse(setting.SettingValue) : 5.5m;
    }

    private string BuildDiagnosisContent(DateTime reportDate, decimal avgCOP, decimal designCOP,
        decimal avgRatio, decimal totalEnergy, decimal totalSaving, List<string> lowEfficiencyDevices)
    {
        var content = $@"
## 系统能效诊断报告

**报告日期**: {reportDate:yyyy-MM-dd}

### 一、系统整体能效评估

- 系统平均COP: {avgCOP:F2}
- 设计COP: {designCOP:F2}
- 能效比: {avgRatio * 100:F1}%
- 日总能耗: {totalEnergy:F2} kWh
- 预计节能量: {totalSaving:F2} kWh

### 二、能效问题诊断

";

        if (avgRatio < 0.7m)
        {
            content += @"⚠️ **严重问题**: 系统整体能效低于设计值70%，需立即检查优化。

";
        }
        else if (avgRatio < 0.9m)
        {
            content += @"⚡ **需关注**: 系统整体能效偏低，建议进行运行参数优化。

";
        }

        if (lowEfficiencyDevices.Any())
        {
            content += $@"🔧 **低效设备**: 以下设备能效偏低，建议检查：
{string.Join(", ", lowEfficiencyDevices)}

";
        }

        return content;
    }

    private string BuildRecommendations(decimal totalEnergy, decimal avgRatio)
    {
        var expectedSaving = totalEnergy * 0.15m;

        return $@"### 三、优化建议

1. **设备组合优化**: 根据当前冷负荷重新计算最优设备启停组合
2. **温度设定值优化**: 适当提高冷冻水出水温度设定值
3. **水泵变频优化**: 根据供回水温差调节水泵频率
4. **冷却塔优化**: 根据湿球温度调整冷却塔运行台数
5. **定期维护**: 加强设备定期维护保养，保持换热效率

### 四、预期节能效果

如全部实施优化建议，预计可提升系统COP 10%-15%，日节能量可达 {expectedSaving:F2} kWh。
";
    }
}
