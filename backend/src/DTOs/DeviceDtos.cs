namespace ChillerPlantOptimization.DTOs;

public class DeviceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DeviceType { get; set; }
    public string DeviceTypeName { get; set; } = string.Empty;
    public decimal DesignCOP { get; set; }
    public decimal RatedPower { get; set; }
    public int Status { get; set; }
    public int EfficiencyStatus { get; set; }
    public decimal? CurrentCOP { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public decimal OperatingHours { get; set; }
}

public class DeviceRealtimeDataDto
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public decimal Power { get; set; }
    public decimal SupplyTemperature { get; set; }
    public decimal ReturnTemperature { get; set; }
    public decimal Pressure { get; set; }
    public decimal FlowRate { get; set; }
    public decimal? Frequency { get; set; }
    public decimal? Current { get; set; }
    public decimal? Voltage { get; set; }
    public decimal? InletTemperature { get; set; }
    public decimal? OutletTemperature { get; set; }
    public decimal? FanSpeed { get; set; }
}

public class TrendDataPointDto
{
    public DateTime Timestamp { get; set; }
    public decimal Value { get; set; }
}

public class DeviceTrendDataDto
{
    public string DeviceId { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public List<TrendDataPointDto> DataPoints { get; set; } = new();
}

public class SystemMetricsDto
{
    public DateTime Timestamp { get; set; }
    public decimal DailyEnergy { get; set; }
    public decimal RealtimeCOP { get; set; }
    public decimal EnergySaving { get; set; }
    public decimal PeakPower { get; set; }
    public int RunningDeviceCount { get; set; }
    public int TotalDeviceCount { get; set; }
}

public class EfficiencyRecordDto
{
    public DateTime Timestamp { get; set; }
    public decimal SystemCOP { get; set; }
    public decimal DesignCOP { get; set; }
    public decimal DesignCOPRatio { get; set; }
    public decimal TotalPower { get; set; }
    public decimal TotalCoolingCapacity { get; set; }
    public decimal ChilledWaterSupplyTemp { get; set; }
    public decimal ChilledWaterReturnTemp { get; set; }
    public decimal CoolingWaterSupplyTemp { get; set; }
    public decimal CoolingWaterReturnTemp { get; set; }
    public decimal LoadRate { get; set; }
    public decimal DailyEnergyConsumption { get; set; }
    public decimal EnergySaving { get; set; }
}

public class AlarmDto
{
    public long Id { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public int AlarmLevel { get; set; }
    public int AlarmType { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int Status { get; set; }
    public int DurationMinutes { get; set; }
    public string? ParameterName { get; set; }
    public decimal? ParameterValue { get; set; }
    public decimal? ThresholdValue { get; set; }
}

public class WorkOrderDto
{
    public long Id { get; set; }
    public string WorkOrderNo { get; set; } = string.Empty;
    public long? AlarmId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Assignee { get; set; }
    public int Status { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public string? Resolution { get; set; }
}

public class OptimizationRecommendationDto
{
    public long Id { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string DeviceCombination { get; set; } = string.Empty;
    public string RunningChillers { get; set; } = string.Empty;
    public string RunningPumps { get; set; } = string.Empty;
    public string RunningTowers { get; set; } = string.Empty;
    public decimal PredictedCOP { get; set; }
    public decimal PredictedPower { get; set; }
    public decimal ChilledWaterSetpoint { get; set; }
    public decimal ExpectedEnergySaving { get; set; }
    public decimal ExpectedSavingPercent { get; set; }
    public decimal LoadRate { get; set; }
    public int Status { get; set; }
}

public class DiagnosisReportDto
{
    public long Id { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ReportDate { get; set; }
    public decimal SystemAverageCOP { get; set; }
    public decimal DesignCOPRatio { get; set; }
    public decimal TotalEnergyConsumption { get; set; }
    public decimal TotalEnergySaving { get; set; }
    public string? LowEfficiencyDevices { get; set; }
    public string DiagnosisContent { get; set; } = string.Empty;
    public string Recommendations { get; set; } = string.Empty;
}

public class OptimizeRequestDto
{
    public long RecommendationId { get; set; }
    public string AppliedBy { get; set; } = string.Empty;
}

public class AcknowledgeAlarmRequestDto
{
    public string AcknowledgedBy { get; set; } = string.Empty;
}

public class ProcessWorkOrderRequestDto
{
    public string Processor { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
}
