using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ChillerPlant.Data.Repositories;
using ChillerPlant.Models;
using ChillerPlant.Modules.Shared.Commands;

namespace ChillerPlant.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlarmsController : ControllerBase
    {
        private readonly IAlarmRepository _alarmRepository;
        private readonly IMediator _mediator;

        public AlarmsController(IAlarmRepository alarmRepository,
            IMediator mediator)
        {
            _alarmRepository = alarmRepository;
            _mediator = mediator;
        }

        [HttpGet("active")]
        public async Task<ActionResult<List<AlarmDto>>> GetActiveAlarms()
        {
            var alarms = await _alarmRepository.GetActiveAlarmsAsync();
            return Ok(alarms);
        }

        [HttpGet("history")]
        public async Task<ActionResult<List<AlarmDto>>> GetAlarmHistory(
            [FromQuery] DateTime? startDate, 
            [FromQuery] DateTime? endDate)
        {
            var start = startDate ?? DateTime.Now.AddDays(-7);
            var end = endDate ?? DateTime.Now;
            
            var alarms = await _alarmRepository.GetAlarmHistoryAsync(start, end);
            return Ok(alarms);
        }

        [HttpPost("check")]
        public async Task<ActionResult> CheckAlarms()
        {
            var alarms = await _mediator.Send(new CheckAlarmsCommand());
            return Ok(new { Success = true, GeneratedCount = alarms.Count });
        }

        [HttpPost("{id}/acknowledge")]
        public async Task<ActionResult> AcknowledgeAlarm(long id, [FromBody] AcknowledgeRequest request)
        {
            var success = await _mediator.Send(new AcknowledgeAlarmCommand 
            { 
                AlarmId = id, 
                AckBy = request?.AckBy ?? "System" 
            });
            if (!success) return NotFound();
            return Ok(new { Success = true });
        }

        [HttpPost("{id}/clear")]
        public async Task<ActionResult> ClearAlarm(long id)
        {
            await _alarmRepository.UpdateAlarmStatusAsync(id, 0);
            return Ok(new { Success = true });
        }

        [HttpGet("workorders")]
        public async Task<ActionResult<List<WorkOrderDto>>> GetWorkOrders([FromQuery] int? status = null)
        {
            var workOrders = await _alarmRepository.GetWorkOrdersAsync(status);
            return Ok(workOrders);
        }

        [HttpPost("workorders")]
        public async Task<ActionResult<WorkOrder>> CreateWorkOrder([FromBody] WorkOrder workOrder)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            
            var created = await _alarmRepository.CreateWorkOrderAsync(workOrder);
            return CreatedAtAction(nameof(GetWorkOrders), new { id = created.WorkOrderId }, created);
        }

        [HttpPost("workorders/{id}/status")]
        public async Task<ActionResult> UpdateWorkOrderStatus(long id, [FromBody] UpdateStatusRequest request)
        {
            var success = await _mediator.Send(new UpdateWorkOrderStatusCommand
            {
                WorkOrderId = id,
                Status = request.Status,
                Remark = request.Remark,
                Assignee = request.Assignee
            });
            if (!success) return NotFound();
            return Ok(new { Success = true });
        }

        [HttpPost("workorders/{id}/start")]
        public async Task<ActionResult> StartWorkOrder(long id, [FromBody] UpdateStatusRequest request = null)
        {
            var success = await _mediator.Send(new UpdateWorkOrderStatusCommand
            {
                WorkOrderId = id,
                Status = 1,
                Remark = request?.Remark,
                Assignee = request?.Assignee
            });
            if (!success) return NotFound();
            return Ok(new { Success = true });
        }

        [HttpPost("workorders/{id}/complete")]
        public async Task<ActionResult> CompleteWorkOrder(long id, [FromBody] UpdateStatusRequest request = null)
        {
            var success = await _mediator.Send(new UpdateWorkOrderStatusCommand
            {
                WorkOrderId = id,
                Status = 2,
                Remark = request?.Remark,
                Assignee = request?.Assignee
            });
            if (!success) return NotFound();
            return Ok(new { Success = true });
        }

        [HttpPost("workorders/{id}/close")]
        public async Task<ActionResult> CloseWorkOrder(long id, [FromBody] UpdateStatusRequest request = null)
        {
            var success = await _mediator.Send(new UpdateWorkOrderStatusCommand
            {
                WorkOrderId = id,
                Status = 3,
                Remark = request?.Remark,
                Assignee = request?.Assignee
            });
            if (!success) return NotFound();
            return Ok(new { Success = true });
        }

        [HttpPost("{id}/push")]
        public async Task<ActionResult> PushAlarmToWechat(long id)
        {
            var success = await _mediator.Send(new PushAlarmToWechatCommand { AlarmId = id });
            if (!success) return NotFound();
            return Ok(new { Success = true, Message = "Alarm queued for WeChat push" });
        }
    }

    public class AcknowledgeRequest
    {
        public string AckBy { get; set; }
    }

    public class UpdateStatusRequest
    {
        public int Status { get; set; }
        public string Remark { get; set; }
        public string Assignee { get; set; }
    }
}
