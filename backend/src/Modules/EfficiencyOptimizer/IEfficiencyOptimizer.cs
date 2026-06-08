using ChillerPlantOptimization.Models;

namespace ChillerPlantOptimization.Modules.EfficiencyOptimizer;

public interface IEfficiencyOptimizer
{
    Task TrainModelAsync();
    Task<OptimizationRecommendation> GenerateOptimizationRecommendationAsync();
    Task<OptimizationRecommendation?> GetLatestRecommendationAsync();
    Task<EfficiencyRecord> CalculateAndSaveSystemCOPAsync(DateTime timestamp);
    Task UpdateDeviceEfficiencyStatusAsync();
    Task ApplyRecommendationAsync(long id, string appliedBy);
}
