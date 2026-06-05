namespace ChillerPlant.Modules.EfficiencyOptimizer.Models
{
    public class OptimizationInput
    {
        public double OutdoorTemp { get; set; }
        public double WetBulbTemp { get; set; }
        public int ChillerCount { get; set; }
        public double SupplyWaterTemp { get; set; }
        public double CoolingWaterInTemp { get; set; }
        public double LoadRate { get; set; }
    }

    public class OptimizationRecommendation
    {
        public long RecommendationId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public double CurrentCOP { get; set; }
        public double PredictedOptimalCOP { get; set; }
        public double CurrentSupplyTemp { get; set; }
        public double OptimalSupplyTemp { get; set; }
        public int CurrentChillerCount { get; set; }
        public int OptimalChillerCount { get; set; }
        public double ExpectedEnergySaving { get; set; }
        public string RecommendationType { get; set; }
        public string Description { get; set; }
        public bool IsImplemented { get; set; }
        public DateTime? ImplementedAt { get; set; }
        public double? ActualEnergySaving { get; set; }
    }

    public class SystemEfficiencyData
    {
        public DateTime Timestamp { get; set; }
        public decimal SystemCOP { get; set; }
        public decimal DesignCOP { get; set; }
        public decimal COPRatio { get; set; }
        public decimal TotalPower { get; set; }
        public decimal TotalCooling { get; set; }
        public int RunningChillerCount { get; set; }
        public int RunningPumpCount { get; set; }
        public int RunningTowerCount { get; set; }
        public decimal OutdoorTemp { get; set; }
        public decimal WetBulbTemp { get; set; }
    }
}
