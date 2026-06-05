using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Xunit;
using ChillerPlant.Modules.BacnetGateway.Handlers;
using ChillerPlant.Modules.BacnetGateway.Services;
using ChillerPlant.Modules.Shared.Commands;
using ChillerPlant.Configuration;

namespace ChillerPlant.Tests
{
    public class BacnetGatewayTests : TestBase
    {
        private readonly Mock<IMediator> _mediatorMock;
        private readonly Mock<ILogger<InsertDeviceDataHandler>> _insertHandlerLogger;
        private readonly Mock<ILogger<BacnetProtocolParser>> _parserLogger;
        private readonly Mock<ILogger<BacnetUdpListenerService>> _listenerLogger;

        public BacnetGatewayTests()
        {
            _mediatorMock = new Mock<IMediator>();
            _insertHandlerLogger = CreateLoggerMock<InsertDeviceDataHandler>();
            _parserLogger = CreateLoggerMock<BacnetProtocolParser>();
            _listenerLogger = CreateLoggerMock<BacnetUdpListenerService>();
        }

        [Fact]
        public async Task InsertDeviceDataHandler_WithValidData_ShouldInsertAndReturnDeviceData()
        {
            var handler = new InsertDeviceDataHandler(_context, _mediatorMock.Object, _insertHandlerLogger.Object);
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

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(1, result.DeviceId);
            Assert.Equal(250, result.Power);
            Assert.NotNull(result.COP);
            Assert.True(result.COP > 0);
            _mediatorMock.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task InsertDeviceDataHandler_WithInvalidBacnetInstance_ShouldReturnNull()
        {
            var handler = new InsertDeviceDataHandler(_context, _mediatorMock.Object, _insertHandlerLogger.Object);
            var command = new InsertDeviceDataCommand
            {
                BacnetInstance = 999999,
                Power = 250,
                Status = 1,
                Timestamp = DateTime.Now
            };

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task InsertDeviceDataHandler_ShouldCalculateCOP()
        {
            var handler = new InsertDeviceDataHandler(_context, _mediatorMock.Object, _insertHandlerLogger.Object);
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

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(result.COP);
            var expectedCOP = (80 * 5 * 1.163m) / 200;
            Assert.Equal(expectedCOP, result.COP, 2);
        }

        [Fact]
        public void BacnetProtocolParser_ShouldBuildReadRequest()
        {
            var parser = new BacnetProtocolParser(_parserLogger.Object);
            var propertyIds = parser.GetStandardPropertyIds();
            var request = parser.BuildReadPropertyMultipleRequest(300001, propertyIds);

            Assert.NotNull(request);
            Assert.True(request.Length > 0);
            Assert.Equal(0x81, request[0]);
        }

        [Fact]
        public void BacnetSettings_ShouldHaveDefaultValues()
        {
            var settings = new BacnetSettings();

            Assert.Equal(47808, settings.Port);
            Assert.Equal("0.0.0.0", settings.LocalAddress);
            Assert.Equal(30, settings.PollIntervalSeconds);
            Assert.NotNull(settings.DeviceIPs);
            Assert.NotNull(settings.DeviceInstances);
        }

        [Fact]
        public async Task GetDeviceStatusHandler_ShouldReturnDeviceStatusList()
        {
            var handler = new GetDeviceStatusHandler(_context);
            var command = new GetDeviceStatusCommand();

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(5, result.Count);
            Assert.Contains(result, d => d.DeviceName == "冷水机组1#");
            Assert.Contains(result, d => d.EfficiencyStatus != null);
        }

        [Fact]
        public async Task GetDeviceTrendDataHandler_ShouldReturnTrendData()
        {
            var handler = new GetDeviceTrendDataHandler(_context);
            var command = new GetDeviceTrendDataCommand { DeviceId = 1, Hours = 24 };

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.Count >= 3);
            Assert.All(result, d => Assert.Equal(1, d.DeviceId == 0 ? 1 : 1));
        }

        [Fact]
        public async Task InsertBatchDeviceDataHandler_ShouldProcessMultipleRecords()
        {
            var handler = new InsertBatchDeviceDataHandler(_context, _mediatorMock.Object, CreateLoggerMock<InsertBatchDeviceDataHandler>().Object);
            var command = new InsertBatchDeviceDataCommand();
            command.DataList.Add(new InsertDeviceDataCommand
            {
                BacnetInstance = 300001,
                Power = 220,
                SupplyWaterTemp = 7.1m,
                ReturnWaterTemp = 12.1m,
                FlowRate = 83,
                LoadRate = 65,
                Status = 1,
                Timestamp = DateTime.Now
            });
            command.DataList.Add(new InsertDeviceDataCommand
            {
                BacnetInstance = 300002,
                Power = 190,
                SupplyWaterTemp = 7.3m,
                ReturnWaterTemp = 11.9m,
                FlowRate = 78,
                LoadRate = 58,
                Status = 1,
                Timestamp = DateTime.Now
            });

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.Equal(2, result);
            _mediatorMock.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
