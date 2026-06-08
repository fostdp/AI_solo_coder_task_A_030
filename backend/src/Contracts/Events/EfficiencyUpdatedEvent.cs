using MediatR;
using ChillerPlantOptimization.Models;

namespace ChillerPlantOptimization.Contracts.Events;

public class EfficiencyUpdatedEvent : INotification
{
    public EfficiencyRecord EfficiencyRecord { get; set; } = null!;
    public DateTime CalculatedAt { get; set; }
}
