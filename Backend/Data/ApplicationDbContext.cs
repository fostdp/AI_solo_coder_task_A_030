using Microsoft.EntityFrameworkCore;
using ChillerPlant.Models;

namespace ChillerPlant.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<DeviceType> DeviceTypes { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<DeviceData> DeviceData { get; set; }
        public DbSet<SystemEfficiency> SystemEfficiencies { get; set; }
        public DbSet<EnergyConsumption> EnergyConsumptions { get; set; }
        public DbSet<Alarm> Alarms { get; set; }
        public DbSet<WorkOrder> WorkOrders { get; set; }
        public DbSet<OptimizationRecommendation> OptimizationRecommendations { get; set; }
        public DbSet<EnergyDiagnosisReport> EnergyDiagnosisReports { get; set; }
        public DbSet<PipeConnection> PipeConnections { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DeviceType>().ToTable("DeviceTypes");
            modelBuilder.Entity<Device>().ToTable("Devices");
            modelBuilder.Entity<DeviceData>().ToTable("DeviceData");
            modelBuilder.Entity<SystemEfficiency>().ToTable("SystemEfficiency");
            modelBuilder.Entity<EnergyConsumption>().ToTable("EnergyConsumption");
            modelBuilder.Entity<Alarm>().ToTable("Alarms");
            modelBuilder.Entity<WorkOrder>().ToTable("WorkOrders");
            modelBuilder.Entity<OptimizationRecommendation>().ToTable("OptimizationRecommendations");
            modelBuilder.Entity<EnergyDiagnosisReport>().ToTable("EnergyDiagnosisReports");
            modelBuilder.Entity<PipeConnection>().ToTable("PipeConnections");

            modelBuilder.Entity<Device>()
                .HasIndex(d => d.DeviceCode)
                .IsUnique();
            
            modelBuilder.Entity<Device>()
                .HasIndex(d => d.BacnetInstance)
                .IsUnique();

            modelBuilder.Entity<DeviceData>()
                .HasIndex(d => new { d.DeviceId, d.Timestamp });

            modelBuilder.Entity<SystemEfficiency>()
                .HasIndex(s => s.Timestamp);

            modelBuilder.Entity<Alarm>()
                .HasIndex(a => new { a.Status, a.AlarmLevel, a.StartTime });
        }
    }
}
