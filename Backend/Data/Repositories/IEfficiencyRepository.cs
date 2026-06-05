using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChillerPlant.Models;

namespace ChillerPlant.Data.Repositories
{
    public interface IEfficiencyRepository
    {
        Task<SystemEfficiency> InsertSystemEfficiencyAsync(SystemEfficiency efficiency);
        Task<SystemEfficiency> GetLatestSystemEfficiencyAsync();
        Task<RealtimeDashboardDto> GetRealtimeDashboardAsync();
        Task<EnergyStatisticsDto> GetDailyEnergyStatisticsAsync(DateTime? date = null);
        Task<List<EnergyStatisticsDto>> GetEnergyStatisticsByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task InsertEnergyConsumptionAsync(EnergyConsumption consumption);
        Task<EnergyDiagnosisReport> GenerateEnergyDiagnosisReportAsync(DateTime? reportDate = null);
        Task<List<EnergyDiagnosisReport>> GetRecentDiagnosisReportsAsync(int count = 10);
        Task UpdateHourlyEnergyConsumptionAsync();
        Task UpdateDailyEnergyConsumptionAsync();
    }
}
