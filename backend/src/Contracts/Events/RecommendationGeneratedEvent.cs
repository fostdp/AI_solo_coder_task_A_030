using MediatR;
using ChillerPlantOptimization.Models;

namespace ChillerPlantOptimization.Contracts.Events;

public class RecommendationGeneratedEvent : INotification
{
    public OptimizationRecommendation Recommendation { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }
}
