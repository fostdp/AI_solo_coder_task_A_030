using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChillerPlant.Models;
using ChillerPlant.Services;

namespace ChillerPlant.Data.Repositories
{
    public interface IOptimizationRepository
    {
        Task<OptimizationRecommendationDto> GetLatestRecommendationAsync();
        Task<List<OptimizationRecommendationDto>> GetRecommendationHistoryAsync(int count = 20);
        Task<OptimizationRecommendation> GenerateOptimizationAsync();
        Task ImplementRecommendationAsync(long recommendationId);
        Task TrainModelAsync();
        Task<ChillerCombination> FindOptimalCombinationAsync(double loadRate, double outdoorTemp, double wetBulbTemp);
        Task<List<double>> PredictAllCombinationsCOP(double loadRate, double outdoorTemp, double wetBulbTemp);
    }
}
