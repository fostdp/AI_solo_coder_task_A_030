using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MediatR;
using ChillerPlantOptimization.Contracts.Events;
using ChillerPlantOptimization.Models;
using ChillerPlantOptimization.Repositories;

namespace ChillerPlantOptimization.Modules.BacnetGateway;

public class BacnetDataPoint
{
    public int ObjectType { get; set; }
    public int ObjectInstance { get; set; }
    public int PropertyId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class BacnetReceivedPacket
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int Length { get; set; }
    public IPEndPoint? RemoteEndPoint { get; set; }
    public DateTime ReceivedTime { get; set; }

    public void Reset()
    {
        Array.Clear(Data, 0, Data.Length);
        Length = 0;
        RemoteEndPoint = null;
        ReceivedTime = DateTime.MinValue;
    }
}

public class BacnetPacketPool
{
    private readonly ConcurrentQueue<BacnetReceivedPacket> _pool = new();
    private readonly int _bufferSize;

    public BacnetPacketPool(int initialCapacity = 100, int bufferSize = 1500)
    {
        _bufferSize = bufferSize;
        for (int i = 0; i < initialCapacity; i++)
        {
            _pool.Enqueue(new BacnetReceivedPacket { Data = new byte[bufferSize] });
        }
    }

    public BacnetReceivedPacket Rent()
    {
        if (_pool.TryDequeue(out var packet))
        {
            return packet;
        }
        return new BacnetReceivedPacket { Data = new byte[_bufferSize] };
    }

    public void Return(BacnetReceivedPacket packet)
    {
        packet.Reset();
        _pool.Enqueue(packet);
    }
}

public class BacnetGateway : IBacnetGateway
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly ITimeSeriesRepository _timeSeriesRepository;
    private readonly IDeviceDataService _deviceDataService;
    private readonly IMediator _mediator;
    private readonly ILogger<BacnetGateway> _logger;

    private UdpClient? _udpClient;
    private readonly BacnetPacketPool _packetPool = new(initialCapacity: 200, bufferSize: 1500);
    private readonly ConcurrentQueue<BacnetReceivedPacket> _receiveQueue = new();
    private readonly SemaphoreSlim _queueSemaphore = new(0);
    private readonly Dictionary<string, BacnetDataPoint[]> _dataPoints = new()
    {
        ["centrifugal_chiller"] = new[]
        {
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 0, PropertyId = 85, Name = "Power" },
            new BacnetDataPoint { ObjectType = 0, ObjectInstance = 0, PropertyId = 85, Name = "SupplyTemperature" },
            new BacnetDataPoint { ObjectType = 0, ObjectInstance = 1, PropertyId = 85, Name = "ReturnTemperature" },
            new BacnetDataPoint { ObjectType = 2, ObjectInstance = 0, PropertyId = 85, Name = "Pressure" },
            new BacnetDataPoint { ObjectType = 1, ObjectInstance = 0, PropertyId = 85, Name = "FlowRate" },
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 1, PropertyId = 85, Name = "Current" },
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 2, PropertyId = 85, Name = "Voltage" },
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 3, PropertyId = 85, Name = "Frequency" }
        },
        ["screw_chiller"] = new[]
        {
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 0, PropertyId = 85, Name = "Power" },
            new BacnetDataPoint { ObjectType = 0, ObjectInstance = 0, PropertyId = 85, Name = "SupplyTemperature" },
            new BacnetDataPoint { ObjectType = 0, ObjectInstance = 1, PropertyId = 85, Name = "ReturnTemperature" },
            new BacnetDataPoint { ObjectType = 2, ObjectInstance = 0, PropertyId = 85, Name = "Pressure" },
            new BacnetDataPoint { ObjectType = 1, ObjectInstance = 0, PropertyId = 85, Name = "FlowRate" },
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 1, PropertyId = 85, Name = "Current" },
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 2, PropertyId = 85, Name = "Voltage" },
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 3, PropertyId = 85, Name = "Frequency" }
        },
        ["cooling_tower"] = new[]
        {
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 0, PropertyId = 85, Name = "Power" },
            new BacnetDataPoint { ObjectType = 0, ObjectInstance = 0, PropertyId = 85, Name = "InletTemperature" },
            new BacnetDataPoint { ObjectType = 0, ObjectInstance = 1, PropertyId = 85, Name = "OutletTemperature" },
            new BacnetDataPoint { ObjectType = 1, ObjectInstance = 0, PropertyId = 85, Name = "FlowRate" },
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 1, PropertyId = 85, Name = "FanSpeed" },
            new BacnetDataPoint { ObjectType = 2, ObjectInstance = 0, PropertyId = 85, Name = "Pressure" }
        },
        ["chilled_water_pump"] = new[]
        {
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 0, PropertyId = 85, Name = "Power" },
            new BacnetDataPoint { ObjectType = 0, ObjectInstance = 0, PropertyId = 85, Name = "SupplyTemperature" },
            new BacnetDataPoint { ObjectType = 0, ObjectInstance = 1, PropertyId = 85, Name = "ReturnTemperature" },
            new BacnetDataPoint { ObjectType = 2, ObjectInstance = 0, PropertyId = 85, Name = "Pressure" },
            new BacnetDataPoint { ObjectType = 1, ObjectInstance = 0, PropertyId = 85, Name = "FlowRate" },
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 1, PropertyId = 85, Name = "Frequency" },
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 2, PropertyId = 85, Name = "Current" },
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 3, PropertyId = 85, Name = "Voltage" }
        },
        ["cooling_water_pump"] = new[]
        {
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 0, PropertyId = 85, Name = "Power" },
            new BacnetDataPoint { ObjectType = 0, ObjectInstance = 0, PropertyId = 85, Name = "SupplyTemperature" },
            new BacnetDataPoint { ObjectType = 0, ObjectInstance = 1, PropertyId = 85, Name = "ReturnTemperature" },
            new BacnetDataPoint { ObjectType = 2, ObjectInstance = 0, PropertyId = 85, Name = "Pressure" },
            new BacnetDataPoint { ObjectType = 1, ObjectInstance = 0, PropertyId = 85, Name = "FlowRate" },
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 1, PropertyId = 85, Name = "Frequency" },
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 2, PropertyId = 85, Name = "Current" },
            new BacnetDataPoint { ObjectType = 4, ObjectInstance = 3, PropertyId = 85, Name = "Voltage" }
        }
    };

    private readonly Random _random = new Random(42);

    public BacnetGateway(
        IDeviceRepository deviceRepository,
        ITimeSeriesRepository timeSeriesRepository,
        IDeviceDataService deviceDataService,
        IMediator mediator,
        ILogger<BacnetGateway> logger)
    {
        _deviceRepository = deviceRepository;
        _timeSeriesRepository = timeSeriesRepository;
        _deviceDataService = deviceDataService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task StartCollectionAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BacnetGateway 数据采集服务已启动");

        try
        {
            InitializeUdpClient();

            var receiveTask = StartReceiveLoopAsync(cancellationToken);
            var processTask = StartProcessLoopAsync(cancellationToken);
            var pollTask = StartPollingLoopAsync(cancellationToken);

            await Task.WhenAll(receiveTask, processTask, pollTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BacnetGateway 数据采集服务发生致命错误");
        }
        finally
        {
            _udpClient?.Close();
            _udpClient?.Dispose();
            _queueSemaphore.Dispose();
            _logger.LogInformation("BacnetGateway 数据采集服务已停止");
        }
    }

    private void InitializeUdpClient()
    {
        var port = 47808;
        _udpClient = new UdpClient();

        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBufferSize, 8 * 1024 * 1024);
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBufferSize, 2 * 1024 * 1024);
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.NoChecksum, false);
        _udpClient.Client.ReceiveTimeout = 5000;

        try
        {
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            _logger.LogInformation("UDP Socket已绑定到端口 {Port}，接收缓冲区: {BufferSize}MB", port, 8);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            _logger.LogWarning("端口 {Port} 已被占用，尝试使用任意可用端口", port);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            var localPort = ((IPEndPoint)_udpClient.Client.LocalEndPoint!).Port;
            _logger.LogInformation("已绑定到备用端口 {Port}", localPort);
        }
    }

    private async Task StartReceiveLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UDP接收循环已启动");

        while (!cancellationToken.IsCancellationRequested)
        {
            BacnetReceivedPacket? packet = null;
            try
            {
                packet = _packetPool.Rent();
                var result = await _udpClient!.ReceiveAsync(cancellationToken);

                if (result.Buffer.Length > packet.Data.Length)
                {
                    _logger.LogWarning("接收到的数据包过大 ({Size} bytes)，丢弃", result.Buffer.Length);
                    _packetPool.Return(packet);
                    continue;
                }

                Buffer.BlockCopy(result.Buffer, 0, packet.Data, 0, result.Buffer.Length);
                packet.Length = result.Buffer.Length;
                packet.RemoteEndPoint = result.RemoteEndPoint;
                packet.ReceivedTime = DateTime.UtcNow;

                _receiveQueue.Enqueue(packet);
                _queueSemaphore.Release();

                if (_receiveQueue.Count > 1000)
                {
                    _logger.LogWarning("接收队列积压严重，当前队列长度: {Count}", _receiveQueue.Count);
                }
            }
            catch (OperationCanceledException)
            {
                if (packet != null) _packetPool.Return(packet);
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                if (packet != null) _packetPool.Return(packet);
                continue;
            }
            catch (Exception ex)
            {
                if (packet != null) _packetPool.Return(packet);
                _logger.LogError(ex, "UDP接收发生错误");
                await Task.Delay(100, cancellationToken);
            }
        }

        _logger.LogInformation("UDP接收循环已停止");
    }

    private async Task StartProcessLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("数据包处理循环已启动");

        var processors = new Task[Environment.ProcessorCount];
        for (int i = 0; i < processors.Length; i++)
        {
            processors[i] = ProcessPacketsAsync(cancellationToken, i);
        }

        await Task.WhenAll(processors);

        _logger.LogInformation("数据包处理循环已停止");
    }

    private async Task ProcessPacketsAsync(CancellationToken cancellationToken, int processorId)
    {
        _logger.LogDebug("数据包处理器 {Id} 已启动", processorId);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _queueSemaphore.WaitAsync(cancellationToken);

                if (_receiveQueue.TryDequeue(out var packet))
                {
                    try
                    {
                        await ProcessReceivedPacketAsync(packet);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "处理BACnet数据包失败");
                    }
                    finally
                    {
                        _packetPool.Return(packet);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "数据包处理器 {Id} 发生错误", processorId);
            }
        }

        _logger.LogDebug("数据包处理器 {Id} 已停止", processorId);
    }

    private async Task ProcessReceivedPacketAsync(BacnetReceivedPacket packet)
    {
        await Task.CompletedTask;
    }

    private async Task StartPollingLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("设备轮询循环已启动");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var devices = await _deviceRepository.GetAllAsync();
                var deviceList = devices.ToList();
                var allDeviceData = new List<DeviceData>();
                var timestamp = DateTime.UtcNow;

                foreach (var device in deviceList)
                {
                    if (device.Status == DeviceStatus.Fault)
                    {
                        continue;
                    }

                    var data = await ReadDeviceDataInternalAsync(device, timestamp);
                    if (data != null)
                    {
                        allDeviceData.Add(data);
                    }
                }

                if (allDeviceData.Any())
                {
                    await _deviceDataService.AddRangeDeviceDataAsync(allDeviceData);
                    _logger.LogInformation("已采集 {Count} 台设备数据，队列长度: {QueueCount}", allDeviceData.Count, _receiveQueue.Count);

                    await _mediator.Publish(new DeviceDataCollectedEvent
                    {
                        DeviceData = allDeviceData,
                        CollectedAt = timestamp
                    }, cancellationToken);
                }

                await Task.Delay(30000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设备轮询发生错误");
                await Task.Delay(5000, cancellationToken);
            }
        }

        _logger.LogInformation("设备轮询循环已停止");
    }

    public async Task<DeviceData?> ReadDeviceDataAsync(string deviceId)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null) return null;

        return await ReadDeviceDataInternalAsync(device, DateTime.UtcNow);
    }

    public async Task<IEnumerable<Device>> GetAllDevicesAsync()
    {
        return await _deviceRepository.GetAllAsync();
    }

    private async Task<DeviceData?> ReadDeviceDataInternalAsync(Device device, DateTime timestamp)
    {
        try
        {
            var deviceTypeKey = GetDeviceTypeKey(device.DeviceTypeId);
            if (!_dataPoints.ContainsKey(deviceTypeKey)) return null;

            var dataPoints = _dataPoints[deviceTypeKey];
            var data = new Dictionary<string, decimal>();

            foreach (var point in dataPoints)
            {
                var value = await SimulateBacnetReadAsync(device, point);
                data[point.Name] = value;
            }

            var deviceData = new DeviceData
            {
                DeviceId = device.Id,
                Timestamp = timestamp,
                Power = data.GetValueOrDefault("Power", 0),
                SupplyTemperature = data.GetValueOrDefault("SupplyTemperature", data.GetValueOrDefault("OutletTemperature", 0)),
                ReturnTemperature = data.GetValueOrDefault("ReturnTemperature", data.GetValueOrDefault("InletTemperature", 0)),
                Pressure = data.GetValueOrDefault("Pressure", 0),
                FlowRate = data.GetValueOrDefault("FlowRate", 0),
                Frequency = data.GetValueOrDefault("Frequency", 50),
                Current = data.GetValueOrDefault("Current", 0),
                Voltage = data.GetValueOrDefault("Voltage", 380),
                InletTemperature = data.GetValueOrDefault("InletTemperature", null),
                OutletTemperature = data.GetValueOrDefault("OutletTemperature", null),
                FanSpeed = data.GetValueOrDefault("FanSpeed", null)
            };

            return deviceData;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取设备 {DeviceId} 数据失败", device.Id);
            return null;
        }
    }

    private async Task<decimal> SimulateBacnetReadAsync(Device device, BacnetDataPoint point)
    {
        await Task.Delay(1);

        var isRunning = device.Status == DeviceStatus.Running;

        var baseValue = GetBaseValue(device.DeviceTypeId, point.Name);
        var variation = (decimal)(_random.NextDouble() * 0.1 - 0.05) * baseValue;

        return isRunning ? Math.Max(0, baseValue + variation) : 0;
    }

    private decimal GetBaseValue(DeviceType deviceType, string parameterName)
    {
        var baseValues = new Dictionary<(DeviceType, string), decimal>
        {
            [(DeviceType.CentrifugalChiller, "Power")] = 850,
            [(DeviceType.CentrifugalChiller, "SupplyTemperature")] = 7.0m,
            [(DeviceType.CentrifugalChiller, "ReturnTemperature")] = 14.0m,
            [(DeviceType.CentrifugalChiller, "Pressure")] = 1.2m,
            [(DeviceType.CentrifugalChiller, "FlowRate")] = 900,
            [(DeviceType.CentrifugalChiller, "Current")] = 150,
            [(DeviceType.CentrifugalChiller, "Voltage")] = 380,
            [(DeviceType.CentrifugalChiller, "Frequency")] = 48,

            [(DeviceType.ScrewChiller, "Power")] = 520,
            [(DeviceType.ScrewChiller, "SupplyTemperature")] = 7.2m,
            [(DeviceType.ScrewChiller, "ReturnTemperature")] = 13.8m,
            [(DeviceType.ScrewChiller, "Pressure")] = 1.15m,
            [(DeviceType.ScrewChiller, "FlowRate")] = 450,
            [(DeviceType.ScrewChiller, "Current")] = 95,
            [(DeviceType.ScrewChiller, "Voltage")] = 380,
            [(DeviceType.ScrewChiller, "Frequency")] = 47,

            [(DeviceType.CoolingTower, "Power")] = 18,
            [(DeviceType.CoolingTower, "InletTemperature")] = 32.0m,
            [(DeviceType.CoolingTower, "OutletTemperature")] = 28.0m,
            [(DeviceType.CoolingTower, "FlowRate")] = 900,
            [(DeviceType.CoolingTower, "FanSpeed")] = 1200,
            [(DeviceType.CoolingTower, "Pressure")] = 0.3m,

            [(DeviceType.ChilledWaterPump, "Power")] = 75,
            [(DeviceType.ChilledWaterPump, "SupplyTemperature")] = 7.0m,
            [(DeviceType.ChilledWaterPump, "ReturnTemperature")] = 14.0m,
            [(DeviceType.ChilledWaterPump, "Pressure")] = 1.5m,
            [(DeviceType.ChilledWaterPump, "FlowRate")] = 450,
            [(DeviceType.ChilledWaterPump, "Frequency")] = 45,
            [(DeviceType.ChilledWaterPump, "Current")] = 135,
            [(DeviceType.ChilledWaterPump, "Voltage")] = 380,

            [(DeviceType.CoolingWaterPump, "Power")] = 78,
            [(DeviceType.CoolingWaterPump, "SupplyTemperature")] = 28.0m,
            [(DeviceType.CoolingWaterPump, "ReturnTemperature")] = 33.0m,
            [(DeviceType.CoolingWaterPump, "Pressure")] = 1.4m,
            [(DeviceType.CoolingWaterPump, "FlowRate")] = 500,
            [(DeviceType.CoolingWaterPump, "Frequency")] = 46,
            [(DeviceType.CoolingWaterPump, "Current")] = 140,
            [(DeviceType.CoolingWaterPump, "Voltage")] = 380,
        };

        return baseValues.TryGetValue((deviceType, parameterName), out var value) ? value : 0;
    }

    private string GetDeviceTypeKey(DeviceType deviceType)
    {
        return deviceType switch
        {
            DeviceType.CentrifugalChiller => "centrifugal_chiller",
            DeviceType.ScrewChiller => "screw_chiller",
            DeviceType.CoolingTower => "cooling_tower",
            DeviceType.ChilledWaterPump => "chilled_water_pump",
            DeviceType.CoolingWaterPump => "cooling_water_pump",
            _ => "unknown"
        };
    }
}
