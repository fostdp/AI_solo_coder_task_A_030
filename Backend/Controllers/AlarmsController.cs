using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ChillerPlant.Data.Repositories;
using ChillerPlant.Models;

namespace ChillerPlant.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlarmsController : ControllerBase
    {
        private readonly IAlarmRepository _alarmRepository;

        public AlarmsController(IAlarmRepository alarmRepository)
        {
            _alarmRepository = alarmRepository;
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
            await _alarmRepository.CheckAndCreateAlarmsAsync();
            return Ok(new { Success = true });
        }

        [HttpPost("{id}/acknowledge")]
        public async Task<ActionResult> AcknowledgeAlarm(long id, [FromBody] AcknowledgeRequest request)
        {
            await _alarmRepository.AcknowledgeAlarmAsync(id, request?.AckBy ?? "System");
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
            await _alarmRepository.UpdateWorkOrderStatusAsync(id, request.Status, request.Remark);
            return Ok(new { Success = true });
        }

        [HttpPost("workorders/{id}/start")]
        public async Task<ActionResult> StartWorkOrder(long id)
        {
            await _alarmRepository.UpdateWorkOrderStatusAsync(id, 1);
            return Ok(new { Success = true });
        }

        [HttpPost("workorders/{id}/complete")]
        public async Task<ActionResult> CompleteWorkOrder(long id, [FromBody] string remark = null)
        {
            await _alarmRepository.UpdateWorkOrderStatusAsync(id, 2, remark);
            return Ok(new { Success = true });
        }

        [HttpPost("workorders/{id}/close")]
        public async Task<ActionResult> CloseWorkOrder(long id, [FromBody] string remark = null)
        {
            await _alarmRepository.UpdateWorkOrderStatusAsync(id, 3, remark);
            return Ok(new { Success = true });
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
    }
}
