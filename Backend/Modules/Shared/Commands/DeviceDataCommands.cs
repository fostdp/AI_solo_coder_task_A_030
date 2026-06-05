using MediatR;
using ChillerPlant.Models;

namespace ChillerPlant.Modules.Shared.Commands
{
    public class InsertDeviceDataCommand : IRequest<DeviceData>
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
        public long? RunningHours { get; set; }
        public int Status { get; set; }
        public DateTime? Timestamp { get; set; }
    }

    public class InsertBatchDeviceDataCommand : IRequest<int>
    {
        public List<InsertDeviceDataCommand> DataList { get; set; } = new();
    }

    public class GetDeviceStatusCommand : IRequest<List<DeviceStatusDto>>
    {
    }

    public class GetDeviceTrendDataCommand : IRequest<List<DeviceTrendDataDto>>
    {
        public int DeviceId { get; set; }
        public int Hours { get; set; } = 24;
    }

    public class CalculateSystemEfficiencyCommand : IRequest<Unit>
    {
    }

    public class GenerateOptimizationCommand : IRequest<OptimizationRecommendationDto>
    {
    }

    public class TrainOptimizationModelCommand : IRequest<bool>
    {
        public int Epochs { get; set; } = 200;
    }

    public class CheckAlarmsCommand : IRequest<List<AlarmDto>>
    {
    }

    public class AcknowledgeAlarmCommand : IRequest<bool>
    {
        public long AlarmId { get; set; }
        public string AckBy { get; set; }
    }

    public class UpdateWorkOrderStatusCommand : IRequest<bool>
    {
        public long WorkOrderId { get; set; }
        public int Status { get; set; }
        public string Remark { get; set; }
        public string Assignee { get; set; }
    }

    public class GetRealtimeDashboardCommand : IRequest<RealtimeDashboardDto>
    {
    }

    public class PushAlarmToWechatCommand : IRequest<bool>
    {
        public long AlarmId { get; set; }
    }
}
