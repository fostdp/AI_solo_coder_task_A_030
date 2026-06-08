using MediatR;
using ChillerPlantOptimization.Contracts.Commands;
using ChillerPlantOptimization.DTOs;

namespace ChillerPlantOptimization.Modules.EfficiencyOptimizer.Handlers;

public class CalculateCOPCommandHandler : IRequestHandler<CalculateCOPCommand, EfficiencyRecordDto?>
{
    private readonly IEfficiencyOptimizer _optimizer;

    public CalculateCOPCommandHandler(IEfficiencyOptimizer optimizer)
    {
        _optimizer = optimizer;
    }

    public async Task<EfficiencyRecordDto?> Handle(CalculateCOPCommand request, CancellationToken cancellationToken)
    {
        var record = await _optimizer.CalculateAndSaveSystemCOPAsync(request.Timestamp);
        return record != null ? MapToDto(record) : null;
    }

    private static EfficiencyRecordDto MapToDto(Models.EfficiencyRecord record)
    {
        return new EfficiencyRecordDto
        {
            Id = record.Id,
            Timestamp = record.Timestamp,
            TotalPower = record.TotalPower,
            TotalCoolingCapacity = record.TotalCoolingCapacity,
            SystemCOP = record.SystemCOP,
            DesignCOP = record.DesignCOP,
            EfficiencyRatio = record.EfficiencyRatio,
            ChilledWaterSupplyTemp = record.ChilledWaterSupplyTemp,
            ChilledWaterReturnTemp = record.ChilledWaterReturnTemp,
            CoolingWaterSupplyTemp = record.CoolingWaterSupplyTemp,
            CoolingWaterReturnTemp = record.CoolingWaterReturnTemp,
            FlowRate = record.FlowRate,
            LoadRate = record.LoadRate,
            RunningChillerCount = record.RunningChillerCount,
            RunningPumpCount = record.RunningPumpCount,
            RunningTowerCount = record.RunningTowerCount,
            DailyEnergyConsumption = record.DailyEnergyConsumption,
            EnergySaving = record.EnergySaving
        };
    }
}
