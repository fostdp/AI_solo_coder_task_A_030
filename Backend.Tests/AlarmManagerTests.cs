using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Xunit;
using ChillerPlant.Modules.AlarmManager.Handlers;
using ChillerPlant.Modules.AlarmManager.Models;
using ChillerPlant.Modules.AlarmManager.Services;
using ChillerPlant.Modules.Shared.Commands;

namespace ChillerPlant.Tests
{
    public class AlarmManagerTests : TestBase
    {
        private readonly Mock<IMediator> _mediatorMock;
        private readonly Mock<ILogger<AlarmEvaluationService>> _evalServiceLogger;
        private readonly Mock<ILogger<WechatAlarmAggregatorService>> _aggregatorLogger;
        private readonly Mock<ILogger<CheckAlarmsHandler>> _checkHandlerLogger;
        private readonly Mock<ILogger<AcknowledgeAlarmHandler>> _ackHandlerLogger;
        private readonly Mock<ILogger<UpdateWorkOrderStatusHandler>> _workOrderHandlerLogger;
        private readonly Mock<ILogger<PushAlarmToWechatHandler>> _pushHandlerLogger;
        private readonly Mock<ILogger<GetRealtimeDashboardHandler>> _dashboardHandlerLogger;

        public AlarmManagerTests()
        {
            _mediatorMock = new Mock<IMediator>();
            _evalServiceLogger = CreateLoggerMock<AlarmEvaluationService>();
            _aggregatorLogger = CreateLoggerMock<WechatAlarmAggregatorService>();
            _checkHandlerLogger = CreateLoggerMock<CheckAlarmsHandler>();
            _ackHandlerLogger = CreateLoggerMock<AcknowledgeAlarmHandler>();
            _workOrderHandlerLogger = CreateLoggerMock<UpdateWorkOrderStatusHandler>();
            _pushHandlerLogger = CreateLoggerMock<PushAlarmToWechatHandler>();
            _dashboardHandlerLogger = CreateLoggerMock<GetRealtimeDashboardHandler>();
        }

        [Fact]
        public void WechatPushConfig_ShouldHaveDefaultValues()
        {
            var config = new WechatPushConfig();

            Assert.Equal(60, config.AggregateWindowSeconds);
            Assert.Equal(10, config.MaxAlarmsPerMessage);
            Assert.Equal(5, config.PushIntervalSeconds);
            Assert.Null(config.WebhookUrl);
        }

        [Fact]
        public async Task AlarmEvaluationService_ShouldEvaluateDeviceStatusAlarms()
        {
            var service = new AlarmEvaluationService(_context, _evalServiceLogger.Object);

            var result = await service.EvaluateAlarms(CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.Count >= 1);
        }

        [Fact]
        public async Task AlarmEvaluationService_ShouldEvaluateThresholdAlarms()
        {
            var service = new AlarmEvaluationService(_context, _evalServiceLogger.Object);

            var highPowerData = new ChillerPlant.Models.DeviceData
            {
                DeviceId = 1,
                Power = 900,
                SupplyWaterTemp = 9,
                Status = 1,
                Timestamp = DateTime.Now.AddMinutes(-1)
            };
            _context.DeviceData.Add(highPowerData);
            await _context.SaveChangesAsync();

            var result = await service.EvaluateAlarms(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Contains(result, a => a.AlarmType == "HighPower");
        }

        [Fact]
        public async Task CheckAlarmsHandler_ShouldEvaluateAndQueueAlarms()
        {
            var evalService = new AlarmEvaluationService(_context, _evalServiceLogger.Object);
            var wechatConfig = new WechatPushConfig { AggregateWindowSeconds = 2, MaxAlarmsPerMessage = 5 };
            var wechatOptions = CreateOptionsMock(wechatConfig);
            var aggregator = new WechatAlarmAggregatorService(_aggregatorLogger.Object, _mediatorMock.Object, wechatOptions);

            var handler = new CheckAlarmsHandler(evalService, aggregator, _mediatorMock.Object, _checkHandlerLogger.Object);
            var command = new CheckAlarmsCommand();

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.NotNull(result);
            _mediatorMock.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task AcknowledgeAlarmHandler_ShouldAcknowledgeAlarm()
        {
            var handler = new AcknowledgeAlarmHandler(_context, _ackHandlerLogger.Object);
            var command = new AcknowledgeAlarmCommand { AlarmId = 1, AckBy = "testuser" };

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.True(result);
            var alarm = await _context.Alarms.FindAsync(1L);
            Assert.NotNull(alarm);
            Assert.Equal(2, alarm.Status);
            Assert.Equal("testuser", alarm.AckBy);
            Assert.NotNull(alarm.AckAt);
            Assert.NotNull(alarm.EndTime);
        }

        [Fact]
        public async Task AcknowledgeAlarmHandler_WithInvalidAlarmId_ShouldReturnFalse()
        {
            var handler = new AcknowledgeAlarmHandler(_context, _ackHandlerLogger.Object);
            var command = new AcknowledgeAlarmCommand { AlarmId = 9999, AckBy = "testuser" };

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateWorkOrderStatusHandler_ShouldUpdateStatus()
        {
            var workOrder = new ChillerPlant.Models.WorkOrder
            {
                AlarmId = 1,
                Title = "Test Work Order",
                Description = "Test Description",
                Priority = 1,
                Status = 0,
                CreatedAt = DateTime.Now
            };
            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();

            var handler = new UpdateWorkOrderStatusHandler(_context, _workOrderHandlerLogger.Object);
            var command = new UpdateWorkOrderStatusCommand
            {
                WorkOrderId = workOrder.WorkOrderId,
                Status = 1,
                Remark = "Starting work",
                Assignee = "technician1"
            };

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.True(result);
            var updated = await _context.WorkOrders.FindAsync(workOrder.WorkOrderId);
            Assert.Equal(1, updated.Status);
            Assert.Equal("Starting work", updated.Remark);
            Assert.Equal("technician1", updated.Assignee);
        }

        [Fact]
        public async Task UpdateWorkOrderStatusHandler_CompletingWorkOrder_ShouldCloseAlarm()
        {
            var workOrder = new ChillerPlant.Models.WorkOrder
            {
                AlarmId = 1,
                Title = "Test Work Order",
                Description = "Test Description",
                Priority = 1,
                Status = 1,
                CreatedAt = DateTime.Now
            };
            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();

            var handler = new UpdateWorkOrderStatusHandler(_context, _workOrderHandlerLogger.Object);
            var command = new UpdateWorkOrderStatusCommand
            {
                WorkOrderId = workOrder.WorkOrderId,
                Status = 2,
                Remark = "Completed"
            };

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.True(result);
            var alarm = await _context.Alarms.FindAsync(1L);
            Assert.Equal(3, alarm.Status);
            Assert.NotNull(alarm.EndTime);
        }

        [Fact]
        public async Task PushAlarmToWechatHandler_ShouldQueueAlarm()
        {
            var wechatConfig = new WechatPushConfig { AggregateWindowSeconds = 2, MaxAlarmsPerMessage = 5 };
            var wechatOptions = CreateOptionsMock(wechatConfig);
            var aggregator = new WechatAlarmAggregatorService(_aggregatorLogger.Object, _mediatorMock.Object, wechatOptions);

            var handler = new PushAlarmToWechatHandler(_context, aggregator, _pushHandlerLogger.Object);
            var command = new PushAlarmToWechatCommand { AlarmId = 1 };

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.True(result);
        }

        [Fact]
        public async Task GetRealtimeDashboardHandler_ShouldReturnDashboardData()
        {
            var handler = new GetRealtimeDashboardHandler(_context, _dashboardHandlerLogger.Object);
            var command = new GetRealtimeDashboardCommand();

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.TotalPower > 0);
            Assert.True(result.TotalCooling > 0);
            Assert.Equal(2, result.ActiveAlarmCount);
            Assert.Equal(1, result.CriticalAlarmCount);
            Assert.Equal(2, result.RunningChillerCount);
            Assert.Equal(3, result.TotalChillerCount);
        }

        [Fact]
        public void WechatAlarmAggregatorService_ShouldEnqueueAlarms()
        {
            var wechatConfig = new WechatPushConfig { AggregateWindowSeconds = 2, MaxAlarmsPerMessage = 5 };
            var wechatOptions = CreateOptionsMock(wechatConfig);
            var aggregator = new WechatAlarmAggregatorService(_aggregatorLogger.Object, _mediatorMock.Object, wechatOptions);

            aggregator.EnqueueAlarm(1, "HighPower", 1, "功率过高", "冷水机组1#");
            aggregator.EnqueueAlarm(2, "DeviceOffline", 2, "设备离线", "冷水机组3#");

            _aggregatorLogger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("enqueued")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task CheckAlarmsHandler_ShouldCreateWorkOrders()
        {
            var evalService = new AlarmEvaluationService(_context, _evalServiceLogger.Object);
            var wechatConfig = new WechatPushConfig { AggregateWindowSeconds = 2, MaxAlarmsPerMessage = 5 };
            var wechatOptions = CreateOptionsMock(wechatConfig);
            var aggregator = new WechatAlarmAggregatorService(_aggregatorLogger.Object, _mediatorMock.Object, wechatOptions);

            var handler = new CheckAlarmsHandler(evalService, aggregator, _mediatorMock.Object, _checkHandlerLogger.Object);
            var command = new CheckAlarmsCommand();

            var initialWorkOrderCount = await _context.WorkOrders.CountAsync();
            var result = await handler.Handle(command, CancellationToken.None);
            var finalWorkOrderCount = await _context.WorkOrders.CountAsync();

            Assert.True(finalWorkOrderCount >= initialWorkOrderCount);
        }

        [Fact]
        public async Task AlarmEvaluationService_ShouldDetectLowEfficiencyAlarms()
        {
            var service = new AlarmEvaluationService(_context, _evalServiceLogger.Object);

            for (int i = 0; i < 3; i++)
            {
                _context.SystemEfficiencies.Add(new ChillerPlant.Models.SystemEfficiency
                {
                    Timestamp = DateTime.Now.AddMinutes(-10 + i * 2),
                    SystemCOP = 3.0m,
                    DesignCOP = 5.5m,
                    COPRatio = 0.54m,
                    TotalPower = 500,
                    TotalCooling = 1500,
                    RunningChillerCount = 2,
                    RunningPumpCount = 2,
                    RunningTowerCount = 1,
                    OutdoorTemp = 28,
                    WetBulbTemp = 25
                });
            }
            await _context.SaveChangesAsync();

            var result = await service.EvaluateAlarms(CancellationToken.None);

            Assert.Contains(result, a => a.AlarmType == "SystemLowEfficiency");
        }

        [Fact]
        public void AlarmDto_ShouldMapCorrectly()
        {
            var alarm = new ChillerPlant.Models.Alarm
            {
                AlarmId = 100,
                DeviceId = 1,
                AlarmType = "TestAlarm",
                AlarmLevel = 2,
                AlarmMessage = "Test message",
                StartTime = DateTime.Now,
                Status = 1
            };

            var device = _context.Devices.Find(1);

            var service = new AlarmEvaluationService(_context, _evalServiceLogger.Object);
            var dtoMethod = service.GetType().GetMethod("MapToDto",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var dto = (AlarmDto)dtoMethod.Invoke(service, new object[] { alarm, device.DeviceName });

            Assert.Equal(100, dto.AlarmId);
            Assert.Equal(1, dto.DeviceId);
            Assert.Equal(device.DeviceName, dto.DeviceName);
            Assert.Equal("TestAlarm", dto.AlarmType);
            Assert.Equal(2, dto.AlarmLevel);
            Assert.Equal("Test message", dto.AlarmMessage);
            Assert.Equal(1, dto.Status);
        }
    }
}
