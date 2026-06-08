using MediatR;
using ChillerPlantOptimization.DTOs;

namespace ChillerPlantOptimization.Contracts.Commands;

public class CalculateCOPCommand : IRequest<EfficiencyRecordDto?>
{
    public DateTime Timestamp { get; set; }
}
