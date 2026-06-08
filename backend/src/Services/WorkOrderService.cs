using ChillerPlantOptimization.Models;
using ChillerPlantOptimization.Repositories;

namespace ChillerPlantOptimization.Services;

public interface IWorkOrderService
{
    Task<IEnumerable<WorkOrder>> GetWorkOrdersAsync(int? status = null, int page = 1, int pageSize = 20);
    Task<WorkOrder?> GetWorkOrderByIdAsync(long id);
    Task<WorkOrder?> GetWorkOrderByAlarmIdAsync(long alarmId);
    Task<WorkOrder> CreateWorkOrderAsync(string title, string description, long? alarmId = null, string? assignee = null, int priority = 2);
    Task<bool> AssignWorkOrderAsync(long id, string processor);
    Task<bool> StartWorkOrderAsync(long id, string processor);
    Task<bool> CompleteWorkOrderAsync(long id, string processor, string resolution);
    Task<bool> CloseWorkOrderAsync(long id, string processor);
    Task<object> GetWorkOrderStatsAsync();
}

public class WorkOrderService : IWorkOrderService
{
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly IAlarmRepository _alarmRepository;
    private readonly ILogger<WorkOrderService> _logger;

    public WorkOrderService(
        IWorkOrderRepository workOrderRepository,
        IAlarmRepository alarmRepository,
        ILogger<WorkOrderService> logger)
    {
        _workOrderRepository = workOrderRepository;
        _alarmRepository = alarmRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<WorkOrder>> GetWorkOrdersAsync(int? status = null, int page = 1, int pageSize = 20)
    {
        return await _workOrderRepository.GetWorkOrdersAsync(status, page, pageSize);
    }

    public async Task<WorkOrder?> GetWorkOrderByIdAsync(long id)
    {
        return await _workOrderRepository.GetByIdAsync(id);
    }

    public async Task<WorkOrder?> GetWorkOrderByAlarmIdAsync(long alarmId)
    {
        return await _workOrderRepository.GetByAlarmIdAsync(alarmId);
    }

    public async Task<WorkOrder> CreateWorkOrderAsync(string title, string description, long? alarmId = null, string? assignee = null, int priority = 2)
    {
        var workOrderNo = $"WO{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";

        var workOrder = new WorkOrder
        {
            WorkOrderNo = workOrderNo,
            AlarmId = alarmId,
            Title = title,
            Description = description,
            Assignee = assignee,
            Status = string.IsNullOrEmpty(assignee) ? WorkOrderStatus.Created : WorkOrderStatus.Assigned,
            Priority = priority,
            CreatedAt = DateTime.UtcNow
        };

        await _workOrderRepository.AddAsync(workOrder);
        _logger.LogInformation("工单已创建: {WorkOrderNo}", workOrderNo);

        if (alarmId.HasValue)
        {
            var alarm = await _alarmRepository.GetByIdAsync(alarmId.Value);
            if (alarm != null)
            {
                alarm.Status = AlarmStatus.WorkOrderGenerated;
                await _alarmRepository.UpdateAsync(alarm);
            }
        }

        return workOrder;
    }

    public async Task<bool> AssignWorkOrderAsync(long id, string processor)
    {
        var workOrder = await _workOrderRepository.GetByIdAsync(id);
        if (workOrder == null) return false;

        workOrder.Assignee = processor;
        workOrder.Status = WorkOrderStatus.Assigned;
        await _workOrderRepository.UpdateAsync(workOrder);

        _logger.LogInformation("工单 {Id} 已指派给 {Processor}", id, processor);
        return true;
    }

    public async Task<bool> StartWorkOrderAsync(long id, string processor)
    {
        var workOrder = await _workOrderRepository.GetByIdAsync(id);
        if (workOrder == null) return false;

        workOrder.Status = WorkOrderStatus.InProgress;
        workOrder.Assignee ??= processor;
        await _workOrderRepository.UpdateAsync(workOrder);

        _logger.LogInformation("工单 {Id} 已开始处理", id);
        return true;
    }

    public async Task<bool> CompleteWorkOrderAsync(long id, string processor, string resolution)
    {
        var workOrder = await _workOrderRepository.GetByIdAsync(id);
        if (workOrder == null) return false;

        workOrder.Status = WorkOrderStatus.Completed;
        workOrder.CompletedAt = DateTime.UtcNow;
        workOrder.CompletedBy = processor;
        workOrder.Resolution = resolution;
        await _workOrderRepository.UpdateAsync(workOrder);

        if (workOrder.AlarmId.HasValue)
        {
            var alarm = await _alarmRepository.GetByIdAsync(workOrder.AlarmId.Value);
            if (alarm != null)
            {
                alarm.Status = AlarmStatus.Resolved;
                alarm.EndTime = DateTime.UtcNow;
                await _alarmRepository.UpdateAsync(alarm);
            }
        }

        _logger.LogInformation("工单 {Id} 已完成", id);
        return true;
    }

    public async Task<bool> CloseWorkOrderAsync(long id, string processor)
    {
        var workOrder = await _workOrderRepository.GetByIdAsync(id);
        if (workOrder == null) return false;

        workOrder.Status = WorkOrderStatus.Closed;
        await _workOrderRepository.UpdateAsync(workOrder);

        _logger.LogInformation("工单 {Id} 已关闭", id);
        return true;
    }

    public async Task<object> GetWorkOrderStatsAsync()
    {
        var allOrders = (await _workOrderRepository.GetWorkOrdersAsync(null, 1, 10000)).ToList();
        return new
        {
            Total = allOrders.Count,
            Created = allOrders.Count(w => w.Status == WorkOrderStatus.Created),
            Assigned = allOrders.Count(w => w.Status == WorkOrderStatus.Assigned),
            InProgress = allOrders.Count(w => w.Status == WorkOrderStatus.InProgress),
            Completed = allOrders.Count(w => w.Status == WorkOrderStatus.Completed),
            Closed = allOrders.Count(w => w.Status == WorkOrderStatus.Closed)
        };
    }
}
