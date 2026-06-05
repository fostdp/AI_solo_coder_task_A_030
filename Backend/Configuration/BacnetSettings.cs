using System.Collections.Generic;

namespace ChillerPlant.Configuration
{
    public class BacnetSettings
    {
        public int Port { get; set; } = 47808;
        public string LocalAddress { get; set; } = "0.0.0.0";
        public int PollIntervalSeconds { get; set; } = 30;
        public List<string> DeviceIPs { get; set; } = new List<string>();
        public List<int> DeviceInstances { get; set; } = new List<int>();
    }
}
