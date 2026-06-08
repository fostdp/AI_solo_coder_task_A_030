using Microsoft.AspNetCore.Mvc;
using ChillerPlantOptimization.DTOs;
using ChillerPlantOptimization.Services;

namespace ChillerPlantOptimization.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EfficiencyController : ControllerBase
{
    private readonly IEfficiencyService _efficiencyService;
    private readonly ILogger<EfficiencyController> _logger;

    public EfficiencyController(
        IEfficiencyService efficiencyService,
        ILogger<EfficiencyController> logger)
    {
        _efficiencyService = efficiencyService;
        _logger = logger;
    }

    [HttpGet("current")]
    public async Task<ActionResult<SystemMetricsDto>> GetCurrentMetrics()
    {
        var metrics = await _efficiencyService.GetCurrentSystemMetricsAsync();
        if (metrics == null)
        {
            return NotFound();
        }

        var dto = new SystemMetricsDto
        {
            Timestamp = metrics.Timestamp,
            DailyEnergy = metrics.DailyEnergyConsumption,
            RealtimeCOP = metrics.SystemCOP,
            EnergySaving = metrics.EnergySaving,
            PeakPower = metrics.PeakPower,
            RunningDeviceCount = metrics.RunningDeviceCount,
            TotalDeviceCount = metrics.TotalDeviceCount
        };

        return Ok(dto);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<EfficiencyRecordDto>>> GetEfficiencyHistory(
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null)
    {
        var actualStartTime = startTime ?? DateTime.UtcNow.AddDays(-7);
        var actualEndTime = endTime ?? DateTime.UtcNow;

        var records = await _efficiencyService.GetEfficiencyHistoryAsync(actualStartTime, actualEndTime);
        var dtos = records.Select(r => new EfficiencyRecordDto
        {
            Timestamp = r.Timestamp,
            SystemCOP = r.SystemCOP,
            DesignCOP = r.DesignCOP,
            DesignCOPRatio = r.DesignCOPRatio,
            TotalPower = r.TotalPower,
            TotalCoolingCapacity = r.TotalCoolingCapacity,
            ChilledWaterSupplyTemp = r.ChilledWaterSupplyTemp,
            ChilledWaterReturnTemp = r.ChilledWaterReturnTemp,
            CoolingWaterSupplyTemp = r.CoolingWaterSupplyTemp,
            CoolingWaterReturnTemp = r.CoolingWaterReturnTemp,
            LoadRate = r.LoadRate,
            DailyEnergyConsumption = r.DailyEnergyConsumption,
            EnergySaving = r.EnergySaving
        });

        return Ok(dtos);
    }

    [HttpGet("today")]
    public async Task<ActionResult<IEnumerable<EfficiencyRecordDto>>> GetTodayEfficiency()
    {
        var records = await _efficiencyService.GetTodayEfficiencyAsync();
        var dtos = records.Select(r => new EfficiencyRecordDto
        {
            Timestamp = r.Timestamp,
            SystemCOP = r.SystemCOP,
            DesignCOP = r.DesignCOP,
            DesignCOPRatio = r.DesignCOPRatio,
            TotalPower = r.TotalPower,
            TotalCoolingCapacity = r.TotalCoolingCapacity,
            ChilledWaterSupplyTemp = r.ChilledWaterSupplyTemp,
            ChilledWaterReturnTemp = r.ChilledWaterReturnTemp,
            CoolingWaterSupplyTemp = r.CoolingWaterSupplyTemp,
            CoolingWaterReturnTemp = r.CoolingWaterReturnTemp,
            LoadRate = r.LoadRate,
            DailyEnergyConsumption = r.DailyEnergyConsumption,
            EnergySaving = r.EnergySaving
        });

        return Ok(dtos);
    }

    [HttpGet("reports")]
    public async Task<ActionResult<IEnumerable<DiagnosisReportDto>>> GetDiagnosisReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var reports = await _efficiencyService.GetDiagnosisReportsAsync(page, pageSize);
        var dtos = reports.Select(r => new DiagnosisReportDto
        {
            Id = r.Id,
            GeneratedAt = r.GeneratedAt,
            ReportDate = r.ReportDate,
            SystemAverageCOP = r.SystemAverageCOP,
            DesignCOPRatio = r.DesignCOPRatio,
            TotalEnergyConsumption = r.TotalEnergyConsumption,
            TotalEnergySaving = r.TotalEnergySaving,
            LowEfficiencyDevices = r.LowEfficiencyDevices,
            DiagnosisContent = r.DiagnosisContent,
            Recommendations = r.Recommendations
        });

        return Ok(dtos);
    }

    [HttpGet("reports/{id}")]
    public async Task<ActionResult<DiagnosisReportDto>> GetDiagnosisReportById(long id)
    {
        var report = await _efficiencyService.GetDiagnosisReportByIdAsync(id);
        if (report == null)
        {
            return NotFound();
        }

        var dto = new DiagnosisReportDto
        {
            Id = report.Id,
            GeneratedAt = report.GeneratedAt,
            ReportDate = report.ReportDate,
            SystemAverageCOP = report.SystemAverageCOP,
            DesignCOPRatio = report.DesignCOPRatio,
            TotalEnergyConsumption = report.TotalEnergyConsumption,
            TotalEnergySaving = report.TotalEnergySaving,
            LowEfficiencyDevices = report.LowEfficiencyDevices,
            DiagnosisContent = report.DiagnosisContent,
            Recommendations = report.Recommendations
        };

        return Ok(dto);
    }

    [HttpPost("calculate")]
    public async Task<ActionResult> CalculateCOP()
    {
        var timestamp = DateTime.UtcNow;
        var result = await _efficiencyService.CalculateAndSaveSystemCOPAsync(timestamp);
        if (result != null)
        {
            return Ok(new { success = true, cop = result.SystemCOP });
        }
        return BadRequest(new { success = false, message = "COP计算失败，数据不足" });
    }
}
