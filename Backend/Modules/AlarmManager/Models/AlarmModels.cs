using System;

namespace ChillerPlant.Modules.AlarmManager.Models
{
    public class AlarmDto
    {
        public long AlarmId { get; set; }
        public int? DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string AlarmType { get; set; }
        public int AlarmLevel { get; set; }
        public string AlarmMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Status { get; set; }
        public string AckBy { get; set; }
        public DateTime? AckAt { get; set; }
    }

    public class WorkOrderDto
    {
        public long WorkOrderId { get; set; }
        public long? AlarmId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Priority { get; set; }
        public int Status { get; set; }
        public string StatusText { get; set; }
        public string Assignee { get; set; }
        public string Remark { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class WechatPushQueueItem
    {
        public long AlarmId { get; set; }
        public string AlarmType { get; set; }
        public int AlarmLevel { get; set; }
        public string AlarmMessage { get; set; }
        public string DeviceName { get; set; }
        public DateTime AddedAt { get; set; }
    }

    public class WechatPushConfig
    {
        public string WebhookUrl { get; set; }
        public string AppKey { get; set; }
        public string AppSecret { get; set; }
        public int AggregateWindowSeconds { get; set; } = 60;
        public int MaxAlarmsPerMessage { get; set; } = 10;
        public int PushIntervalSeconds { get; set; } = 5;
    }
}
