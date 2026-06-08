using MediatR;
using ChillerPlantOptimization.Models;

namespace ChillerPlantOptimization.Contracts.Events;

public class DeviceDataCollectedEvent : INotification
{
    public IEnumerable<DeviceData> DeviceData { get; set; } = Enumerable.Empty<DeviceData>();
    public DateTime CollectedAt { get; set; }
}
