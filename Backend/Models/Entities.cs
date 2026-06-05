using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChillerPlant.Models
{
    public class DeviceType
    {
        [Key]
        public int DeviceTypeId { get; set; }
        [Required]
        [MaxLength(50)]
        public string TypeName { get; set; }
        [MaxLength(200)]
        public string Description { get; set; }
        public decimal DesignCOP { get; set; }
        public decimal PowerRating { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public virtual ICollection<Device> Devices { get; set; }
    }

    public class Device
    {
        [Key]
        public int DeviceId { get; set; }
        [Required]
        public int DeviceTypeId { get; set; }
        [Required]
        [MaxLength(100)]
        public string DeviceName { get; set; }
        [Required]
        [MaxLength(50)]
        public string DeviceCode { get; set; }
        [Required]
        public int BacnetInstance { get; set; }
        [MaxLength(50)]
        public string IpAddress { get; set; }
        [MaxLength(200)]
        public string Location { get; set; }
        public DateTime? InstallDate { get; set; }
        public int Status { get; set; } = 1;
        public decimal DesignCOP { get; set; }
        public decimal RatedPower { get; set; }
        public decimal RatedCapacity { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        [ForeignKey("DeviceTypeId")]
        public virtual DeviceType DeviceType { get; set; }
        public virtual ICollection<DeviceData> DeviceData { get; set; }
    }

    public class DeviceData
    {
        [Key]
        public long DataId { get; set; }
        [Required]
        public int DeviceId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public decimal Power { get; set; }
        public decimal? SupplyWaterTemp { get; set; }
        public decimal? ReturnWaterTemp { get; set; }
        public decimal? CoolingWaterInTemp { get; set; }
        public decimal? CoolingWaterOutTemp { get; set; }
        public decimal? FlowRate { get; set; }
        public decimal? SupplyPressure { get; set; }
        public decimal? ReturnPressure { get; set; }
        public decimal? LoadRate { get; set; }
        public decimal? Frequency { get; set; }
        public decimal? Vibration { get; set; }
        public decimal? Current { get; set; }
        public decimal? Voltage { get; set; }
        public long RunningHours { get; set; } = 0;
        public int Status { get; set; } = 1;
        public decimal? COP { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [ForeignKey("DeviceId")]
        public virtual Device Device { get; set; }
    }

    public class SystemEfficiency
    {
        [Key]
        public long EfficiencyId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public decimal? TotalCoolingCapacity { get; set; }
        public decimal? TotalPowerConsumption { get; set; }
        public decimal SystemCOP { get; set; }
        public decimal DesignCOP { get; set; }
        public decimal? COPRatio { get; set; }
        public decimal? ChillerPower { get; set; }
        public decimal? PumpPower { get; set; }
        public decimal? TowerPower { get; set; }
        public decimal? OutdoorTemp { get; set; }
        public decimal? WetBulbTemp { get; set; }
        public decimal? TotalFlowRate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class EnergyConsumption
    {
        [Key]
        public long ConsumptionId { get; set; }
        public int? DeviceId { get; set; }
        public DateTime Date { get; set; }
        public int? Hour { get; set; }
        public decimal EnergyConsumed { get; set; }
        public decimal? CoolingCapacity { get; set; }
        public decimal? AvgCOP { get; set; }
        public decimal? PeakPower { get; set; }
        public int? Runtime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [ForeignKey("DeviceId")]
        public virtual Device Device { get; set; }
    }

    public class Alarm
    {
        [Key]
        public long AlarmId { get; set; }
        [Required]
        [MaxLength(50)]
        public string AlarmCode { get; set; }
        [Required]
        public int AlarmLevel { get; set; }
        public int? DeviceId { get; set; }
        [Required]
        [MaxLength(50)]
        public string AlarmType { get; set; }
        [Required]
        [MaxLength(500)]
        public string AlarmMessage { get; set; }
        [MaxLength(50)]
        public string ParameterName { get; set; }
        public decimal? ActualValue { get; set; }
        public decimal? ThresholdValue { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? Duration { get; set; }
        public int AckStatus { get; set; } = 0;
        [MaxLength(50)]
        public string AckBy { get; set; }
        public DateTime? AckTime { get; set; }
        public int Status { get; set; } = 1;
        public int WechatPushStatus { get; set; } = 0;
        public long? WorkOrderId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [ForeignKey("DeviceId")]
        public virtual Device Device { get; set; }
    }

    public class WorkOrder
    {
        [Key]
        public long WorkOrderId { get; set; }
        [Required]
        [MaxLength(50)]
        public string OrderNo { get; set; }
        public long? AlarmId { get; set; }
        public int? DeviceId { get; set; }
        [Required]
        [MaxLength(50)]
        public string OrderType { get; set; }
        [Required]
        public int Priority { get; set; }
        [Required]
        [MaxLength(200)]
        public string Title { get; set; }
        [MaxLength(1000)]
        public string Description { get; set; }
        public int Status { get; set; } = 0;
        [MaxLength(50)]
        public string Assignee { get; set; }
        public decimal? EstimateTime { get; set; }
        public decimal? ActualTime { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? CompleteTime { get; set; }
        public DateTime? CloseTime { get; set; }
        [MaxLength(1000)]
        public string Remark { get; set; }
        [MaxLength(50)]
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        [ForeignKey("AlarmId")]
        public virtual Alarm Alarm { get; set; }
        [ForeignKey("DeviceId")]
        public virtual Device Device { get; set; }
    }

    public class OptimizationRecommendation
    {
        [Key]
        public long RecommendationId { get; set; }
        public DateTime RecommendationTime { get; set; }
        public decimal CurrentLoadRate { get; set; }
        public decimal? OutdoorTemp { get; set; }
        public decimal? WetBulbTemp { get; set; }
        [MaxLength(500)]
        public string RecommendedChillerCombination { get; set; }
        public decimal? RecommendedSupplyWaterTemp { get; set; }
        public decimal? PredictedCOP { get; set; }
        public decimal? CurrentCOP { get; set; }
        public decimal? ExpectedEnergySaving { get; set; }
        public decimal? ExpectedEnergySavingPercent { get; set; }
        [MaxLength(500)]
        public string OptimizationStrategy { get; set; }
        public bool IsImplemented { get; set; } = false;
        public DateTime? ImplementedAt { get; set; }
        public decimal? ActualCOPAfterImpl { get; set; }
        public decimal? ActualEnergySaving { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class EnergyDiagnosisReport
    {
        [Key]
        public long ReportId { get; set; }
        public DateTime ReportDate { get; set; }
        public decimal? SystemAvgCOP { get; set; }
        public decimal DesignCOP { get; set; }
        public decimal? COPRatio { get; set; }
        public decimal? TotalEnergyConsumption { get; set; }
        public decimal? BenchmarkEnergyConsumption { get; set; }
        public decimal? EnergySavingPotential { get; set; }
        [MaxLength(2000)]
        public string DiagnosisFindings { get; set; }
        [MaxLength(2000)]
        public string Recommendations { get; set; }
        [MaxLength(500)]
        public string LowEfficiencyDevices { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }

    public class PipeConnection
    {
        [Key]
        public int ConnectionId { get; set; }
        [Required]
        public int FromDeviceId { get; set; }
        [Required]
        public int ToDeviceId { get; set; }
        [Required]
        [MaxLength(50)]
        public string PipeType { get; set; }
        [MaxLength(20)]
        public string Color { get; set; } = "#3498db";
        public int FlowDirection { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [ForeignKey("FromDeviceId")]
        public virtual Device FromDevice { get; set; }
        [ForeignKey("ToDeviceId")]
        public virtual Device ToDevice { get; set; }
    }
}
