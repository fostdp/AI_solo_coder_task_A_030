using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChillerPlantOptimization.Models;

[Table("EfficiencyRecords")]
public class EfficiencyRecord
{
    [Key]
    public long Id { get; set; }

    public DateTime Timestamp { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal SystemCOP { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DesignCOP { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DesignCOPRatio { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalPower { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalCoolingCapacity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ChilledWaterSupplyTemp { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ChilledWaterReturnTemp { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal CoolingWaterSupplyTemp { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal CoolingWaterReturnTemp { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal LoadRate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DailyEnergyConsumption { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal EnergySaving { get; set; }
}

[Table("OptimizationRecommendations")]
public class OptimizationRecommendation
{
    [Key]
    public long Id { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string DeviceCombination { get; set; } = string.Empty;

    [MaxLength(200)]
    public string RunningChillers { get; set; } = string.Empty;

    [MaxLength(500)]
    public string RunningPumps { get; set; } = string.Empty;

    [MaxLength(200)]
    public string RunningTowers { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal PredictedCOP { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal PredictedPower { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ChilledWaterSetpoint { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ExpectedEnergySaving { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ExpectedSavingPercent { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal LoadRate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? AmbientTemp { get; set; }

    public RecommendationStatus Status { get; set; } = RecommendationStatus.New;

    public DateTime? AppliedAt { get; set; }

    [MaxLength(50)]
    public string? AppliedBy { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? ActualCOP { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? ActualEnergySaving { get; set; }
}

[Table("SystemMetrics")]
public class SystemMetric
{
    [Key]
    public long Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,4)")]
    public decimal DailyEnergy { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal RealtimeCOP { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal EnergySaving { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal PeakPower { get; set; }

    public int RunningDeviceCount { get; set; }

    public int TotalDeviceCount { get; set; }
}

[Table("DiagnosisReports")]
public class DiagnosisReport
{
    [Key]
    public long Id { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public DateTime ReportDate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal SystemAverageCOP { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DesignCOPRatio { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalEnergyConsumption { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalEnergySaving { get; set; }

    [MaxLength(500)]
    public string? LowEfficiencyDevices { get; set; }

    public string DiagnosisContent { get; set; } = string.Empty;

    public string Recommendations { get; set; } = string.Empty;
}

[Table("SystemSettings")]
public class SystemSetting
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    public string SettingKey { get; set; } = string.Empty;

    [MaxLength(500)]
    public string SettingValue { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("Users")]
public class User
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(50)]
    public string RealName { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? WeChatUserId { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }
}
