using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ChillerPlant.Models;

namespace ChillerPlant.Services
{
    public class NeuralNetworkModel
    {
        private double[][] _weightsInputHidden;
        private double[] _weightsHiddenOutput;
        private double[] _hiddenBias;
        private double _outputBias;
        private readonly int _inputNodes = 6;
        private readonly int _hiddenNodes = 12;
        private readonly double _learningRate = 0.01;
        private readonly Random _random = new Random();

        public NeuralNetworkModel()
        {
            InitializeWeights();
        }

        private void InitializeWeights()
        {
            _weightsInputHidden = new double[_inputNodes][];
            for (int i = 0; i < _inputNodes; i++)
            {
                _weightsInputHidden[i] = new double[_hiddenNodes];
                for (int j = 0; j < _hiddenNodes; j++)
                {
                    _weightsInputHidden[i][j] = (_random.NextDouble() - 0.5) * 0.1;
                }
            }

            _weightsHiddenOutput = new double[_hiddenNodes];
            for (int i = 0; i < _hiddenNodes; i++)
            {
                _weightsHiddenOutput[i] = (_random.NextDouble() - 0.5) * 0.1;
            }

            _hiddenBias = new double[_hiddenNodes];
            for (int i = 0; i < _hiddenNodes; i++)
            {
                _hiddenBias[i] = (_random.NextDouble() - 0.5) * 0.1;
            }

            _outputBias = (_random.NextDouble() - 0.5) * 0.1;
        }

        private double Sigmoid(double x)
        {
            return 1.0 / (1.0 + Math.Exp(-x));
        }

        private double SigmoidDerivative(double x)
        {
            return x * (1.0 - x);
        }

        private double[] NormalizeInputs(double[] inputs)
        {
            var normalized = new double[inputs.Length];
            
            var ranges = new[]
            {
                new { Min = -10.0, Max = 45.0 },      // 室外温度
                new { Min = -5.0, Max = 35.0 },       // 湿球温度
                new { Min = 0.0, Max = 5.0 },         // 冷水机组数量
                new { Min = 4.0, Max = 12.0 },        // 冷冻水出水温度
                new { Min = 20.0, Max = 40.0 },       // 冷却水进水温度
                new { Min = 0.0, Max = 100.0 }        // 负荷率
            };

            for (int i = 0; i < inputs.Length && i < ranges.Length; i++)
            {
                normalized[i] = (inputs[i] - ranges[i].Min) / (ranges[i].Max - ranges[i].Min);
                normalized[i] = Math.Max(0.01, Math.Min(0.99, normalized[i]));
            }

            return normalized;
        }

        public double Predict(double[] inputs)
        {
            var normalizedInputs = NormalizeInputs(inputs);
            
            var hiddenLayer = new double[_hiddenNodes];
            for (int j = 0; j < _hiddenNodes; j++)
            {
                double sum = _hiddenBias[j];
                for (int i = 0; i < _inputNodes; i++)
                {
                    sum += normalizedInputs[i] * _weightsInputHidden[i][j];
                }
                hiddenLayer[j] = Sigmoid(sum);
            }

            double output = _outputBias;
            for (int i = 0; i < _hiddenNodes; i++)
            {
                output += hiddenLayer[i] * _weightsHiddenOutput[i];
            }

            return Math.Max(0.1, output * 10.0);
        }

        public void Train(double[] inputs, double target)
        {
            var normalizedInputs = NormalizeInputs(inputs);
            var normalizedTarget = target / 10.0;
            normalizedTarget = Math.Max(0.01, Math.Min(0.99, normalizedTarget));

            var hiddenLayer = new double[_hiddenNodes];
            for (int j = 0; j < _hiddenNodes; j++)
            {
                double sum = _hiddenBias[j];
                for (int i = 0; i < _inputNodes; i++)
                {
                    sum += normalizedInputs[i] * _weightsInputHidden[i][j];
                }
                hiddenLayer[j] = Sigmoid(sum);
            }

            double output = _outputBias;
            for (int i = 0; i < _hiddenNodes; i++)
            {
                output += hiddenLayer[i] * _weightsHiddenOutput[i];
            }
            output = Sigmoid(output);

            double outputError = normalizedTarget - output;
            double outputDelta = outputError * SigmoidDerivative(output);

            var hiddenDeltas = new double[_hiddenNodes];
            for (int j = 0; j < _hiddenNodes; j++)
            {
                hiddenDeltas[j] = outputDelta * _weightsHiddenOutput[j] * SigmoidDerivative(hiddenLayer[j]);
            }

            for (int i = 0; i < _hiddenNodes; i++)
            {
                _weightsHiddenOutput[i] += _learningRate * outputDelta * hiddenLayer[i];
            }
            _outputBias += _learningRate * outputDelta;

            for (int i = 0; i < _inputNodes; i++)
            {
                for (int j = 0; j < _hiddenNodes; j++)
                {
                    _weightsInputHidden[i][j] += _learningRate * hiddenDeltas[j] * normalizedInputs[i];
                }
            }
            for (int j = 0; j < _hiddenNodes; j++)
            {
                _hiddenBias[j] += _learningRate * hiddenDeltas[j];
            }
        }

        public void TrainBatch(List<TrainingSample> samples, int epochs = 100)
        {
            for (int epoch = 0; epoch < epochs; epoch++)
            {
                double totalError = 0;
                foreach (var sample in samples)
                {
                    var inputs = sample.GetInputs();
                    double prediction = Predict(inputs);
                    double error = Math.Abs(sample.ActualCOP - prediction);
                    totalError += error;
                    Train(inputs, sample.ActualCOP);
                }
                
                if (epoch % 20 == 0)
                {
                    Console.WriteLine($"Epoch {epoch}, Average Error: {totalError / samples.Count:F4}");
                }
            }
        }

        public void SaveModel(string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine($"{_inputNodes},{_hiddenNodes}");
                
                for (int i = 0; i < _inputNodes; i++)
                {
                    writer.WriteLine(string.Join(",", _weightsInputHidden[i]));
                }
                
                writer.WriteLine(string.Join(",", _weightsHiddenOutput));
                writer.WriteLine(string.Join(",", _hiddenBias));
                writer.WriteLine(_outputBias);
            }
        }

        public void LoadModel(string filePath)
        {
            if (!File.Exists(filePath)) return;

            var lines = File.ReadAllLines(filePath);
            int lineIndex = 0;
            
            var dimensions = lines[lineIndex++].Split(',');
            
            for (int i = 0; i < _inputNodes; i++)
            {
                var weights = lines[lineIndex++].Split(',');
                for (int j = 0; j < _hiddenNodes && j < weights.Length; j++)
                {
                    _weightsInputHidden[i][j] = double.Parse(weights[j]);
                }
            }
            
            var outputWeights = lines[lineIndex++].Split(',');
            for (int i = 0; i < _hiddenNodes && i < outputWeights.Length; i++)
            {
                _weightsHiddenOutput[i] = double.Parse(outputWeights[i]);
            }
            
            var hiddenBiases = lines[lineIndex++].Split(',');
            for (int i = 0; i < _hiddenNodes && i < hiddenBiases.Length; i++)
            {
                _hiddenBias[i] = double.Parse(hiddenBiases[i]);
            }
            
            _outputBias = double.Parse(lines[lineIndex++]);
        }
    }

    public class TrainingSample
    {
        public double OutdoorTemp { get; set; }
        public double WetBulbTemp { get; set; }
        public int ChillerCount { get; set; }
        public double SupplyWaterTemp { get; set; }
        public double CoolingWaterInTemp { get; set; }
        public double LoadRate { get; set; }
        public double ActualCOP { get; set; }

        public double[] GetInputs()
        {
            return new[] 
            { 
                OutdoorTemp, 
                WetBulbTemp, 
                ChillerCount, 
                SupplyWaterTemp, 
                CoolingWaterInTemp, 
                LoadRate 
            };
        }
    }

    public class DecisionTreeModel
    {
        private List<DecisionNode> _rules;

        public DecisionTreeModel()
        {
            InitializeRules();
        }

        private void InitializeRules()
        {
            _rules = new List<DecisionNode>
            {
                new DecisionNode 
                { 
                    Condition = sample => sample.LoadRate > 80,
                    ChillerCount = 3,
                    SupplyTemp = 5.5,
                    BaseCOP = 6.0
                },
                new DecisionNode 
                { 
                    Condition = sample => sample.LoadRate > 60 && sample.LoadRate <= 80,
                    ChillerCount = 2,
                    SupplyTemp = 6.0,
                    BaseCOP = 6.2
                },
                new DecisionNode 
                { 
                    Condition = sample => sample.LoadRate > 40 && sample.LoadRate <= 60,
                    ChillerCount = 2,
                    SupplyTemp = 6.5,
                    BaseCOP = 5.8
                },
                new DecisionNode 
                { 
                    Condition = sample => sample.LoadRate > 25 && sample.LoadRate <= 40,
                    ChillerCount = 1,
                    SupplyTemp = 7.0,
                    BaseCOP = 5.5
                },
                new DecisionNode 
                { 
                    Condition = sample => sample.LoadRate > 15 && sample.LoadRate <= 25,
                    ChillerCount = 1,
                    SupplyTemp = 7.5,
                    BaseCOP = 5.0
                },
                new DecisionNode 
                { 
                    Condition = sample => sample.LoadRate <= 15,
                    ChillerCount = 1,
                    SupplyTemp = 8.0,
                    BaseCOP = 4.5
                }
            };
        }

        public double Predict(TrainingSample sample)
        {
            var rule = _rules.FirstOrDefault(r => r.Condition(sample)) ?? _rules.Last();
            
            double tempAdjust = (7.0 - sample.SupplyWaterTemp) * 0.1;
            double coolingAdjust = (30.0 - sample.CoolingWaterInTemp) * 0.05;
            double outdoorAdjust = (25.0 - sample.OutdoorTemp) * 0.02;
            
            return rule.BaseCOP + tempAdjust + coolingAdjust + outdoorAdjust;
        }

        public (int ChillerCount, double SupplyTemp) GetOptimization(TrainingSample sample)
        {
            var currentLoad = sample.LoadRate;
            
            if (currentLoad > 80)
            {
                return (3, 5.5);
            }
            else if (currentLoad > 60)
            {
                return (2, 6.0);
            }
            else if (currentLoad > 40)
            {
                return (2, 6.5);
            }
            else if (currentLoad > 25)
            {
                return (1, 7.0);
            }
            else if (currentLoad > 15)
            {
                return (1, 7.5);
            }
            else
            {
                return (1, 8.0);
            }
        }
    }

    public class DecisionNode
    {
        public Func<TrainingSample, bool> Condition { get; set; }
        public int ChillerCount { get; set; }
        public double SupplyTemp { get; set; }
        public double BaseCOP { get; set; }
    }

    public class ChillerCombination
    {
        public List<int> ChillerIds { get; set; } = new List<int>();
        public List<int> PumpIds { get; set; } = new List<int>();
        public List<int> TowerIds { get; set; } = new List<int>();
        public double PredictedCOP { get; set; }
        public double ExpectedPower { get; set; }
        public double ExpectedCapacity { get; set; }
    }
}
