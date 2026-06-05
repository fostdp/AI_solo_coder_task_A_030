using MediatR;
using ChillerPlant.Models;

namespace ChillerPlant.Modules.Shared.Events
{
    public class DeviceDataReceivedEvent : INotification
    {
        public int DeviceId { get; set; }
        public int BacnetInstance { get; set; }
        public decimal Power { get; set; }
        public decimal? COP { get; set; }
        public decimal? LoadRate { get; set; }
        public decimal? SupplyWaterTemp { get; set; }
        public decimal? ReturnWaterTemp { get; set; }
        public decimal? CoolingWaterInTemp { get; set; }
        public DateTime Timestamp { get; set; }
        public int Status { get; set; }
    }

    public class DeviceDataBatchReceivedEvent : INotification
    {
        public List<DeviceDataPoint> DataPoints { get; set; } = new();
    }

    public class DeviceDataPoint
    {
        public int DeviceId { get; set; }
        public decimal Power { get; set; }
        public decimal? COP { get; set; }
        public decimal? LoadRate { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DeviceStatusChangedEvent : INotification
    {
        public int DeviceId { get; set; }
        public int OldStatus { get; set; }
        public int NewStatus { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class SystemEfficiencyCalculatedEvent : INotification
    {
        public DateTime Timestamp { get; set; }
        public decimal SystemCOP { get; set; }
        public decimal DesignCOP { get; set; }
        public decimal COPRatio { get; set; }
        public decimal TotalPower { get; set; }
        public decimal TotalCooling { get; set; }
    }

    public class OptimizationRecommendationGeneratedEvent : INotification
    {
        public long RecommendationId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public double PredictedCOP { get; set; }
        public double ExpectedEnergySaving { get; set; }
        public double RecommendedSupplyTemp { get; set; }
    }

    public class AlarmCreatedEvent : INotification
    {
        public long AlarmId { get; set; }
        public int AlarmLevel { get; set; }
        public string AlarmType { get; set; }
        public string AlarmMessage { get; set; }
        public int? DeviceId { get; set; }
        public DateTime StartTime { get; set; }
    }

    public class AlarmPushedEvent : INotification
    {
        public long AlarmId { get; set; }
        public bool Success { get; set; }
        public DateTime PushedAt { get; set; }
    }
}
