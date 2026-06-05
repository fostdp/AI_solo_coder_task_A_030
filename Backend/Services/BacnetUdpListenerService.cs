using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ChillerPlant.Data;
using ChillerPlant.Data.Repositories;
using ChillerPlant.Models;

namespace ChillerPlant.Services
{
    public class BacnetUdpListenerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BacnetUdpListenerService> _logger;
        private readonly AppSettings _appSettings;
        private readonly BacnetSettings _bacnetSettings;

        private UdpClient _udpClient;
        private ConcurrentQueue<BacnetDataDto> _receiveQueue;
        private SemaphoreSlim _queueSemaphore;
        private List<Thread> _workerThreads;
        private CancellationTokenSource _cts;
        private int _maxQueueSize = 10000;
        private int _workerCount = 4;

        private ConcurrentDictionary<int, DateTime> _deviceLastSeen;
        private ConcurrentDictionary<int, int> _consecutiveMissed;
        private int _offlineThreshold = 5;

        public BacnetUdpListenerService(IServiceProvider serviceProvider,
            ILogger<BacnetUdpListenerService> logger,
            IOptions<AppSettings> appSettings,
            IOptions<BacnetSettings> bacnetSettings)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _appSettings = appSettings.Value;
            _bacnetSettings = bacnetSettings.Value;

            _receiveQueue = new ConcurrentQueue<BacnetDataDto>();
            _queueSemaphore = new SemaphoreSlim(0, _maxQueueSize);
            _deviceLastSeen = new ConcurrentDictionary<int, DateTime>();
            _consecutiveMissed = new ConcurrentDictionary<int, int>();
            _workerThreads = new List<Thread>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var port = _bacnetSettings.ListenPort;

            try
            {
                _udpClient = new UdpClient();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBufferSize, 8 * 1024 * 1024);
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBufferSize, 4 * 1024 * 1024);
                _udpClient.Client.ReceiveTimeout = 5000;
                _udpClient.Client.SendTimeout = 5000;

                try
                {
                    _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                    _logger.LogInformation($"BACnet/IP UDP Listener started on port {port}, ReceiveBufferSize: {_udpClient.Client.ReceiveBufferSize / 1024 / 1024}MB");
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    _logger.LogWarning($"Port {port} already in use, BACnet UDP listener will work in passive mode");
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                    return;
                }

                for (int i = 0; i < _workerCount; i++)
                {
                    var workerThread = new Thread(ProcessQueueWorker)
                    {
                        Name = $"BacnetWorker-{i}",
                        IsBackground = true,
                        Priority = ThreadPriority.AboveNormal
                    };
                    workerThread.Start(stoppingToken);
                    _workerThreads.Add(workerThread);
                }

                var receiveThread = new Thread(ReceiveLoop)
                {
                    Name = "BacnetReceiver",
                    IsBackground = true,
                    Priority = ThreadPriority.Highest
                };
                receiveThread.Start(stoppingToken);

                var monitorThread = new Thread(MonitorDeviceStatus)
                {
                    Name = "BacnetMonitor",
                    IsBackground = true,
                    Priority = ThreadPriority.Normal
                };
                monitorThread.Start(stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                    _logger.LogDebug($"Queue size: {_receiveQueue.Count}, Semaphore count: {_queueSemaphore.CurrentCount}");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("BACnet/IP UDP Listener stopping...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in BACnet/IP UDP Listener");
            }
            finally
            {
                Cleanup();
            }
        }

        private void ReceiveLoop(object state)
        {
            var token = (CancellationToken)state;
            var remoteEp = new IPEndPoint(IPAddress.Any, 0);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var buffer = _udpClient.Receive(ref remoteEp);
                    if (buffer == null || buffer.Length == 0) continue;

                    var bacnetData = ParseBacnetData(buffer, remoteEp);
                    if (bacnetData == null) continue;

                    if (_receiveQueue.Count >= _maxQueueSize)
                    {
                        _logger.LogWarning($"Receive queue overflow, dropping packet from {remoteEp}, queue size: {_receiveQueue.Count}");
                        if (_receiveQueue.TryDequeue(out var dropped))
                        {
                            _queueSemaphore.Wait(token);
                        }
                    }

                    _receiveQueue.Enqueue(bacnetData);
                    _queueSemaphore.Release();

                    _deviceLastSeen.AddOrUpdate(bacnetData.BacnetInstance, 
                        _ => DateTime.Now, 
                        (_, _) => DateTime.Now);
                    _consecutiveMissed.AddOrUpdate(bacnetData.BacnetInstance, 0, (_, _) => 0);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    continue;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.LogError(ex, "Error receiving BACnet packet");
                    Thread.Sleep(100);
                }
            }
        }

        private void ProcessQueueWorker(object state)
        {
            var token = (CancellationToken)state;
            var batchSize = 10;
            var batch = new List<BacnetDataDto>(batchSize);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    _queueSemaphore.Wait(token);

                    batch.Clear();
                    for (int i = 0; i < batchSize; i++)
                    {
                        if (_receiveQueue.TryDequeue(out var item))
                        {
                            batch.Add(item);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (batch.Count > 0)
                    {
                        ProcessBatch(batch).Wait(token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing BACnet data batch");
                    Thread.Sleep(500);
                }
            }
        }

        private async Task ProcessBatch(List<BacnetDataDto> batch)
        {
            using var scope = _serviceProvider.CreateScope();
            var deviceRepository = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
            var hubContext = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<RealtimeHub>>();

            var results = new List<DeviceData>();
            foreach (var data in batch)
            {
                try
                {
                    var deviceData = await deviceRepository.InsertDeviceDataAsync(data);
                    if (deviceData != null)
                    {
                        results.Add(deviceData);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing BACnet data for instance {data.BacnetInstance}");
                }
            }

            if (results.Count > 0)
            {
                var updates = results.Select(d => new
                {
                    d.DeviceId,
                    d.Power,
                    d.COP,
                    d.LoadRate,
                    d.Timestamp
                }).ToList();

                await hubContext.Clients.All.SendAsync("DeviceDataBatchUpdated", updates);
            }
        }

        private void MonitorDeviceStatus(object state)
        {
            var token = (CancellationToken)state;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    var timeout = TimeSpan.FromSeconds(_appSettings.DataReportIntervalSeconds * 3);

                    foreach (var kvp in _deviceLastSeen)
                    {
                        if (now - kvp.Value > timeout)
                        {
                            _consecutiveMissed.AddOrUpdate(kvp.Key, 1, (_, old) => old + 1);
                        }
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var hubContext = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<RealtimeHub>>();

                    var offlineDevices = new List<int>();
                    foreach (var kvp in _consecutiveMissed)
                    {
                        if (kvp.Value >= _offlineThreshold)
                        {
                            offlineDevices.Add(kvp.Key);
                        }
                    }

                    if (offlineDevices.Count > 0)
                    {
                        var devices = await context.Devices
                            .Where(d => offlineDevices.Contains(d.BacnetInstance))
                            .ToListAsync(token);

                        foreach (var device in devices)
                        {
                            if (device.Status != -1)
                            {
                                device.Status = -1;
                                _logger.LogWarning($"Device {device.DeviceCode} marked as offline (missed {_consecutiveMissed[device.BacnetInstance]} polls)");
                            }
                        }

                        await context.SaveChangesAsync(token);
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.LogError(ex, "Error monitoring device status");
                }

                for (int i = 0; i < 30 && !token.IsCancellationRequested; i++)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        private BacnetDataDto ParseBacnetData(byte[] buffer, IPEndPoint remoteEp)
        {
            try
            {
                if (buffer.Length < 8) return null;

                if (buffer[0] == 0x81 && buffer[1] == 0x0a)
                {
                    return ParseStandardBacnet(buffer);
                }

                var jsonStr = Encoding.UTF8.GetString(buffer).Trim('\0');
                if (!string.IsNullOrWhiteSpace(jsonStr) && (jsonStr.StartsWith('{') || jsonStr.StartsWith('[')))
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true
                    };
                    return JsonSerializer.Deserialize<BacnetDataDto>(jsonStr, options);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, $"Failed to parse BACnet packet from {remoteEp}, length: {buffer.Length}");
                return null;
            }
        }

        private BacnetDataDto ParseStandardBacnet(byte[] buffer)
        {
            try
            {
                if (buffer.Length < 20) return null;

                var instance = BitConverter.ToInt32(buffer, 8);
                if (instance <= 0) return null;

                var data = new BacnetDataDto
                {
                    BacnetInstance = instance,
                    Timestamp = DateTime.Now
                };

                var offset = 12;
                if (offset + 8 <= buffer.Length)
                {
                    data.Power = (decimal)BitConverter.ToSingle(buffer, offset);
                    offset += 4;
                    data.LoadRate = (decimal)BitConverter.ToSingle(buffer, offset);
                    offset += 4;
                }

                if (offset + 16 <= buffer.Length)
                {
                    data.SupplyWaterTemp = (decimal)BitConverter.ToSingle(buffer, offset);
                    offset += 4;
                    data.ReturnWaterTemp = (decimal)BitConverter.ToSingle(buffer, offset);
                    offset += 4;
                    data.CoolingWaterInTemp = (decimal)BitConverter.ToSingle(buffer, offset);
                    offset += 4;
                    data.CoolingWaterOutTemp = (decimal)BitConverter.ToSingle(buffer, offset);
                    offset += 4;
                }

                if (offset + 8 <= buffer.Length)
                {
                    data.FlowRate = (decimal)BitConverter.ToSingle(buffer, offset);
                    offset += 4;
                    data.Status = BitConverter.ToInt32(buffer, offset);
                    offset += 4;
                }

                return data;
            }
            catch
            {
                return null;
            }
        }

        private void Cleanup()
        {
            try
            {
                _cts?.Cancel();
                _udpClient?.Close();
                _udpClient?.Dispose();
                _queueSemaphore?.Dispose();

                foreach (var thread in _workerThreads)
                {
                    if (thread != null && thread.IsAlive)
                    {
                        thread.Join(2000);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
            }
        }

        public override void Dispose()
        {
            Cleanup();
            base.Dispose();
        }
    }

    public class BacnetSettings
    {
        public int ListenPort { get; set; } = 47808;
        public string LocalAddress { get; set; } = "0.0.0.0";
    }
}
