using Microsoft.AspNetCore.Mvc;
using ChillerPlantOptimization.DTOs;
using ChillerPlantOptimization.Models;
using ChillerPlantOptimization.Services;

namespace ChillerPlantOptimization.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlarmController : ControllerBase
{
    private readonly IAlarmEngineService _alarmService;
    private readonly ILogger<AlarmController> _logger;

    public AlarmController(
        IAlarmEngineService alarmService,
        ILogger<AlarmController> logger)
    {
        _alarmService = alarmService;
        _logger = logger;
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<AlarmDto>>> GetActiveAlarms()
    {
        var alarms = await _alarmService.GetActiveAlarmsAsync();
        var dtos = alarms.Select(a => new AlarmDto
        {
            Id = a.Id,
            DeviceId = a.DeviceId,
            DeviceName = a.Device?.Name,
            AlarmLevel = (int)a.AlarmLevel,
            AlarmType = (int)a.AlarmType,
            Message = a.Message,
            StartTime = a.StartTime,
            EndTime = a.EndTime,
            Status = (int)a.Status,
            DurationMinutes = a.DurationMinutes,
            ParameterName = a.ParameterName,
            ParameterValue = a.ParameterValue,
            ThresholdValue = a.ThresholdValue
        });

        return Ok(dtos);
    }

    [HttpGet("level/{level}")]
    public async Task<ActionResult<IEnumerable<AlarmDto>>> GetAlarmsByLevel(int level)
    {
        var alarms = await _alarmService.GetAlarmsByLevelAsync((AlarmLevel)level);
        var dtos = alarms.Select(a => new AlarmDto
        {
            Id = a.Id,
            DeviceId = a.DeviceId,
            DeviceName = a.Device?.Name,
            AlarmLevel = (int)a.AlarmLevel,
            AlarmType = (int)a.AlarmType,
            Message = a.Message,
            StartTime = a.StartTime,
            EndTime = a.EndTime,
            Status = (int)a.Status,
            DurationMinutes = a.DurationMinutes,
            ParameterName = a.ParameterName,
            ParameterValue = a.ParameterValue,
            ThresholdValue = a.ThresholdValue
        });

        return Ok(dtos);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<AlarmDto>>> GetAlarmHistory(
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var actualStartTime = startTime ?? DateTime.UtcNow.AddDays(-7);
        var actualEndTime = endTime ?? DateTime.UtcNow;

        var alarms = await _alarmService.GetAlarmHistoryAsync(actualStartTime, actualEndTime, page, pageSize);
        var dtos = alarms.Select(a => new AlarmDto
        {
            Id = a.Id,
            DeviceId = a.DeviceId,
            DeviceName = a.Device?.Name,
            AlarmLevel = (int)a.AlarmLevel,
            AlarmType = (int)a.AlarmType,
            Message = a.Message,
            StartTime = a.StartTime,
            EndTime = a.EndTime,
            Status = (int)a.Status,
            DurationMinutes = a.DurationMinutes,
            ParameterName = a.ParameterName,
            ParameterValue = a.ParameterValue,
            ThresholdValue = a.ThresholdValue
        });

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AlarmDto>> GetAlarmById(long id)
    {
        var alarm = await _alarmService.GetAlarmByIdAsync(id);
        if (alarm == null)
        {
            return NotFound();
        }

        var dto = new AlarmDto
        {
            Id = alarm.Id,
            DeviceId = alarm.DeviceId,
            DeviceName = alarm.Device?.Name,
            AlarmLevel = (int)alarm.AlarmLevel,
            AlarmType = (int)alarm.AlarmType,
            Message = alarm.Message,
            StartTime = alarm.StartTime,
            EndTime = alarm.EndTime,
            Status = (int)alarm.Status,
            DurationMinutes = alarm.DurationMinutes,
            ParameterName = alarm.ParameterName,
            ParameterValue = alarm.ParameterValue,
            ThresholdValue = alarm.ThresholdValue
        };

        return Ok(dto);
    }

    [HttpPut("{id}/acknowledge")]
    public async Task<ActionResult> AcknowledgeAlarm(long id, [FromBody] AcknowledgeAlarmRequestDto request)
    {
        var result = await _alarmService.AcknowledgeAlarmAsync(id, request.AcknowledgedBy);
        if (result)
        {
            return Ok(new { success = true, message = "告警已确认" });
        }
        return BadRequest(new { success = false, message = "确认失败" });
    }

    [HttpPut("{id}/clear")]
    public async Task<ActionResult> ClearAlarm(long id, [FromBody] AcknowledgeAlarmRequestDto request)
    {
        var result = await _alarmService.ClearAlarmAsync(id, request.AcknowledgedBy);
        if (result)
        {
            return Ok(new { success = true, message = "告警已清除" });
        }
        return BadRequest(new { success = false, message = "清除失败" });
    }

    [HttpGet("thresholds")]
    public async Task<ActionResult> GetAlarmThresholds()
    {
        var thresholds = await _alarmService.GetAllThresholdsAsync();
        return Ok(thresholds);
    }

    [HttpPut("thresholds/{id}")]
    public async Task<ActionResult> UpdateThreshold(long id, [FromBody] decimal value)
    {
        var result = await _alarmService.UpdateThresholdAsync(id, value);
        if (result)
        {
            return Ok(new { success = true, message = "阈值已更新" });
        }
        return BadRequest(new { success = false, message = "更新失败" });
    }
}
