using Microsoft.AspNetCore.Mvc;
using ChillerPlantOptimization.DTOs;
using ChillerPlantOptimization.Services;

namespace ChillerPlantOptimization.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OptimizationController : ControllerBase
{
    private readonly IOptimizationModelService _optimizationService;
    private readonly ILogger<OptimizationController> _logger;

    public OptimizationController(
        IOptimizationModelService optimizationService,
        ILogger<OptimizationController> logger)
    {
        _optimizationService = optimizationService;
        _logger = logger;
    }

    [HttpGet("current")]
    public async Task<ActionResult<OptimizationRecommendationDto>> GetCurrentRecommendation()
    {
        var recommendation = await _optimizationService.GetLatestRecommendationAsync();
        if (recommendation == null)
        {
            return NotFound();
        }

        var dto = new OptimizationRecommendationDto
        {
            Id = recommendation.Id,
            GeneratedAt = recommendation.GeneratedAt,
            DeviceCombination = recommendation.DeviceCombination,
            RunningChillers = recommendation.RunningChillers,
            RunningPumps = recommendation.RunningPumps,
            RunningTowers = recommendation.RunningTowers,
            PredictedCOP = recommendation.PredictedCOP,
            PredictedPower = recommendation.PredictedPower,
            ChilledWaterSetpoint = recommendation.ChilledWaterSetpoint,
            ExpectedEnergySaving = recommendation.ExpectedEnergySaving,
            ExpectedSavingPercent = recommendation.ExpectedSavingPercent,
            LoadRate = recommendation.LoadRate,
            Status = (int)recommendation.Status
        };

        return Ok(dto);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<OptimizationRecommendationDto>>> GetRecommendationHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var recommendations = await _optimizationService.GetRecommendationHistoryAsync(page, pageSize);
        var dtos = recommendations.Select(r => new OptimizationRecommendationDto
        {
            Id = r.Id,
            GeneratedAt = r.GeneratedAt,
            DeviceCombination = r.DeviceCombination,
            RunningChillers = r.RunningChillers,
            RunningPumps = r.RunningPumps,
            RunningTowers = r.RunningTowers,
            PredictedCOP = r.PredictedCOP,
            PredictedPower = r.PredictedPower,
            ChilledWaterSetpoint = r.ChilledWaterSetpoint,
            ExpectedEnergySaving = r.ExpectedEnergySaving,
            ExpectedSavingPercent = r.ExpectedSavingPercent,
            LoadRate = r.LoadRate,
            Status = (int)r.Status
        });

        return Ok(dtos);
    }

    [HttpPost("generate")]
    public async Task<ActionResult<OptimizationRecommendationDto>> GenerateRecommendation()
    {
        var recommendation = await _optimizationService.GenerateOptimizationRecommendationAsync();
        if (recommendation == null)
        {
            return BadRequest(new { success = false, message = "无法生成优化建议，数据不足" });
        }

        var dto = new OptimizationRecommendationDto
        {
            Id = recommendation.Id,
            GeneratedAt = recommendation.GeneratedAt,
            DeviceCombination = recommendation.DeviceCombination,
            RunningChillers = recommendation.RunningChillers,
            RunningPumps = recommendation.RunningPumps,
            RunningTowers = recommendation.RunningTowers,
            PredictedCOP = recommendation.PredictedCOP,
            PredictedPower = recommendation.PredictedPower,
            ChilledWaterSetpoint = recommendation.ChilledWaterSetpoint,
            ExpectedEnergySaving = recommendation.ExpectedEnergySaving,
            ExpectedSavingPercent = recommendation.ExpectedSavingPercent,
            LoadRate = recommendation.LoadRate,
            Status = (int)recommendation.Status
        };

        return Ok(dto);
    }

    [HttpPost("train")]
    public async Task<ActionResult> TrainModel()
    {
        try
        {
            await _optimizationService.TrainModelAsync();
            return Ok(new { success = true, message = "模型训练完成" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模型训练失败");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("model/performance")]
    public async Task<ActionResult> GetModelPerformance()
    {
        var performance = await _optimizationService.GetModelPerformanceAsync();
        if (performance == null)
        {
            return NotFound();
        }
        return Ok(performance);
    }

    [HttpPost("apply")]
    public async Task<ActionResult> ApplyRecommendation([FromBody] OptimizeRequestDto request)
    {
        var result = await _optimizationService.ApplyRecommendationAsync(request.RecommendationId, request.AppliedBy);
        if (result)
        {
            return Ok(new { success = true, message = "优化方案已应用" });
        }
        return BadRequest(new { success = false, message = "应用失败" });
    }

    [HttpPost("{id}/reject")]
    public async Task<ActionResult> RejectRecommendation(long id, [FromBody] OptimizeRequestDto request)
    {
        var result = await _optimizationService.RejectRecommendationAsync(id, request.AppliedBy);
        if (result)
        {
            return Ok(new { success = true, message = "已拒绝优化方案" });
        }
        return BadRequest(new { success = false, message = "操作失败" });
    }
}
