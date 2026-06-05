using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChillerPlant.Models;

namespace ChillerPlant.Data.Repositories
{
    public interface IAlarmRepository
    {
        Task<List<AlarmDto>> GetActiveAlarmsAsync();
        Task<List<AlarmDto>> GetAlarmHistoryAsync(DateTime startDate, DateTime endDate);
        Task<Alarm> CreateAlarmAsync(Alarm alarm);
        Task UpdateAlarmStatusAsync(long alarmId, int status);
        Task AcknowledgeAlarmAsync(long alarmId, string ackBy);
        Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder);
        Task<List<WorkOrderDto>> GetWorkOrdersAsync(int? status = null);
        Task UpdateWorkOrderStatusAsync(long workOrderId, int status, string remark = null);
        Task CheckAndCreateAlarmsAsync();
        Task PushAlarmToWechatAsync(Alarm alarm);
    }
}
