using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ChillerPlant.Data;
using ChillerPlant.Models;
using ChillerPlant.Modules.Shared.Commands;
using ChillerPlant.Modules.Shared.Events;
using ChillerPlant.Modules.AlarmManager.Models;
using ChillerPlant.Modules.AlarmManager.Services;

namespace ChillerPlant.Modules.AlarmManager.Handlers
{
    public class CheckAlarmsHandler : IRequestHandler<CheckAlarmsCommand, List<AlarmDto>>
    {
        private readonly AlarmEvaluationService _alarmEvaluationService;
        private readonly WechatAlarmAggregatorService _wechatAggregator;
        private readonly IMediator _mediator;
        private readonly ILogger<CheckAlarmsHandler> _logger;

        public CheckAlarmsHandler(
            AlarmEvaluationService alarmEvaluationService,
            WechatAlarmAggregatorService wechatAggregator,
            IMediator mediator,
            ILogger<CheckAlarmsHandler> logger)
        {
            _alarmEvaluationService = alarmEvaluationService;
            _wechatAggregator = wechatAggregator;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<List<AlarmDto>> Handle(CheckAlarmsCommand request, CancellationToken cancellationToken)
        {
            var alarms = await _alarmEvaluationService.EvaluateAlarms(cancellationToken);

            foreach (var alarm in alarms.Where(a => a != null))
            {
                await _mediator.Publish(new AlarmCreatedEvent
                {
                    AlarmId = alarm.AlarmId,
                    AlarmLevel = alarm.AlarmLevel,
                    AlarmType = alarm.AlarmType,
                    AlarmMessage = alarm.AlarmMessage,
                    DeviceId = alarm.DeviceId,
                    StartTime = alarm.StartTime
                }, cancellationToken);

                _wechatAggregator.EnqueueAlarm(
                    alarm.AlarmId,
                    alarm.AlarmType,
                    alarm.AlarmLevel,
                    alarm.AlarmMessage,
                    alarm.DeviceName);
            }

            return alarms;
        }
    }

    public class AcknowledgeAlarmHandler : IRequestHandler<AcknowledgeAlarmCommand, bool>
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AcknowledgeAlarmHandler> _logger;

        public AcknowledgeAlarmHandler(
            ApplicationDbContext context,
            ILogger<AcknowledgeAlarmHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> Handle(AcknowledgeAlarmCommand request, CancellationToken cancellationToken)
        {
            var alarm = await _context.Alarms.FindAsync(new object[] { request.AlarmId }, cancellationToken);
            if (alarm == null)
            {
                _logger.LogWarning($"Alarm {request.AlarmId} not found for acknowledgment");
                return false;
            }

            alarm.Status = 2;
            alarm.AckBy = request.AckBy;
            alarm.AckAt = DateTime.Now;
            alarm.EndTime = DateTime.Now;

            var workOrder = await _context.WorkOrders
                .FirstOrDefaultAsync(w => w.AlarmId == alarm.AlarmId, cancellationToken);
            if (workOrder != null)
            {
                workOrder.Status = 1;
                workOrder.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation($"Alarm {request.AlarmId} acknowledged by {request.AckBy}");
            return true;
        }
    }

    public class UpdateWorkOrderStatusHandler : IRequestHandler<UpdateWorkOrderStatusCommand, bool>
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UpdateWorkOrderStatusHandler> _logger;

        public UpdateWorkOrderStatusHandler(
            ApplicationDbContext context,
            ILogger<UpdateWorkOrderStatusHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> Handle(UpdateWorkOrderStatusCommand request, CancellationToken cancellationToken)
        {
            var workOrder = await _context.WorkOrders.FindAsync(new object[] { request.WorkOrderId }, cancellationToken);
            if (workOrder == null)
            {
                _logger.LogWarning($"Work order {request.WorkOrderId} not found");
                return false;
            }

            var oldStatus = workOrder.Status;
            workOrder.Status = request.Status;
            workOrder.UpdatedAt = DateTime.Now;

            if (!string.IsNullOrEmpty(request.Remark))
            {
                workOrder.Remark = request.Remark;
            }

            if (!string.IsNullOrEmpty(request.Assignee))
            {
                workOrder.Assignee = request.Assignee;
            }

            if (request.Status == 2 && oldStatus != 2)
            {
                var alarm = await _context.Alarms
                    .FirstOrDefaultAsync(a => a.AlarmId == workOrder.AlarmId, cancellationToken);
                if (alarm != null && alarm.Status != 3)
                {
                    alarm.Status = 3;
                    alarm.EndTime = DateTime.Now;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation($"Work order {request.WorkOrderId} status updated: {oldStatus} -> {request.Status}");
            return true;
        }
    }

    public class PushAlarmToWechatHandler : IRequestHandler<PushAlarmToWechatCommand, bool>
    {
        private readonly ApplicationDbContext _context;
        private readonly WechatAlarmAggregatorService _wechatAggregator;
        private readonly ILogger<PushAlarmToWechatHandler> _logger;

        public PushAlarmToWechatHandler(
            ApplicationDbContext context,
            WechatAlarmAggregatorService wechatAggregator,
            ILogger<PushAlarmToWechatHandler> logger)
        {
            _context = context;
            _wechatAggregator = wechatAggregator;
            _logger = logger;
        }

        public async Task<bool> Handle(PushAlarmToWechatCommand request, CancellationToken cancellationToken)
        {
            var alarm = await _context.Alarms
                .Include(a => a.Device)
                .FirstOrDefaultAsync(a => a.AlarmId == request.AlarmId, cancellationToken);

            if (alarm == null)
            {
                _logger.LogWarning($"Alarm {request.AlarmId} not found for WeChat push");
                return false;
            }

            _wechatAggregator.EnqueueAlarm(
                alarm.AlarmId,
                alarm.AlarmType,
                alarm.AlarmLevel,
                alarm.AlarmMessage,
                alarm.Device?.DeviceName);

            _logger.LogInformation($"Alarm {request.AlarmId} manually queued for WeChat push");
            return true;
        }
    }

    public class GetRealtimeDashboardHandler : IRequestHandler<GetRealtimeDashboardCommand, RealtimeDashboardDto>
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GetRealtimeDashboardHandler> _logger;

        public GetRealtimeDashboardHandler(
            ApplicationDbContext context,
            ILogger<GetRealtimeDashboardHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<RealtimeDashboardDto> Handle(GetRealtimeDashboardCommand request, CancellationToken cancellationToken)
        {
            var fiveMinutesAgo = DateTime.Now.AddMinutes(-5);
            var chillerType = await _context.DeviceTypes
                .FirstOrDefaultAsync(dt => dt.TypeName == "冷水机组", cancellationToken);

            var devices = await _context.Devices
                .Include(d => d.DeviceType)
                .ToListAsync(cancellationToken);

            var latestData = await _context.DeviceData
                .Where(d => d.Timestamp >= fiveMinutesAgo)
                .GroupBy(d => d.DeviceId)
                .Select(g => new
                {
                    DeviceId = g.Key,
                    Latest = g.OrderByDescending(d => d.Timestamp).FirstOrDefault()
                })
                .ToDictionaryAsync(d => d.DeviceId, d => d.Latest, cancellationToken);

            var chillers = devices.Where(d => d.DeviceTypeId == chillerType?.DeviceTypeId).ToList();
            var runningChillers = chillers.Where(d => d.Status == 1).ToList();

            var totalPower = latestData.Values
                .Where(d => d != null)
                .Sum(d => d.Power);

            var totalCooling = chillers
                .Select(c => latestData.ContainsKey(c.DeviceId) ? latestData[c.DeviceId] : null)
                .Where(d => d != null && d.COP.HasValue && d.Power > 0)
                .Sum(d => d.Power * d.COP.Value);

            var activeAlarms = await _context.Alarms
                .Where(a => a.EndTime == null)
                .CountAsync(cancellationToken);

            var criticalAlarms = await _context.Alarms
                .Where(a => a.EndTime == null && a.AlarmLevel == 2)
                .CountAsync(cancellationToken);

            var latestEfficiency = await _context.SystemEfficiencies
                .OrderByDescending(e => e.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);

            var openWorkOrders = await _context.WorkOrders
                .Where(w => w.Status < 2)
                .CountAsync(cancellationToken);

            return new RealtimeDashboardDto
            {
                TotalPower = (decimal)totalPower,
                TotalCooling = (decimal)totalCooling,
                SystemCOP = latestEfficiency?.SystemCOP ?? 0,
                COPRatio = latestEfficiency?.COPRatio ?? 0,
                ActiveAlarmCount = activeAlarms,
                CriticalAlarmCount = criticalAlarms,
                RunningChillerCount = runningChillers.Count,
                TotalChillerCount = chillers.Count,
                OpenWorkOrderCount = openWorkOrders,
                OutdoorTemp = latestEfficiency?.OutdoorTemp ?? 28,
                WetBulbTemp = latestEfficiency?.WetBulbTemp ?? 25,
                LastUpdateTime = DateTime.Now
            };
        }
    }
}
