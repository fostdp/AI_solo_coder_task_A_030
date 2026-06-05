namespace ChillerPlant.Modules.EfficiencyOptimizer.Configuration
{
    public class OptimizationSettings
    {
        public string ModelWeightsPath { get; set; } = "Data/neural_network_model.txt";
        public int AutoRetrainIntervalMinutes { get; set; } = 300;
        public int EfficiencyCalcIntervalSeconds { get; set; } = 30;
        public int TrainingEpochs { get; set; } = 200;
        public int MinTrainingSamples { get; set; } = 50;
        public int TrainingDataHours { get; set; } = 72;
    }
}
