using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChillerPlantOptimization.Models;

[Table("Alarms")]
public class Alarm
{
    [Key]
    public long Id { get; set; }

    [MaxLength(50)]
    public string? DeviceId { get; set; }

    [ForeignKey("DeviceId")]
    public virtual Device? Device { get; set; }

    public AlarmLevel AlarmLevel { get; set; }

    public AlarmType AlarmType { get; set; }

    [MaxLength(100)]
    public string? ParameterName { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? ParameterValue { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? ThresholdValue { get; set; }

    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public AlarmStatus Status { get; set; } = AlarmStatus.Active;

    public int DurationMinutes { get; set; } = 0;

    public bool Acknowledged { get; set; } = false;

    [MaxLength(50)]
    public string? AcknowledgedBy { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public bool WeChatPushed { get; set; } = false;

    public DateTime? WeChatPushedAt { get; set; }

    public virtual WorkOrder? WorkOrder { get; set; }
}

[Table("AlarmThresholds")]
public class AlarmThreshold
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    public string ParameterName { get; set; } = string.Empty;

    public DeviceType? DeviceTypeId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? UpperLimit { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? LowerLimit { get; set; }

    public int DurationMinutes { get; set; } = 10;

    public AlarmLevel AlarmLevel { get; set; } = AlarmLevel.Level1;

    public bool Enabled { get; set; } = true;
}

[Table("WorkOrders")]
public class WorkOrder
{
    [Key]
    public long Id { get; set; }

    public long? AlarmId { get; set; }

    [ForeignKey("AlarmId")]
    public virtual Alarm? Alarm { get; set; }

    [MaxLength(50)]
    public string WorkOrderNo { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Assignee { get; set; }

    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Pending;

    public int Priority { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [MaxLength(50)]
    public string? CompletedBy { get; set; }

    [MaxLength(1000)]
    public string? Resolution { get; set; }
}
