using MediatR;

namespace ChillerPlantOptimization.Contracts.Commands;

public class CheckAlarmsCommand : IRequest<int>
{
    public DateTime Timestamp { get; set; }
}
