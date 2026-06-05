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
    public class EfficiencyController : ControllerBase
    {
        private readonly IEfficiencyRepository _efficiencyRepository;
        private readonly IOptimizationRepository _optimizationRepository;
        private readonly IMediator _mediator;

        public EfficiencyController(IEfficiencyRepository efficiencyRepository,
            IOptimizationRepository optimizationRepository,
            IMediator mediator)
        {
            _efficiencyRepository = efficiencyRepository;
            _optimizationRepository = optimizationRepository;
            _mediator = mediator;
        }

        [HttpGet("realtime")]
        public async Task<ActionResult<SystemEfficiency>> GetRealtimeEfficiency()
        {
            var efficiency = await _efficiencyRepository.GetLatestSystemEfficiencyAsync();
            return Ok(efficiency);
        }

        [HttpGet("daily")]
        public async Task<ActionResult<EnergyStatisticsDto>> GetDailyStatistics([FromQuery] DateTime? date = null)
        {
            var stats = await _efficiencyRepository.GetDailyEnergyStatisticsAsync(date);
            return Ok(stats);
        }

        [HttpGet("range")]
        public async Task<ActionResult<List<EnergyStatisticsDto>>> GetStatisticsByRange([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            var stats = await _efficiencyRepository.GetEnergyStatisticsByDateRangeAsync(startDate, endDate);
            return Ok(stats);
        }

        [HttpGet("optimization/latest")]
        public async Task<ActionResult<OptimizationRecommendationDto>> GetLatestRecommendation()
        {
            var recommendation = await _optimizationRepository.GetLatestRecommendationAsync();
            if (recommendation == null) return NotFound();
            return Ok(recommendation);
        }

        [HttpGet("optimization/history")]
        public async Task<ActionResult<List<OptimizationRecommendationDto>>> GetRecommendationHistory([FromQuery] int count = 20)
        {
            var history = await _optimizationRepository.GetRecommendationHistoryAsync(count);
            return Ok(history);
        }

        [HttpPost("optimization/generate")]
        public async Task<ActionResult<OptimizationRecommendationDto>> GenerateOptimization()
        {
            var recommendation = await _mediator.Send(new GenerateOptimizationCommand());
            if (recommendation == null) return BadRequest("Unable to generate optimization recommendation");
            return Ok(recommendation);
        }

        [HttpPost("optimization/{id}/implement")]
        public async Task<ActionResult> ImplementRecommendation(long id)
        {
            await _optimizationRepository.ImplementRecommendationAsync(id);
            return Ok(new { Success = true });
        }

        [HttpPost("optimization/train")]
        public async Task<ActionResult> TrainModel([FromBody] TrainModelRequest request = null)
        {
            var epochs = request?.Epochs ?? 200;
            var success = await _mediator.Send(new TrainOptimizationModelCommand { Epochs = epochs });
            if (!success) return BadRequest("Insufficient training data or training failed");
            return Ok(new { Success = true, Message = "Model training completed" });
        }

        [HttpGet("optimization/predict")]
        public async Task<ActionResult<List<double>>> PredictCombinations(
            [FromQuery] double loadRate, 
            [FromQuery] double outdoorTemp, 
            [FromQuery] double wetBulbTemp)
        {
            var predictions = await _optimizationRepository.PredictAllCombinationsCOP(loadRate, outdoorTemp, wetBulbTemp);
            return Ok(predictions);
        }

        [HttpGet("diagnosis/recent")]
        public async Task<ActionResult<List<EnergyDiagnosisReport>>> GetRecentReports([FromQuery] int count = 10)
        {
            var reports = await _efficiencyRepository.GetRecentDiagnosisReportsAsync(count);
            return Ok(reports);
        }

        [HttpPost("diagnosis/generate")]
        public async Task<ActionResult<EnergyDiagnosisReport>> GenerateDiagnosisReport([FromQuery] DateTime? reportDate = null)
        {
            var report = await _efficiencyRepository.GenerateEnergyDiagnosisReportAsync(reportDate);
            return Ok(report);
        }

        [HttpPost("energy/hourly/update")]
        public async Task<ActionResult> UpdateHourlyEnergy()
        {
            await _efficiencyRepository.UpdateHourlyEnergyConsumptionAsync();
            return Ok(new { Success = true });
        }

        [HttpPost("energy/daily/update")]
        public async Task<ActionResult> UpdateDailyEnergy()
        {
            await _efficiencyRepository.UpdateDailyEnergyConsumptionAsync();
            return Ok(new { Success = true });
        }
    }

    public class TrainModelRequest
    {
        public int Epochs { get; set; } = 200;
    }
}
