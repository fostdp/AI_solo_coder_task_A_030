namespace BacnetSimulator.Models;

public class SimulatorConfig
{
    public string TargetAddress { get; set; } = "127.0.0.1";
    public int TargetPort { get; set; } = 47808;
    public int SendIntervalSeconds { get; set; } = 30;
    public int StartInstance { get; set; } = 300001;
    
    public int CentrifugalCount { get; set; } = 3;
    public int ScrewCount { get; set; } = 2;
    public int CoolingTowerCount { get; set; } = 8;
    public int ChilledPumpCount { get; set; } = 12;
    public int CoolingPumpCount { get; set; } = 12;
    
    public double LoadFactor { get; set; } = 0.7;
    public double AmbientTemp { get; set; } = 28.0;
    public double WetBulbTemp { get; set; } = 25.0;
    public double RandomNoise { get; set; } = 0.05;
    
    public Dictionary<string, DevicePerformanceCurve> PerformanceCurves { get; set; } = new();
}

public class DevicePerformanceCurve
{
    public double A { get; set; }
    public double B { get; set; }
    public double C { get; set; }
    public double RatedPower { get; set; }
    public double DesignCOP { get; set; }
    public double Efficiency { get; set; } = 0.85;
}

public enum DeviceType
{
    CentrifugalChiller = 1,
    ScrewChiller = 2,
    CoolingTower = 3,
    ChilledPump = 4,
    CoolingPump = 5
}

public class SimulatedDevice
{
    public int BacnetInstance { get; set; }
    public DeviceType DeviceType { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DevicePerformanceCurve PerformanceCurve { get; set; } = new();
    
    public double CurrentLoad { get; set; }
    public double CurrentPower { get; set; }
    public double SupplyWaterTemp { get; set; }
    public double ReturnWaterTemp { get; set; }
    public double CoolingWaterInTemp { get; set; }
    public double CoolingWaterOutTemp { get; set; }
    public double FlowRate { get; set; }
    public double SupplyPressure { get; set; }
    public double ReturnPressure { get; set; }
    public double LoadRate { get; set; }
    public double Frequency { get; set; }
    public double Vibration { get; set; }
    public double Current { get; set; }
    public double Voltage { get; set; }
    public long RunningHours { get; set; }
    public int Status { get; set; } = 1;
    public DateTime Timestamp { get; set; }
    public double COP { get; set; }
}
