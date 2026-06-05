using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ChillerPlant.Data;
using ChillerPlant.Models;

namespace ChillerPlant.Tests
{
    public abstract class TestBase : IDisposable
    {
        protected readonly ApplicationDbContext _context;
        protected readonly IServiceProvider _serviceProvider;
        protected readonly Mock<ILoggerFactory> _loggerFactoryMock;

        protected TestBase()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _loggerFactoryMock = new Mock<ILoggerFactory>();

            var services = new ServiceCollection();
            services.AddSingleton(_context);
            services.AddSingleton(_loggerFactoryMock.Object);
            _serviceProvider = services.BuildServiceProvider();

            SeedDatabase();
        }

        private void SeedDatabase()
        {
            var deviceTypes = new List<DeviceType>
            {
                new DeviceType { DeviceTypeId = 1, TypeName = "冷水机组", Description = "Chiller" },
                new DeviceType { DeviceTypeId = 2, TypeName = "冷冻水泵", Description = "Chiller Pump" },
                new DeviceType { DeviceTypeId = 3, TypeName = "冷却水泵", Description = "Cooling Pump" },
                new DeviceType { DeviceTypeId = 4, TypeName = "冷却塔", Description = "Cooling Tower" }
            };
            _context.DeviceTypes.AddRange(deviceTypes);

            var devices = new List<Device>
            {
                new Device { DeviceId = 1, DeviceName = "冷水机组1#", DeviceCode = "CH-001", DeviceTypeId = 1, BacnetInstance = 300001, DesignCOP = 5.5m, Status = 1, X = 100, Y = 100 },
                new Device { DeviceId = 2, DeviceName = "冷水机组2#", DeviceCode = "CH-002", DeviceTypeId = 1, BacnetInstance = 300002, DesignCOP = 5.5m, Status = 1, X = 200, Y = 100 },
                new Device { DeviceId = 3, DeviceName = "冷水机组3#", DeviceCode = "CH-003", DeviceTypeId = 1, BacnetInstance = 300003, DesignCOP = 5.5m, Status = 0, X = 300, Y = 100 },
                new Device { DeviceId = 4, DeviceName = "冷冻水泵1#", DeviceCode = "CP-001", DeviceTypeId = 2, BacnetInstance = 300004, DesignCOP = 0, Status = 1, X = 50, Y = 200 },
                new Device { DeviceId = 5, DeviceName = "冷冻水泵2#", DeviceCode = "CP-002", DeviceTypeId = 2, BacnetInstance = 300005, DesignCOP = 0, Status = 1, X = 150, Y = 200 }
            };
            _context.Devices.AddRange(devices);

            var deviceData = new List<DeviceData>
            {
                new DeviceData { DeviceId = 1, Power = 200, SupplyWaterTemp = 7, ReturnWaterTemp = 12, CoolingWaterInTemp = 30, CoolingWaterOutTemp = 35, FlowRate = 80, LoadRate = 60, COP = 4.8m, Status = 1, Timestamp = DateTime.Now.AddMinutes(-5) },
                new DeviceData { DeviceId = 1, Power = 210, SupplyWaterTemp = 6.8m, ReturnWaterTemp = 12.2m, CoolingWaterInTemp = 30.5m, CoolingWaterOutTemp = 35.5m, FlowRate = 82, LoadRate = 62, COP = 4.9m, Status = 1, Timestamp = DateTime.Now.AddMinutes(-3) },
                new DeviceData { DeviceId = 1, Power = 205, SupplyWaterTemp = 6.9m, ReturnWaterTemp = 12.1m, CoolingWaterInTemp = 30.2m, CoolingWaterOutTemp = 35.2m, FlowRate = 81, LoadRate = 61, COP = 4.85m, Status = 1, Timestamp = DateTime.Now.AddMinutes(-1) },
                new DeviceData { DeviceId = 2, Power = 180, SupplyWaterTemp = 7.2m, ReturnWaterTemp = 11.8m, CoolingWaterInTemp = 29.8m, CoolingWaterOutTemp = 34.8m, FlowRate = 75, LoadRate = 55, COP = 4.6m, Status = 1, Timestamp = DateTime.Now.AddMinutes(-1) }
            };
            _context.DeviceData.AddRange(deviceData);

            var alarms = new List<Alarm>
            {
                new Alarm { AlarmId = 1, DeviceId = 1, AlarmType = "HighPower", AlarmLevel = 1, AlarmMessage = "功率过高测试告警", StartTime = DateTime.Now.AddHours(-1), Status = 1 },
                new Alarm { AlarmId = 2, DeviceId = 3, AlarmType = "DeviceOffline", AlarmLevel = 2, AlarmMessage = "设备离线测试告警", StartTime = DateTime.Now.AddHours(-2), Status = 1 }
            };
            _context.Alarms.AddRange(alarms);

            var systemEfficiencies = new List<SystemEfficiency>
            {
                new SystemEfficiency { Timestamp = DateTime.Now.AddMinutes(-30), SystemCOP = 4.2m, DesignCOP = 5.5m, COPRatio = 0.76m, TotalPower = 400, TotalCooling = 1680, RunningChillerCount = 2, RunningPumpCount = 2, RunningTowerCount = 1, OutdoorTemp = 28, WetBulbTemp = 25 },
                new SystemEfficiency { Timestamp = DateTime.Now.AddMinutes(-15), SystemCOP = 4.3m, DesignCOP = 5.5m, COPRatio = 0.78m, TotalPower = 410, TotalCooling = 1763, RunningChillerCount = 2, RunningPumpCount = 2, RunningTowerCount = 1, OutdoorTemp = 28.5m, WetBulbTemp = 25.2m }
            };
            _context.SystemEfficiencies.AddRange(systemEfficiencies);

            _context.SaveChanges();
        }

        protected Mock<ILogger<T>> CreateLoggerMock<T>()
        {
            var loggerMock = new Mock<ILogger<T>>();
            _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
                .Returns(loggerMock.Object);
            return loggerMock;
        }

        protected IOptions<TOptions> CreateOptionsMock<TOptions>(TOptions value)
            where TOptions : class, new()
        {
            var optionsMock = new Mock<IOptions<TOptions>>();
            optionsMock.Setup(o => o.Value).Returns(value);
            return optionsMock.Object;
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
