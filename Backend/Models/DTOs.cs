using System;
using System.Collections.Generic;

namespace ChillerPlant.Models
{
    public class DeviceDataDto
    {
        public int DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string DeviceCode { get; set; }
        public int DeviceTypeId { get; set; }
        public string TypeName { get; set; }
        public decimal Power { get; set; }
        public decimal? SupplyWaterTemp { get; set; }
        public decimal? ReturnWaterTemp { get; set; }
        public decimal? CoolingWaterInTemp { get; set; }
        public decimal? CoolingWaterOutTemp { get; set; }
        public decimal? FlowRate { get; set; }
        public decimal? LoadRate { get; set; }
        public decimal? COP { get; set; }
        public int Status { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DeviceStatusDto
    {
        public int DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string DeviceCode { get; set; }
        public int DeviceTypeId { get; set; }
        public string TypeName { get; set; }
        public int Status { get; set; }
        public decimal? CurrentPower { get; set; }
        public decimal? CurrentLoadRate { get; set; }
        public decimal? CurrentCOP { get; set; }
        public decimal DesignCOP { get; set; }
        public string EfficiencyStatus { get; set; }
        public string StatusColor { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public DateTime? LastUpdateTime { get; set; }
    }

    public class DeviceTrendDataDto
    {
        public DateTime Timestamp { get; set; }
        public decimal? Power { get; set; }
        public decimal? SupplyWaterTemp { get; set; }
        public decimal? ReturnWaterTemp { get; set; }
        public decimal? CoolingWaterInTemp { get; set; }
        public decimal? CoolingWaterOutTemp { get; set; }
        public decimal? FlowRate { get; set; }
        public decimal? LoadRate { get; set; }
        public decimal? COP { get; set; }
    }

    public class RealtimeDashboardDto
    {
        public decimal DailyTotalEnergy { get; set; }
        public decimal RealtimeCOP { get; set; }
        public decimal DesignCOP { get; set; }
        public decimal COPRatio { get; set; }
        public decimal TotalEnergySaving { get; set; }
        public decimal EnergySavingPercent { get; set; }
        public decimal TotalCoolingCapacity { get; set; }
        public decimal TotalPowerConsumption { get; set; }
        public DateTime UpdateTime { get; set; }
        public List<DeviceStatusDto> DeviceStatusList { get; set; }
        public List<AlarmDto> ActiveAlarms { get; set; }
    }

    public class AlarmDto
    {
        public long AlarmId { get; set; }
        public string AlarmCode { get; set; }
        public int AlarmLevel { get; set; }
        public int? DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string DeviceCode { get; set; }
        public string AlarmType { get; set; }
        public string AlarmMessage { get; set; }
        public string ParameterName { get; set; }
        public decimal? ActualValue { get; set; }
        public decimal? ThresholdValue { get; set; }
        public DateTime StartTime { get; set; }
        public int? Duration { get; set; }
        public int Status { get; set; }
        public int AckStatus { get; set; }
    }

    public class OptimizationRecommendationDto
    {
        public long RecommendationId { get; set; }
        public DateTime RecommendationTime { get; set; }
        public decimal CurrentLoadRate { get; set; }
        public decimal? OutdoorTemp { get; set; }
        public decimal? WetBulbTemp { get; set; }
        public string RecommendedChillerCombination { get; set; }
        public decimal? RecommendedSupplyWaterTemp { get; set; }
        public decimal? PredictedCOP { get; set; }
        public decimal? CurrentCOP { get; set; }
        public decimal? ExpectedEnergySaving { get; set; }
        public decimal? ExpectedEnergySavingPercent { get; set; }
        public string OptimizationStrategy { get; set; }
        public bool IsImplemented { get; set; }
    }

    public class WorkOrderDto
    {
        public long WorkOrderId { get; set; }
        public string OrderNo { get; set; }
        public long? AlarmId { get; set; }
        public int? DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string OrderType { get; set; }
        public int Priority { get; set; }
        public string PriorityText { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Status { get; set; }
        public string StatusText { get; set; }
        public string Assignee { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class EnergyDiagnosisReportDto
    {
        public long ReportId { get; set; }
        public DateTime ReportDate { get; set; }
        public decimal? SystemAvgCOP { get; set; }
        public decimal DesignCOP { get; set; }
        public decimal? COPRatio { get; set; }
        public decimal? TotalEnergyConsumption { get; set; }
        public decimal? EnergySavingPotential { get; set; }
        public string DiagnosisFindings { get; set; }
        public string Recommendations { get; set; }
        public string LowEfficiencyDevices { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class BacnetDataDto
    {
        public int BacnetInstance { get; set; }
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
        public long RunningHours { get; set; }
        public int Status { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class EnergyStatisticsDto
    {
        public DateTime Date { get; set; }
        public decimal TotalEnergy { get; set; }
        public decimal ChillerEnergy { get; set; }
        public decimal ChillerPumpEnergy { get; set; }
        public decimal CoolingPumpEnergy { get; set; }
        public decimal TowerEnergy { get; set; }
        public decimal AvgCOP { get; set; }
        public decimal PeakPower { get; set; }
    }

    public class PipeConnectionDto
    {
        public int ConnectionId { get; set; }
        public int FromDeviceId { get; set; }
        public int ToDeviceId { get; set; }
        public string PipeType { get; set; }
        public string Color { get; set; }
        public int FlowDirection { get; set; }
        public int FromX { get; set; }
        public int FromY { get; set; }
        public int ToX { get; set; }
        public int ToY { get; set; }
    }
}
