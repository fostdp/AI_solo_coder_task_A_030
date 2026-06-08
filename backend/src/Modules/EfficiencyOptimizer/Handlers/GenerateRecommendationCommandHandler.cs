using MediatR;
using ChillerPlantOptimization.Contracts.Commands;
using ChillerPlantOptimization.DTOs;

namespace ChillerPlantOptimization.Modules.EfficiencyOptimizer.Handlers;

public class GenerateRecommendationCommandHandler : IRequestHandler<GenerateRecommendationCommand, OptimizationRecommendationDto?>
{
    private readonly IEfficiencyOptimizer _optimizer;

    public GenerateRecommendationCommandHandler(IEfficiencyOptimizer optimizer)
    {
        _optimizer = optimizer;
    }

    public async Task<OptimizationRecommendationDto?> Handle(GenerateRecommendationCommand request, CancellationToken cancellationToken)
    {
        var recommendation = await _optimizer.GenerateOptimizationRecommendationAsync();
        return recommendation != null ? MapToDto(recommendation) : null;
    }

    private static OptimizationRecommendationDto MapToDto(Models.OptimizationRecommendation r)
    {
        return new OptimizationRecommendationDto
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
            AmbientTemp = r.AmbientTemp,
            Status = r.Status.ToString(),
            StatusText = r.Status switch
            {
                Models.RecommendationStatus.New => "待处理",
                Models.RecommendationStatus.Applied => "已应用",
                Models.RecommendationStatus.Rejected => "已拒绝",
                _ => "未知"
            },
            AppliedAt = r.AppliedAt,
            AppliedBy = r.AppliedBy,
            ModelVersion = r.ModelVersion
        };
    }
}
