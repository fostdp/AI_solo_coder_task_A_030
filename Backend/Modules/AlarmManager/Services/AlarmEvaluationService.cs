using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ChillerPlant.Data;
using ChillerPlant.Models;
using AlarmModels = ChillerPlant.Modules.AlarmManager.Models;

namespace ChillerPlant.Modules.AlarmManager.Services
{
    public class AlarmEvaluationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AlarmEvaluationService> _logger;

        public AlarmEvaluationService(
            ApplicationDbContext context,
            ILogger<AlarmEvaluationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<AlarmDto>> EvaluateAlarms(CancellationToken cancellationToken)
        {
            var generatedAlarms = new List<AlarmDto>();
            
            try
            {
                var deviceStatusAlarms = await EvaluateDeviceStatusAlarms(cancellationToken);
                generatedAlarms.AddRange(deviceStatusAlarms);

                var thresholdAlarms = await EvaluateThresholdAlarms(cancellationToken);
                generatedAlarms.AddRange(thresholdAlarms);

                var efficiencyAlarms = await EvaluateEfficiencyAlarms(cancellationToken);
                generatedAlarms.AddRange(efficiencyAlarms);

                _logger.LogInformation($"Alarm evaluation completed: {generatedAlarms.Count} alarms generated");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error evaluating alarms: {ex.Message}");
            }

            return generatedAlarms;
        }

        private async Task<List<AlarmDto>> EvaluateDeviceStatusAlarms(CancellationToken cancellationToken)
        {
            var alarms = new List<AlarmDto>();
            var fiveMinutesAgo = DateTime.Now.AddMinutes(-5);

            var offlineDevices = await _context.Devices
                .Where(d => d.Status == 0 && d.UpdatedAt < fiveMinutesAgo)
                .Include(d => d.DeviceType)
                .ToListAsync(cancellationToken);

            foreach (var device in offlineDevices)
            {
                var existingAlarm = await _context.Alarms
                    .Where(a => a.DeviceId == device.DeviceId 
                        && a.AlarmType == "DeviceOffline" 
                        && a.EndTime == null)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existingAlarm == null)
                {
                    var alarm = new Alarm
                    {
                        DeviceId = device.DeviceId,
                        AlarmType = "DeviceOffline",
                        AlarmLevel = 2,
                        AlarmMessage = $"设备[{device.DeviceName}]离线超过5分钟",
                        StartTime = DateTime.Now,
                        Status = 1
                    };
                    _context.Alarms.Add(alarm);
                    await _context.SaveChangesAsync(cancellationToken);

                    await GenerateWorkOrder(alarm, device.DeviceName, cancellationToken);
                    alarms.Add(MapToDto(alarm, device.DeviceName));
                }
            }

            return alarms;
        }

        private async Task<List<AlarmDto>> EvaluateThresholdAlarms(CancellationToken cancellationToken)
        {
            var alarms = new List<AlarmDto>();
            var twoMinutesAgo = DateTime.Now.AddMinutes(-2);

            var recentData = await _context.DeviceData
                .Where(d => d.Timestamp >= twoMinutesAgo)
                .Include(d => d.Device)
                .GroupBy(d => d.DeviceId)
                .Select(g => new
                {
                    DeviceId = g.Key,
                    LatestData = g.OrderByDescending(d => d.Timestamp).FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            foreach (var item in recentData)
            {
                if (item.LatestData == null) continue;
                var data = item.LatestData;
                var device = data.Device;

                if (data.Power > 800)
                {
                    alarms.Add(await CreateAlarmIfNotExists(
                        device.DeviceId, "HighPower", 1,
                        $"设备[{device.DeviceName}]功率过高: {data.Power:F1}kW",
                        cancellationToken));
                }

                if (data.SupplyWaterTemp > 8)
                {
                    alarms.Add(await CreateAlarmIfNotExists(
                        device.DeviceId, "HighSupplyTemp", 1,
                        $"设备[{device.DeviceName}]冷冻水出水温度过高: {data.SupplyWaterTemp:F1}°C",
                        cancellationToken));
                }

                if (data.SupplyWaterTemp < 5)
                {
                    alarms.Add(await CreateAlarmIfNotExists(
                        device.DeviceId, "LowSupplyTemp", 2,
                        $"设备[{device.DeviceName}]冷冻水出水温度过低: {data.SupplyWaterTemp:F1}°C",
                        cancellationToken));
                }

                if (data.Vibration > 4.5)
                {
                    alarms.Add(await CreateAlarmIfNotExists(
                        device.DeviceId, "HighVibration", 2,
                        $"设备[{device.DeviceName}]振动值超标: {data.Vibration:F2}mm/s",
                        cancellationToken));
                }

                if (data.COP.HasValue && data.COP < 3 && data.Status == 1)
                {
                    alarms.Add(await CreateAlarmIfNotExists(
                        device.DeviceId, "LowCOP", 1,
                        $"设备[{device.DeviceName}]COP过低: {data.COP:F2}",
                        cancellationToken));
                }
            }

            return alarms.Where(a => a != null).ToList();
        }

        private async Task<List<AlarmDto>> EvaluateEfficiencyAlarms(CancellationToken cancellationToken)
        {
            var alarms = new List<AlarmDto>();
            var oneHourAgo = DateTime.Now.AddHours(-1);

            var lowEfficiencyData = await _context.SystemEfficiencies
                .Where(e => e.Timestamp >= oneHourAgo && e.COPRatio < 0.6m)
                .OrderByDescending(e => e.Timestamp)
                .Take(3)
                .ToListAsync(cancellationToken);

            if (lowEfficiencyData.Count >= 2)
            {
                var avgRatio = lowEfficiencyData.Average(e => e.COPRatio);
                var existingAlarm = await _context.Alarms
                    .Where(a => a.AlarmType == "SystemLowEfficiency" && a.EndTime == null)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existingAlarm == null)
                {
                    var alarm = new Alarm
                    {
                        AlarmType = "SystemLowEfficiency",
                        AlarmLevel = 1,
                        AlarmMessage = $"系统整体能效偏低，平均COP比率: {avgRatio:F2}，建议检查设备运行状态",
                        StartTime = DateTime.Now,
                        Status = 1
                    };
                    _context.Alarms.Add(alarm);
                    await _context.SaveChangesAsync(cancellationToken);

                    await GenerateWorkOrder(alarm, "系统", cancellationToken);
                    alarms.Add(MapToDto(alarm, "系统"));
                }
            }

            return alarms;
        }

        private async Task<AlarmDto> CreateAlarmIfNotExists(int deviceId, string alarmType, int alarmLevel, string message, CancellationToken cancellationToken)
        {
            var existingAlarm = await _context.Alarms
                .Where(a => a.DeviceId == deviceId 
                    && a.AlarmType == alarmType 
                    && a.EndTime == null)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingAlarm != null) return null;

            var device = await _context.Devices.FindAsync(new object[] { deviceId }, cancellationToken);
            
            var alarm = new Alarm
            {
                DeviceId = deviceId,
                AlarmType = alarmType,
                AlarmLevel = alarmLevel,
                AlarmMessage = message,
                StartTime = DateTime.Now,
                Status = 1
            };
            _context.Alarms.Add(alarm);
            await _context.SaveChangesAsync(cancellationToken);

            await GenerateWorkOrder(alarm, device?.DeviceName, cancellationToken);
            _logger.LogInformation($"Alarm created: {alarmType} for device {deviceId}");
            
            return MapToDto(alarm, device?.DeviceName);
        }

        private async Task GenerateWorkOrder(Alarm alarm, string deviceName, CancellationToken cancellationToken)
        {
            var workOrder = new WorkOrder
            {
                AlarmId = alarm.AlarmId,
                Title = $"{deviceName} - {alarm.AlarmType}",
                Description = alarm.AlarmMessage,
                Priority = alarm.AlarmLevel == 2 ? 1 : 2,
                Status = 0,
                CreatedAt = DateTime.Now
            };

            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation($"Work order generated: {workOrder.WorkOrderId} for alarm {alarm.AlarmId}");
        }

        private AlarmDto MapToDto(Alarm alarm, string deviceName)
        {
            return new AlarmDto
            {
                AlarmId = alarm.AlarmId,
                DeviceId = alarm.DeviceId,
                DeviceName = deviceName,
                AlarmType = alarm.AlarmType,
                AlarmLevel = alarm.AlarmLevel,
                AlarmMessage = alarm.AlarmMessage,
                StartTime = alarm.StartTime,
                EndTime = alarm.EndTime,
                Status = alarm.Status,
                AckBy = alarm.AckBy,
                AckAt = alarm.AckAt
            };
        }
    }
}
