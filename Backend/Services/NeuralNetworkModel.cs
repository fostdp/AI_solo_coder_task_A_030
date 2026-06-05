using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ChillerPlant.Models;

namespace ChillerPlant.Services
{
    public class StandardScaler
    {
        public double[] Means { get; private set; }
        public double[] StandardDeviations { get; private set; }
        public int FeatureCount { get; private set; }
        public bool IsFitted { get; private set; }

        public StandardScaler(int featureCount)
        {
            FeatureCount = featureCount;
            Means = new double[featureCount];
            StandardDeviations = new double[featureCount];
        }

        public void Fit(List<double[]> data)
        {
            if (data == null || data.Count == 0)
                throw new ArgumentException("Training data cannot be empty");

            int n = data.Count;

            for (int i = 0; i < FeatureCount; i++)
            {
                double sum = 0;
                for (int j = 0; j < n; j++)
                {
                    if (data[j].Length > i)
                    {
                        sum += data[j][i];
                    }
                }
                Means[i] = sum / n;

                double sumSquaredDiff = 0;
                for (int j = 0; j < n; j++)
                {
                    if (data[j].Length > i)
                    {
                        double diff = data[j][i] - Means[i];
                        sumSquaredDiff += diff * diff;
                    }
                }
                StandardDeviations[i] = Math.Sqrt(sumSquaredDiff / n);

                if (StandardDeviations[i] < 1e-10)
                {
                    StandardDeviations[i] = 1.0;
                }
            }

            IsFitted = true;
        }

        public double[] Transform(double[] input)
        {
            if (!IsFitted)
                throw new InvalidOperationException("Scaler must be fitted before transforming");

            var result = new double[Math.Min(input.Length, FeatureCount)];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (input[i] - Means[i]) / StandardDeviations[i];
                result[i] = Math.Max(-5.0, Math.Min(5.0, result[i]));
            }
            return result;
        }

        public double[] InverseTransform(double[] normalized)
        {
            if (!IsFitted)
                throw new InvalidOperationException("Scaler must be fitted before inverse transforming");

            var result = new double[Math.Min(normalized.Length, FeatureCount)];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = normalized[i] * StandardDeviations[i] + Means[i];
            }
            return result;
        }

        public void Save(StreamWriter writer)
        {
            writer.WriteLine(FeatureCount);
            writer.WriteLine(string.Join(",", Means));
            writer.WriteLine(string.Join(",", StandardDeviations));
            writer.WriteLine(IsFitted ? "1" : "0");
        }

        public static StandardScaler Load(StreamReader reader)
        {
            int featureCount = int.Parse(reader.ReadLine());
            var scaler = new StandardScaler(featureCount);

            scaler.Means = reader.ReadLine().Split(',').Select(double.Parse).ToArray();
            scaler.StandardDeviations = reader.ReadLine().Split(',').Select(double.Parse).ToArray();
            scaler.IsFitted = reader.ReadLine() == "1";

            return scaler;
        }
    }

    public class NeuralNetworkModel
    {
        private double[][] _weightsInputHidden;
        private double[] _weightsHiddenOutput;
        private double[] _hiddenBias;
        private double _outputBias;
        private readonly int _inputNodes = 6;
        private readonly int _hiddenNodes = 16;
        private readonly double _learningRate = 0.005;
        private readonly Random _random = new Random();

        private StandardScaler _inputScaler;
        private StandardScaler _outputScaler;
        private readonly string[] _featureNames = new[]
        {
            "室外温度", "湿球温度", "冷水机组数量", "冷冻水出水温度", "冷却水进水温度", "负荷率"
        };

        public NeuralNetworkModel()
        {
            _inputScaler = new StandardScaler(_inputNodes);
            _outputScaler = new StandardScaler(1);
            InitializeWeights();
        }

        private void InitializeWeights()
        {
            _weightsInputHidden = new double[_inputNodes][];
            for (int i = 0; i < _inputNodes; i++)
            {
                _weightsInputHidden[i] = new double[_hiddenNodes];
                double std = Math.Sqrt(2.0 / (_inputNodes + _hiddenNodes));
                for (int j = 0; j < _hiddenNodes; j++)
                {
                    _weightsInputHidden[i][j] = _random.NextDouble() * 2 * std - std;
                }
            }

            _weightsHiddenOutput = new double[_hiddenNodes];
            double outputStd = Math.Sqrt(2.0 / (_hiddenNodes + 1));
            for (int i = 0; i < _hiddenNodes; i++)
            {
                _weightsHiddenOutput[i] = _random.NextDouble() * 2 * outputStd - outputStd;
            }

            _hiddenBias = new double[_hiddenNodes];
            for (int i = 0; i < _hiddenNodes; i++)
            {
                _hiddenBias[i] = 0.01;
            }

            _outputBias = 0.0;
        }

        private double Sigmoid(double x)
        {
            if (x > 10) return 0.9999;
            if (x < -10) return 0.0001;
            return 1.0 / (1.0 + Math.Exp(-x));
        }

        private double SigmoidDerivative(double x)
        {
            return x * (1.0 - x);
        }

        private double Relu(double x)
        {
            return Math.Max(0, x);
        }

        private double ReluDerivative(double x)
        {
            return x > 0 ? 1 : 0;
        }

        [Obsolete("Use PredictScaled instead for better accuracy")]
        private double[] NormalizeInputs(double[] inputs)
        {
            var normalized = new double[inputs.Length];

            var ranges = new[]
            {
                new { Min = -10.0, Max = 45.0 },
                new { Min = -5.0, Max = 35.0 },
                new { Min = 0.0, Max = 5.0 },
                new { Min = 4.0, Max = 12.0 },
                new { Min = 20.0, Max = 40.0 },
                new { Min = 0.0, Max = 100.0 }
            };

            for (int i = 0; i < inputs.Length && i < ranges.Length; i++)
            {
                normalized[i] = (inputs[i] - ranges[i].Min) / (ranges[i].Max - ranges[i].Min);
                normalized[i] = Math.Max(0.01, Math.Min(0.99, normalized[i]));
            }

            return normalized;
        }

        public void FitScaler(List<TrainingSample> samples)
        {
            var inputData = samples.Select(s => s.GetInputs()).ToList();
            var outputData = samples.Select(s => new[] { s.ActualCOP }).ToList();

            _inputScaler.Fit(inputData);
            _outputScaler.Fit(outputData);

            Console.WriteLine("StandardScaler fitted successfully:");
            for (int i = 0; i < _inputNodes; i++)
            {
                Console.WriteLine($"  {_featureNames[i]}: mean={_inputScaler.Means[i]:F2}, std={_inputScaler.StandardDeviations[i]:F2}");
            }
            Console.WriteLine($"  Output COP: mean={_outputScaler.Means[0]:F2}, std={_outputScaler.StandardDeviations[0]:F2}");
        }

        public double Predict(double[] inputs)
        {
            if (_inputScaler.IsFitted)
            {
                return PredictScaled(inputs);
            }

            var normalizedInputs = NormalizeInputs(inputs);
            return PredictRaw(normalizedInputs);
        }

        private double PredictScaled(double[] inputs)
        {
            var scaledInputs = _inputScaler.Transform(inputs);
            var hiddenLayer = new double[_hiddenNodes];
            for (int j = 0; j < _hiddenNodes; j++)
            {
                double sum = _hiddenBias[j];
                for (int i = 0; i < _inputNodes; i++)
                {
                    sum += scaledInputs[i] * _weightsInputHidden[i][j];
                }
                hiddenLayer[j] = Relu(sum);
            }

            double output = _outputBias;
            for (int i = 0; i < _hiddenNodes; i++)
            {
                output += hiddenLayer[i] * _weightsHiddenOutput[i];
            }

            var normalizedOutput = new[] { output };
            var actualOutput = _outputScaler.InverseTransform(normalizedOutput);
            return Math.Max(0.5, Math.Min(8.0, actualOutput[0]));
        }

        private double PredictRaw(double[] normalizedInputs)
        {
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
            if (_inputScaler.IsFitted)
            {
                TrainScaled(inputs, target);
            }
            else
            {
                TrainLegacy(inputs, target);
            }
        }

        private void TrainScaled(double[] inputs, double target)
        {
            var scaledInputs = _inputScaler.Transform(inputs);
            var scaledTarget = _outputScaler.Transform(new[] { target })[0];

            var hiddenLayer = new double[_hiddenNodes];
            for (int j = 0; j < _hiddenNodes; j++)
            {
                double sum = _hiddenBias[j];
                for (int i = 0; i < _inputNodes; i++)
                {
                    sum += scaledInputs[i] * _weightsInputHidden[i][j];
                }
                hiddenLayer[j] = Relu(sum);
            }

            double output = _outputBias;
            for (int i = 0; i < _hiddenNodes; i++)
            {
                output += hiddenLayer[i] * _weightsHiddenOutput[i];
            }

            double outputError = scaledTarget - output;
            double outputDelta = outputError;

            var hiddenDeltas = new double[_hiddenNodes];
            for (int j = 0; j < _hiddenNodes; j++)
            {
                hiddenDeltas[j] = outputDelta * _weightsHiddenOutput[j] * ReluDerivative(hiddenLayer[j]);
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
                    _weightsInputHidden[i][j] += _learningRate * hiddenDeltas[j] * scaledInputs[i];
                }
            }
            for (int j = 0; j < _hiddenNodes; j++)
            {
                _hiddenBias[j] += _learningRate * hiddenDeltas[j];
            }
        }

        private void TrainLegacy(double[] inputs, double target)
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

        public void TrainBatch(List<TrainingSample> samples, int epochs = 200)
        {
            if (!_inputScaler.IsFitted)
            {
                FitScaler(samples);
            }

            var shuffled = new List<TrainingSample>(samples);
            double bestError = double.MaxValue;
            int patience = 20;
            int noImproveCount = 0;

            for (int epoch = 0; epoch < epochs; epoch++)
            {
                shuffled = shuffled.OrderBy(x => _random.Next()).ToList();

                double totalError = 0;
                foreach (var sample in shuffled)
                {
                    var inputs = sample.GetInputs();
                    double prediction = Predict(inputs);
                    double error = Math.Abs(sample.ActualCOP - prediction);
                    totalError += error;
                    Train(inputs, sample.ActualCOP);
                }

                double avgError = totalError / shuffled.Count;

                if (avgError < bestError * 0.995)
                {
                    bestError = avgError;
                    noImproveCount = 0;
                }
                else
                {
                    noImproveCount++;
                    if (noImproveCount >= patience && epoch > 50)
                    {
                        Console.WriteLine($"Early stopping at epoch {epoch}, best error: {bestError:F4}");
                        break;
                    }
                }

                if (epoch % 20 == 0)
                {
                    Console.WriteLine($"Epoch {epoch,3}/{epochs}, Average Error: {avgError:F4}, Best: {bestError:F4}");
                }
            }

            Console.WriteLine($"Training completed. Final average error: {bestError:F4}");
        }

        public void SaveModel(string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("SCALER_DATA");
                _inputScaler.Save(writer);
                _outputScaler.Save(writer);

                writer.WriteLine("NETWORK_DATA");
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
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Model file not found: {filePath}, using initialized weights");
                return;
            }

            try
            {
                var lines = File.ReadAllLines(filePath);
                int lineIndex = 0;

                if (lines[lineIndex] == "SCALER_DATA")
                {
                    lineIndex++;
                    using (var reader = new StreamReader(filePath))
                    {
                        for (int i = 0; i < lineIndex; i++)
                        {
                            reader.ReadLine();
                        }
                        _inputScaler = StandardScaler.Load(reader);
                        _outputScaler = StandardScaler.Load(reader);
                    }

                    lineIndex = Array.FindIndex(lines, lineIndex, x => x == "NETWORK_DATA") + 1;
                }

                var dimensions = lines[lineIndex++].Split(',');

                for (int i = 0; i < _inputNodes; i++)
                {
                    if (lineIndex < lines.Length)
                    {
                        var weights = lines[lineIndex++].Split(',');
                        for (int j = 0; j < _hiddenNodes && j < weights.Length; j++)
                        {
                            _weightsInputHidden[i][j] = double.Parse(weights[j]);
                        }
                    }
                }

                if (lineIndex < lines.Length)
                {
                    var outputWeights = lines[lineIndex++].Split(',');
                    for (int i = 0; i < _hiddenNodes && i < outputWeights.Length; i++)
                    {
                        _weightsHiddenOutput[i] = double.Parse(outputWeights[i]);
                    }
                }

                if (lineIndex < lines.Length)
                {
                    var hiddenBiases = lines[lineIndex++].Split(',');
                    for (int i = 0; i < _hiddenNodes && i < hiddenBiases.Length; i++)
                    {
                        _hiddenBias[i] = double.Parse(hiddenBiases[i]);
                    }
                }

                if (lineIndex < lines.Length)
                {
                    _outputBias = double.Parse(lines[lineIndex++]);
                }

                Console.WriteLine($"Model loaded successfully from {filePath}");
                if (_inputScaler.IsFitted)
                {
                    Console.WriteLine("StandardScaler parameters loaded");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load model: {ex.Message}, using initialized weights");
                InitializeWeights();
            }
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
