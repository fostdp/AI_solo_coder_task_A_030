using MediatR;
using ChillerPlantOptimization.DTOs;

namespace ChillerPlantOptimization.Contracts.Commands;

public class GenerateRecommendationCommand : IRequest<OptimizationRecommendationDto?>
{
}
