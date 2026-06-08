using Microsoft.AspNetCore.Mvc;
using ChillerPlantOptimization.DTOs;
using ChillerPlantOptimization.Services;

namespace ChillerPlantOptimization.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkOrderController : ControllerBase
{
    private readonly IWorkOrderService _workOrderService;
    private readonly ILogger<WorkOrderController> _logger;

    public WorkOrderController(
        IWorkOrderService workOrderService,
        ILogger<WorkOrderController> logger)
    {
        _workOrderService = workOrderService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkOrderDto>>> GetWorkOrders(
        [FromQuery] int? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var orders = await _workOrderService.GetWorkOrdersAsync(status, page, pageSize);
        var dtos = orders.Select(w => new WorkOrderDto
        {
            Id = w.Id,
            WorkOrderNo = w.WorkOrderNo,
            AlarmId = w.AlarmId,
            Title = w.Title,
            Description = w.Description,
            Assignee = w.Assignee,
            Status = (int)w.Status,
            Priority = w.Priority,
            CreatedAt = w.CreatedAt,
            CompletedAt = w.CompletedAt,
            CompletedBy = w.CompletedBy,
            Resolution = w.Resolution
        });

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkOrderDto>> GetWorkOrderById(long id)
    {
        var order = await _workOrderService.GetWorkOrderByIdAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        var dto = new WorkOrderDto
        {
            Id = order.Id,
            WorkOrderNo = order.WorkOrderNo,
            AlarmId = order.AlarmId,
            Title = order.Title,
            Description = order.Description,
            Assignee = order.Assignee,
            Status = (int)order.Status,
            Priority = order.Priority,
            CreatedAt = order.CreatedAt,
            CompletedAt = order.CompletedAt,
            CompletedBy = order.CompletedBy,
            Resolution = order.Resolution
        };

        return Ok(dto);
    }

    [HttpGet("alarm/{alarmId}")]
    public async Task<ActionResult<WorkOrderDto>> GetWorkOrderByAlarmId(long alarmId)
    {
        var order = await _workOrderService.GetWorkOrderByAlarmIdAsync(alarmId);
        if (order == null)
        {
            return NotFound();
        }

        var dto = new WorkOrderDto
        {
            Id = order.Id,
            WorkOrderNo = order.WorkOrderNo,
            AlarmId = order.AlarmId,
            Title = order.Title,
            Description = order.Description,
            Assignee = order.Assignee,
            Status = (int)order.Status,
            Priority = order.Priority,
            CreatedAt = order.CreatedAt,
            CompletedAt = order.CompletedAt,
            CompletedBy = order.CompletedBy,
            Resolution = order.Resolution
        };

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<WorkOrderDto>> CreateWorkOrder([FromBody] WorkOrderDto dto)
    {
        var order = await _workOrderService.CreateWorkOrderAsync(
            dto.Title,
            dto.Description,
            dto.AlarmId,
            dto.Assignee,
            dto.Priority);

        var resultDto = new WorkOrderDto
        {
            Id = order.Id,
            WorkOrderNo = order.WorkOrderNo,
            AlarmId = order.AlarmId,
            Title = order.Title,
            Description = order.Description,
            Assignee = order.Assignee,
            Status = (int)order.Status,
            Priority = order.Priority,
            CreatedAt = order.CreatedAt
        };

        return CreatedAtAction(nameof(GetWorkOrderById), new { id = order.Id }, resultDto);
    }

    [HttpPut("{id}/assign")]
    public async Task<ActionResult> AssignWorkOrder(long id, [FromBody] ProcessWorkOrderRequestDto request)
    {
        var result = await _workOrderService.AssignWorkOrderAsync(id, request.Processor);
        if (result)
        {
            return Ok(new { success = true, message = "工单已指派" });
        }
        return BadRequest(new { success = false, message = "指派失败" });
    }

    [HttpPut("{id}/start")]
    public async Task<ActionResult> StartWorkOrder(long id, [FromBody] ProcessWorkOrderRequestDto request)
    {
        var result = await _workOrderService.StartWorkOrderAsync(id, request.Processor);
        if (result)
        {
            return Ok(new { success = true, message = "工单已开始处理" });
        }
        return BadRequest(new { success = false, message = "处理失败" });
    }

    [HttpPut("{id}/complete")]
    public async Task<ActionResult> CompleteWorkOrder(long id, [FromBody] ProcessWorkOrderRequestDto request)
    {
        var result = await _workOrderService.CompleteWorkOrderAsync(id, request.Processor, request.Resolution);
        if (result)
        {
            return Ok(new { success = true, message = "工单已完成" });
        }
        return BadRequest(new { success = false, message = "完成失败" });
    }

    [HttpPut("{id}/close")]
    public async Task<ActionResult> CloseWorkOrder(long id, [FromBody] ProcessWorkOrderRequestDto request)
    {
        var result = await _workOrderService.CloseWorkOrderAsync(id, request.Processor);
        if (result)
        {
            return Ok(new { success = true, message = "工单已关闭" });
        }
        return BadRequest(new { success = false, message = "关闭失败" });
    }

    [HttpGet("stats")]
    public async Task<ActionResult> GetWorkOrderStats()
    {
        var stats = await _workOrderService.GetWorkOrderStatsAsync();
        return Ok(stats);
    }
}
