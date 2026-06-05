using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ChillerPlant.Modules.BacnetGateway.Models;
using ChillerPlant.Modules.Shared.Commands;
using ChillerPlant.Configuration;

namespace ChillerPlant.Modules.BacnetGateway.Services
{
    public class BacnetUdpListenerService : BackgroundService
    {
        private readonly ILogger<BacnetUdpListenerService> _logger;
        private readonly IMediator _mediator;
        private readonly BacnetSettings _settings;
        private readonly BacnetProtocolParser _parser;
        
        private UdpClient _udpClient;
        private ConcurrentQueue<BacnetDataDto> _receiveQueue;
        private SemaphoreSlim _queueSemaphore;
        private int _maxQueueSize = 1000;
        private int _workerThreads = 4;
        private List<Task> _workerTasks;
        private CancellationTokenSource _cts;

        public BacnetUdpListenerService(
            ILogger<BacnetUdpListenerService> logger,
            IMediator mediator,
            IOptions<BacnetSettings> settings,
            BacnetProtocolParser parser)
        {
            _logger = logger;
            _mediator = mediator;
            _settings = settings.Value;
            _parser = parser;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _receiveQueue = new ConcurrentQueue<BacnetDataDto>();
            _queueSemaphore = new SemaphoreSlim(0, _maxQueueSize);
            _workerTasks = new List<Task>();

            try
            {
                _udpClient = new UdpClient();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, 
                    SocketOptionName.ReceiveBufferSize, 8 * 1024 * 1024);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _settings.Port));

                _logger.LogInformation($"BACnet/IP listener started on port {_settings.Port}, 8MB buffer");

                for (int i = 0; i < _workerThreads; i++)
                {
                    var workerId = i;
                    _workerTasks.Add(ProcessQueueWorker(workerId, _cts.Token));
                }

                var pollingTask = PollDevices(_cts.Token);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var receiveTask = _udpClient.ReceiveAsync();
                    var timeoutTask = Task.Delay(5000, stoppingToken);
                    
                    var completed = await Task.WhenAny(receiveTask, timeoutTask);
                    if (completed == receiveTask)
                    {
                        try
                        {
                            var result = await receiveTask;
                            ProcessReceivedData(result.Buffer, result.Buffer.Length, result.RemoteEndPoint);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error processing UDP data: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"BACnet listener error: {ex.Message}");
            }
        }

        private void ProcessReceivedData(byte[] buffer, int received, IPEndPoint remote)
        {
            try
            {
                if (_parser.TryParseBacnetIpPacket(buffer, received, out var data, out var bacnetInstance))
                {
                    if (data != null)
                    {
                        data.RemoteEndpoint = remote.ToString();
                        _receiveQueue.Enqueue(data);
                        if (_queueSemaphore.CurrentCount < _maxQueueSize)
                        {
                            _queueSemaphore.Release();
                        }
                        _logger.LogDebug($"Enqueued BACnet data from instance {bacnetInstance}, queue size: {_receiveQueue.Count}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing BACnet packet: {ex.Message}");
            }
        }

        private async Task ProcessQueueWorker(int workerId, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"BACnet worker thread {workerId} started");
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _queueSemaphore.WaitAsync(cancellationToken);
                    
                    if (_receiveQueue.TryDequeue(out var data))
                    {
                        await ProcessBacnetData(data);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Worker {workerId} error: {ex.Message}");
                }
            }
            
            _logger.LogInformation($"BACnet worker thread {workerId} stopped");
        }

        private async Task ProcessBacnetData(BacnetDataDto data)
        {
            try
            {
                var command = new InsertDeviceDataCommand
                {
                    BacnetInstance = data.BacnetInstance,
                    Power = data.Power,
                    SupplyWaterTemp = data.SupplyWaterTemp,
                    ReturnWaterTemp = data.ReturnWaterTemp,
                    CoolingWaterInTemp = data.CoolingWaterInTemp,
                    CoolingWaterOutTemp = data.CoolingWaterOutTemp,
                    FlowRate = data.FlowRate,
                    SupplyPressure = data.SupplyPressure,
                    ReturnPressure = data.ReturnPressure,
                    LoadRate = data.LoadRate,
                    Frequency = data.Frequency,
                    Vibration = data.Vibration,
                    Current = data.Current,
                    Voltage = data.Voltage,
                    RunningHours = data.RunningHours,
                    Status = data.Status,
                    Timestamp = data.Timestamp
                };

                await _mediator.Send(command);
                _logger.LogDebug($"Processed BACnet data for instance {data.BacnetInstance}, Power={data.Power}kW");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving BACnet data for instance {data.BacnetInstance}: {ex.Message}");
            }
        }

        private async Task PollDevices(CancellationToken cancellationToken)
        {
            var lastPollTime = DateTime.MinValue;
            var pollInterval = TimeSpan.FromSeconds(_settings.PollIntervalSeconds);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if ((DateTime.Now - lastPollTime) >= pollInterval)
                    {
                        foreach (var deviceIp in _settings.DeviceIPs)
                        {
                            try
                            {
                                var endpoint = new IPEndPoint(IPAddress.Parse(deviceIp), _settings.Port);
                                foreach (var instance in _settings.DeviceInstances)
                                {
                                    var request = _parser.BuildReadPropertyMultipleRequest(
                                        instance, 
                                        _parser.GetStandardPropertyIds());
                                    await _udpClient.SendAsync(request, request.Length, endpoint);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Failed to poll device {deviceIp}: {ex.Message}");
                            }
                        }
                        lastPollTime = DateTime.Now;
                        _logger.LogDebug($"Polled {_settings.DeviceInstances.Count} devices");
                    }
                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Polling error: {ex.Message}");
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }

        public override void Dispose()
        {
            _cts?.Cancel();
            _udpClient?.Close();
            _udpClient?.Dispose();
            _queueSemaphore?.Dispose();
            base.Dispose();
        }
    }
}
