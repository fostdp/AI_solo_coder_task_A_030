using MediatR;
using ChillerPlantOptimization.Models;

namespace ChillerPlantOptimization.Contracts.Events;

public class AlarmTriggeredEvent : INotification
{
    public Alarm Alarm { get; set; } = null!;
    public DateTime TriggeredAt { get; set; }
}
