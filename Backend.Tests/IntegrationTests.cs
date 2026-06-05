using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using ChillerPlant.Configuration;
using ChillerPlant.Data;
using ChillerPlant.Modules.AlarmManager;
using ChillerPlant.Modules.AlarmManager.Models;
using ChillerPlant.Modules.AlarmManager.Services;
using ChillerPlant.Modules.BacnetGateway;
using ChillerPlant.Modules.BacnetGateway.Services;
using ChillerPlant.Modules.EfficiencyOptimizer;
using ChillerPlant.Modules.EfficiencyOptimizer.Configuration;
using ChillerPlant.Modules.EfficiencyOptimizer.Services;
using ChillerPlant.Modules.Shared.Commands;
using ChillerPlant.Modules.Shared.Events;

namespace ChillerPlant.Tests
{
    public class IntegrationTests : TestBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMediator _mediator;
        private readonly string _testConfigPath;

        public IntegrationTests()
        {
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.test.json", optional: true, reloadOnChange: true);

            var config = configBuilder.Build();

            var services = new ServiceCollection();

            services.AddSingleton(_context);
            services.AddSingleton<IConfiguration>(config);

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblies(
                    typeof(InsertDeviceDataCommand).Assembly,
                    typeof(Modules.BacnetGateway.Handlers.InsertDeviceDataHandler).Assembly,
                    typeof(Modules.EfficiencyOptimizer.Handlers.CalculateSystemEfficiencyHandler).Assembly,
                    typeof(Modules.AlarmManager.Handlers.CheckAlarmsHandler).Assembly);
            });

            services.AddBacnetGatewayModule();
            services.AddEfficiencyOptimizerModule();
            services.AddAlarmManagerModule();

            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddSingleton<ILoggerFactory, LoggerFactory>();

            _serviceProvider = services.BuildServiceProvider();
            _mediator = _serviceProvider.GetRequiredService<IMediator>();
            _testConfigPath = Path.Combine(Path.GetTempPath(), $"test_model_{Guid.NewGuid()}.txt");
        }

        [Fact]
        public void DependencyInjection_ShouldResolveAllServices()
        {
            var bacnetListener = _serviceProvider.GetService<BacnetUdpListenerService>();
            var nnService = _serviceProvider.GetService<NeuralNetworkOptimizationService>();
            var alarmEvalService = _serviceProvider.GetService<AlarmEvaluationService>();
            var wechatAggregator = _serviceProvider.GetService<WechatAlarmAggregatorService>();

            Assert.NotNull(bacnetListener);
            Assert.NotNull(nnService);
            Assert.NotNull(alarmEvalService);
            Assert.NotNull(wechatAggregator);
        }

        [Fact]
        public void Options_ShouldBindConfigurationCorrectly()
        {
            var bacnetSettings = _serviceProvider.GetService<IOptions<BacnetSettings>>();
            var optimizationSettings = _serviceProvider.GetService<IOptions<OptimizationSettings>>();
            var wechatConfig = _serviceProvider.GetService<IOptions<WechatPushConfig>>();

            Assert.NotNull(bacnetSettings?.Value);
            Assert.NotNull(optimizationSettings?.Value);
            Assert.NotNull(wechatConfig?.Value);

            Assert.Equal(47808, bacnetSettings.Value.Port);
            Assert.Equal("Data/neural_network_model.txt", optimizationSettings.Value.ModelWeightsPath);
            Assert.Equal(60, wechatConfig.Value.AggregateWindowSeconds);
        }

        [Fact]
        public async Task MediatR_ShouldHandleInsertDeviceDataCommand()
        {
            var command = new InsertDeviceDataCommand
            {
                BacnetInstance = 300001,
                Power = 250,
                SupplyWaterTemp = 7.5m,
                ReturnWaterTemp = 12.5m,
                CoolingWaterInTemp = 30,
                CoolingWaterOutTemp = 35,
                FlowRate = 85,
                LoadRate = 70,
                Status = 1,
                Timestamp = DateTime.Now
            };

            var result = await _mediator.Send(command);

            Assert.NotNull(result);
            Assert.Equal(1, result.DeviceId);
            Assert.NotNull(result.COP);
            Assert.True(result.COP > 0);

            var savedData = await _context.DeviceData
                .Where(d => d.DeviceId == 1)
                .OrderByDescending(d => d.Timestamp)
                .FirstOrDefaultAsync();

            Assert.NotNull(savedData);
            Assert.Equal(250, savedData.Power);
        }

        [Fact]
        public async Task MediatR_ShouldHandleGetDeviceStatusCommand()
        {
            var command = new GetDeviceStatusCommand();

            var result = await _mediator.Send(command);

            Assert.NotNull(result);
            Assert.True(result.Count >= 5);
            Assert.All(result, r =>
            {
                Assert.NotNull(r.DeviceName);
                Assert.NotNull(r.StatusColor);
            });
        }

        [Fact]
        public async Task MediatR_ShouldHandleCalculateSystemEfficiencyCommand()
        {
            var command = new CalculateSystemEfficiencyCommand();

            var result = await _mediator.Send(command);

            Assert.Equal(Unit.Value, result);

            var efficiencies = await _context.SystemEfficiencies
                .OrderByDescending(e => e.Timestamp)
                .Take(5)
                .ToListAsync();

            Assert.True(efficiencies.Count >= 1);
            Assert.All(efficiencies, e =>
            {
                Assert.True(e.SystemCOP > 0);
                Assert.True(e.TotalPower > 0);
            });
        }

        [Fact]
        public async Task MediatR_ShouldHandleCheckAlarmsCommand()
        {
            var command = new CheckAlarmsCommand();

            var result = await _mediator.Send(command);

            Assert.NotNull(result);
            Assert.True(result.Count >= 0);
        }

        [Fact]
        public async Task MediatR_ShouldHandleAcknowledgeAlarmCommand()
        {
            var command = new AcknowledgeAlarmCommand
            {
                AlarmId = 1,
                AckBy = "integration_test"
            };

            var result = await _mediator.Send(command);

            Assert.True(result);

            var alarm = await _context.Alarms.FindAsync(1L);
            Assert.Equal(2, alarm.Status);
            Assert.Equal("integration_test", alarm.AckBy);
        }

        [Fact]
        public async Task FullWorkflow_ShouldProcessDataAndGenerateAlarms()
        {
            var insertCommand = new InsertDeviceDataCommand
            {
                BacnetInstance = 300001,
                Power = 950,
                SupplyWaterTemp = 9.5m,
                ReturnWaterTemp = 14,
                CoolingWaterInTemp = 32,
                CoolingWaterOutTemp = 37,
                FlowRate = 90,
                LoadRate = 85,
                Vibration = 5.5m,
                Status = 1,
                Timestamp = DateTime.Now
            };

            var insertResult = await _mediator.Send(insertCommand);
            Assert.NotNull(insertResult);

            var efficiencyCommand = new CalculateSystemEfficiencyCommand();
            var efficiencyResult = await _mediator.Send(efficiencyCommand);
            Assert.Equal(Unit.Value, efficiencyResult);

            var alarmCommand = new CheckAlarmsCommand();
            var alarmResult = await _mediator.Send(alarmCommand);

            Assert.NotNull(alarmResult);
            Assert.Contains(alarmResult, a => a.AlarmType == "HighPower");
        }

        [Fact]
        public void NeuralNetworkOptimizationService_ShouldReadModelPathFromConfig()
        {
            var inMemorySettings = new Dictionary<string, string>
            {
                {"Optimization:ModelWeightsPath", _testConfigPath},
                {"Optimization:AutoRetrainIntervalMinutes", "60"},
                {"Optimization:EfficiencyCalcIntervalSeconds", "15"}
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddOptions<OptimizationSettings>()
                .BindConfiguration("Optimization")
                .ValidateOnStart();
            services.AddSingleton<NeuralNetworkOptimizationService>();
            services.AddSingleton(CreateLoggerMock<NeuralNetworkOptimizationService>().Object);

            var serviceProvider = services.BuildServiceProvider();
            var settings = serviceProvider.GetRequiredService<IOptions<OptimizationSettings>>();

            Assert.Equal(_testConfigPath, settings.Value.ModelWeightsPath);
            Assert.Equal(60, settings.Value.AutoRetrainIntervalMinutes);
            Assert.Equal(15, settings.Value.EfficiencyCalcIntervalSeconds);
        }

        [Fact]
        public void AppConfiguration_ShouldContainAllRequiredSections()
        {
            var configPath = Path.Combine(
                Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? "",
                "Backend",
                "appsettings.json");

            if (File.Exists(configPath))
            {
                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(configPath)
                    .Build();

                var bacnetSection = configuration.GetSection("BACnet");
                var optimizationSection = configuration.GetSection("Optimization");
                var wechatSection = configuration.GetSection("Wechat");

                Assert.True(bacnetSection.Exists(), "BACnet configuration section is missing");
                Assert.True(optimizationSection.Exists(), "Optimization configuration section is missing");
                Assert.True(wechatSection.Exists(), "Wechat configuration section is missing");

                Assert.NotNull(optimizationSection["ModelWeightsPath"]);
                Assert.Equal("Data/neural_network_model.txt", optimizationSection["ModelWeightsPath"]);
                Assert.NotNull(bacnetSection["Port"]);
            }
        }

        [Fact]
        public async Task EventPublishing_ShouldPublishEventsOnDeviceDataInsert()
        {
            var publishedEvents = new List<INotification>();
            var mockMediator = new Mock<IMediator>();
            mockMediator.Setup(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
                .Callback<INotification, CancellationToken>((evt, _) => publishedEvents.Add(evt))
                .Returns(Task.CompletedTask);

            var handler = new Modules.BacnetGateway.Handlers.InsertDeviceDataHandler(
                _context,
                mockMediator.Object,
                CreateLoggerMock<Modules.BacnetGateway.Handlers.InsertDeviceDataHandler>().Object);

            var command = new InsertDeviceDataCommand
            {
                BacnetInstance = 300001,
                Power = 200,
                SupplyWaterTemp = 7,
                ReturnWaterTemp = 12,
                FlowRate = 80,
                LoadRate = 60,
                Status = 1,
                Timestamp = DateTime.Now
            };

            await handler.Handle(command, CancellationToken.None);

            Assert.True(publishedEvents.Count >= 1);
            Assert.Contains(publishedEvents, e => e is DeviceDataReceivedEvent);
        }

        [Fact]
        public void ModuleRegistration_ShouldRegisterAllModuleServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton(_context);
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

            services.AddBacnetGatewayModule();
            services.AddEfficiencyOptimizerModule();
            services.AddAlarmManagerModule();

            var serviceProvider = services.BuildServiceProvider();

            var bacnetServices = services.Where(s => s.ServiceType.Namespace?.Contains("BacnetGateway") ?? false).ToList();
            var efficiencyServices = services.Where(s => s.ServiceType.Namespace?.Contains("EfficiencyOptimizer") ?? false).ToList();
            var alarmServices = services.Where(s => s.ServiceType.Namespace?.Contains("AlarmManager") ?? false).ToList();

            Assert.True(bacnetServices.Count >= 2);
            Assert.True(efficiencyServices.Count >= 2);
            Assert.True(alarmServices.Count >= 3);
        }

        [Fact]
        public async Task Dashboard_ShouldReturnCorrectStatistics()
        {
            var command = new GetRealtimeDashboardCommand();
            var handler = new Modules.AlarmManager.Handlers.GetRealtimeDashboardHandler(
                _context,
                CreateLoggerMock<Modules.AlarmManager.Handlers.GetRealtimeDashboardHandler>().Object);

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.TotalPower > 0);
            Assert.True(result.TotalCooling > 0);
            Assert.True(result.SystemCOP >= 0);
            Assert.True(result.COPRatio >= 0);
            Assert.True(result.ActiveAlarmCount >= 0);
            Assert.True(result.RunningChillerCount >= 0);
        }

        public override void Dispose()
        {
            if (File.Exists(_testConfigPath))
            {
                try { File.Delete(_testConfigPath); } catch { }
            }
            base.Dispose();
        }
    }
}
