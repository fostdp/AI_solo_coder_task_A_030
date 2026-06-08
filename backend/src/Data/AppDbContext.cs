using Microsoft.EntityFrameworkCore;
using ChillerPlantOptimization.Models;

namespace ChillerPlantOptimization.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceData> DeviceData => Set<DeviceData>();
    public DbSet<Alarm> Alarms => Set<Alarm>();
    public DbSet<AlarmThreshold> AlarmThresholds => Set<AlarmThreshold>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<EfficiencyRecord> EfficiencyRecords => Set<EfficiencyRecord>();
    public DbSet<OptimizationRecommendation> OptimizationRecommendations => Set<OptimizationRecommendation>();
    public DbSet<SystemMetric> SystemMetrics => Set<SystemMetric>();
    public DbSet<DiagnosisReport> DiagnosisReports => Set<DiagnosisReport>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceData>()
            .HasIndex(d => new { d.DeviceId, d.Timestamp })
            .IsDescending(false, true);

        modelBuilder.Entity<DeviceData>()
            .HasIndex(d => d.Timestamp)
            .IsDescending(true);

        modelBuilder.Entity<EfficiencyRecord>()
            .HasIndex(e => e.Timestamp)
            .IsDescending(true);

        modelBuilder.Entity<SystemMetric>()
            .HasIndex(s => s.Timestamp)
            .IsDescending(true);

        modelBuilder.Entity<Alarm>()
            .HasIndex(a => a.StartTime)
            .IsDescending(true);

        modelBuilder.Entity<OptimizationRecommendation>()
            .HasIndex(o => o.GeneratedAt)
            .IsDescending(true);

        modelBuilder.Entity<DiagnosisReport>()
            .HasIndex(d => d.ReportDate)
            .IsDescending(true);

        modelBuilder.Entity<WorkOrder>()
            .HasIndex(w => w.WorkOrderNo)
            .IsUnique();
    }
}
