using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChillerPlantOptimization.Models;

[Table("Devices")]
public class Device
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public DeviceType DeviceTypeId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DesignCOP { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal RatedPower { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal RatedCoolingCapacity { get; set; }

    [MaxLength(100)]
    public string BACnetAddress { get; set; } = string.Empty;

    public int BACnetInstance { get; set; }

    public int PositionX { get; set; }

    public int PositionY { get; set; }

    public DeviceStatus Status { get; set; } = DeviceStatus.Stopped;

    public EfficiencyStatus EfficiencyStatus { get; set; } = EfficiencyStatus.High;

    [Column(TypeName = "decimal(18,4)")]
    public decimal? CurrentCOP { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal OperatingHours { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<DeviceData>? DeviceData { get; set; }
    public virtual ICollection<Alarm>? Alarms { get; set; }
}

[Table("DeviceData")]
public class DeviceData
{
    [Key]
    public long Id { get; set; }

    [MaxLength(50)]
    public string DeviceId { get; set; } = string.Empty;

    [ForeignKey("DeviceId")]
    public virtual Device? Device { get; set; }

    public DateTime Timestamp { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Power { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal SupplyTemperature { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReturnTemperature { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Pressure { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal FlowRate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? Frequency { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? Current { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? Voltage { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? InletTemperature { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? OutletTemperature { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? FanSpeed { get; set; }
}
