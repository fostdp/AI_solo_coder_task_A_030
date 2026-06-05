using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ChillerPlant.Data;
using ChillerPlant.Models;
using ChillerPlant.Modules.Shared.Commands;
using ChillerPlant.Modules.Shared.Events;

namespace ChillerPlant.Modules.BacnetGateway.Handlers
{
    public class InsertDeviceDataHandler : IRequestHandler<InsertDeviceDataCommand, DeviceData>
    {
        private readonly ApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<InsertDeviceDataHandler> _logger;

        public InsertDeviceDataHandler(ApplicationDbContext context, 
            IMediator mediator,
            ILogger<InsertDeviceDataHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<DeviceData> Handle(InsertDeviceDataCommand request, CancellationToken cancellationToken)
        {
            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.BacnetInstance == request.BacnetInstance, cancellationToken);
            
            if (device == null)
            {
                _logger.LogWarning($"Device not found for BACnet instance {request.BacnetInstance}");
                return null;
            }

            var deviceData = new DeviceData
            {
                DeviceId = device.DeviceId,
                Power = request.Power,
                SupplyWaterTemp = request.SupplyWaterTemp,
                ReturnWaterTemp = request.ReturnWaterTemp,
                CoolingWaterInTemp = request.CoolingWaterInTemp,
                CoolingWaterOutTemp = request.CoolingWaterOutTemp,
                FlowRate = request.FlowRate,
                SupplyPressure = request.SupplyPressure,
                ReturnPressure = request.ReturnPressure,
                LoadRate = request.LoadRate,
                Frequency = request.Frequency,
                Vibration = request.Vibration,
                Current = request.Current,
                Voltage = request.Voltage,
                RunningHours = request.RunningHours,
                Status = request.Status,
                COP = CalculateCOP(request),
                Timestamp = request.Timestamp ?? DateTime.Now
            };

            _context.DeviceData.Add(deviceData);

            var oldStatus = device.Status;
            if (device.Status != request.Status)
            {
                device.Status = request.Status;
                var statusChangedEvent = new DeviceStatusChangedEvent
                {
                    DeviceId = device.DeviceId,
                    OldStatus = oldStatus,
                    NewStatus = request.Status,
                    ChangedAt = DateTime.Now
                };
                await _mediator.Publish(statusChangedEvent, cancellationToken);
            }
            
            device.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);

            var dataReceivedEvent = new DeviceDataReceivedEvent
            {
                DeviceId = device.DeviceId,
                BacnetInstance = request.BacnetInstance,
                Power = request.Power,
                COP = deviceData.COP,
                LoadRate = request.LoadRate,
                SupplyWaterTemp = request.SupplyWaterTemp,
                ReturnWaterTemp = request.ReturnWaterTemp,
                CoolingWaterInTemp = request.CoolingWaterInTemp,
                Timestamp = deviceData.Timestamp,
                Status = request.Status
            };
            await _mediator.Publish(dataReceivedEvent, cancellationToken);

            _logger.LogDebug($"Device data inserted: DeviceId={device.DeviceId}, Power={request.Power}kW");
            return deviceData;
        }

        private decimal? CalculateCOP(InsertDeviceDataCommand data)
        {
            if (data.Power <= 0 || !data.FlowRate.HasValue || 
                !data.SupplyWaterTemp.HasValue || !data.ReturnWaterTemp.HasValue)
                return null;

            var deltaT = Math.Abs(data.ReturnWaterTemp.Value - data.SupplyWaterTemp.Value);
            if (deltaT <= 0) return null;

            var coolingCapacity = data.FlowRate.Value * deltaT * 1.163m;
            return coolingCapacity / data.Power;
        }
    }

    public class InsertBatchDeviceDataHandler : IRequestHandler<InsertBatchDeviceDataCommand, int>
    {
        private readonly ApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<InsertBatchDeviceDataHandler> _logger;

        public InsertBatchDeviceDataHandler(ApplicationDbContext context,
            IMediator mediator,
            ILogger<InsertBatchDeviceDataHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<int> Handle(InsertBatchDeviceDataCommand request, CancellationToken cancellationToken)
        {
            var processedCount = 0;
            var dataPoints = new List<DeviceDataPoint>();

            foreach (var cmd in request.DataList)
            {
                var device = await _context.Devices
                    .FirstOrDefaultAsync(d => d.BacnetInstance == cmd.BacnetInstance, cancellationToken);
                
                if (device == null) continue;

                var deviceData = new DeviceData
                {
                    DeviceId = device.DeviceId,
                    Power = cmd.Power,
                    SupplyWaterTemp = cmd.SupplyWaterTemp,
                    ReturnWaterTemp = cmd.ReturnWaterTemp,
                    CoolingWaterInTemp = cmd.CoolingWaterInTemp,
                    CoolingWaterOutTemp = cmd.CoolingWaterOutTemp,
                    FlowRate = cmd.FlowRate,
                    LoadRate = cmd.LoadRate,
                    Status = cmd.Status,
                    COP = CalculateCOP(cmd),
                    Timestamp = cmd.Timestamp ?? DateTime.Now
                };

                _context.DeviceData.Add(deviceData);
                device.Status = cmd.Status;
                device.UpdatedAt = DateTime.Now;

                dataPoints.Add(new DeviceDataPoint
                {
                    DeviceId = device.DeviceId,
                    Power = cmd.Power,
                    COP = deviceData.COP,
                    LoadRate = cmd.LoadRate,
                    Timestamp = deviceData.Timestamp
                });

                processedCount++;
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (dataPoints.Count > 0)
            {
                var batchEvent = new DeviceDataBatchReceivedEvent
                {
                    DataPoints = dataPoints
                };
                await _mediator.Publish(batchEvent, cancellationToken);
            }

            _logger.LogInformation($"Batch device data inserted: {processedCount} records");
            return processedCount;
        }

        private decimal? CalculateCOP(InsertDeviceDataCommand data)
        {
            if (data.Power <= 0 || !data.FlowRate.HasValue || 
                !data.SupplyWaterTemp.HasValue || !data.ReturnWaterTemp.HasValue)
                return null;

            var deltaT = Math.Abs(data.ReturnWaterTemp.Value - data.SupplyWaterTemp.Value);
            if (deltaT <= 0) return null;

            var coolingCapacity = data.FlowRate.Value * deltaT * 1.163m;
            return coolingCapacity / data.Power;
        }
    }

    public class GetDeviceStatusHandler : IRequestHandler<GetDeviceStatusCommand, List<DeviceStatusDto>>
    {
        private readonly ApplicationDbContext _context;

        public GetDeviceStatusHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<DeviceStatusDto>> Handle(GetDeviceStatusCommand request, CancellationToken cancellationToken)
        {
            var devices = await _context.Devices
                .Include(d => d.DeviceType)
                .ToListAsync(cancellationToken);
            
            var latestData = await _context.DeviceData
                .GroupBy(d => d.DeviceId)
                .Select(g => new
                {
                    DeviceId = g.Key,
                    LatestData = g.OrderByDescending(d => d.Timestamp).FirstOrDefault()
                })
                .ToDictionaryAsync(d => d.DeviceId, d => d.LatestData, cancellationToken);

            var result = new List<DeviceStatusDto>();
            foreach (var device in devices)
            {
                var data = latestData.ContainsKey(device.DeviceId) ? latestData[device.DeviceId] : null;
                var efficiencyStatus = GetEfficiencyStatus(data?.COP, device.DesignCOP);
                
                result.Add(new DeviceStatusDto
                {
                    DeviceId = device.DeviceId,
                    DeviceName = device.DeviceName,
                    DeviceCode = device.DeviceCode,
                    DeviceTypeId = device.DeviceTypeId,
                    TypeName = device.DeviceType?.TypeName,
                    Status = device.Status,
                    CurrentPower = data?.Power,
                    CurrentLoadRate = data?.LoadRate,
                    CurrentCOP = data?.COP,
                    DesignCOP = device.DesignCOP,
                    EfficiencyStatus = efficiencyStatus.Status,
                    StatusColor = efficiencyStatus.Color,
                    X = device.X,
                    Y = device.Y,
                    LastUpdateTime = data?.Timestamp
                });
            }

            return result;
        }

        private (string Status, string Color) GetEfficiencyStatus(decimal? cop, decimal designCop)
        {
            if (!cop.HasValue) return ("未知", "#95a5a6");
            
            var ratio = cop.Value / designCop;
            if (ratio >= 0.9m) return ("高效", "#27ae60");
            if (ratio >= 0.7m) return ("正常", "#2ecc71");
            if (ratio >= 0.5m) return ("效率偏低", "#f39c12");
            return ("低效", "#e74c3c");
        }
    }

    public class GetDeviceTrendDataHandler : IRequestHandler<GetDeviceTrendDataCommand, List<DeviceTrendDataDto>>
    {
        private readonly ApplicationDbContext _context;

        public GetDeviceTrendDataHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<DeviceTrendDataDto>> Handle(GetDeviceTrendDataCommand request, CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now.AddHours(-request.Hours);
            return await _context.DeviceData
                .Where(d => d.DeviceId == request.DeviceId && d.Timestamp >= startTime)
                .OrderBy(d => d.Timestamp)
                .Select(d => new DeviceTrendDataDto
                {
                    Timestamp = d.Timestamp,
                    Power = d.Power,
                    SupplyWaterTemp = d.SupplyWaterTemp,
                    ReturnWaterTemp = d.ReturnWaterTemp,
                    CoolingWaterInTemp = d.CoolingWaterInTemp,
                    CoolingWaterOutTemp = d.CoolingWaterOutTemp,
                    FlowRate = d.FlowRate,
                    LoadRate = d.LoadRate,
                    COP = d.COP
                })
                .ToListAsync(cancellationToken);
        }
    }
}
