namespace ChillerPlant.Modules.BacnetGateway.Models
{
    public class BacnetDataDto
    {
        public int BacnetInstance { get; set; }
        public int Status { get; set; }
        public decimal Power { get; set; }
        public decimal? SupplyWaterTemp { get; set; }
        public decimal? ReturnWaterTemp { get; set; }
        public decimal? CoolingWaterInTemp { get; set; }
        public decimal? CoolingWaterOutTemp { get; set; }
        public decimal? FlowRate { get; set; }
        public decimal? LoadRate { get; set; }
        public decimal? Frequency { get; set; }
        public decimal? Vibration { get; set; }
        public decimal? Current { get; set; }
        public decimal? Voltage { get; set; }
        public long? RunningHours { get; set; }
        public decimal? SupplyPressure { get; set; }
        public decimal? ReturnPressure { get; set; }
        public DateTime Timestamp { get; set; }
        public string RemoteEndpoint { get; set; }
    }
}
