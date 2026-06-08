using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ChillerPlantOptimization.Models;
using ChillerPlantOptimization.Repositories;

namespace ChillerPlantOptimization.Services;

public interface INotificationService
{
    Task PushWeChatAlarmAsync(Alarm alarm);
    Task PushWeChatNotificationAsync(string title, string content, string? userId = null);
    Task StartAggregationLoopAsync(CancellationToken cancellationToken);
}

public class WeChatMessage
{
    public string msgtype { get; set; } = "markdown";
    public MarkdownContent markdown { get; set; } = new MarkdownContent();
}

public class MarkdownContent
{
    public string content { get; set; } = string.Empty;
}

public class PendingAlarm
{
    public Alarm Alarm { get; set; } = null!;
    public DateTime QueuedAt { get; set; }
}

public class NotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ISystemConfigRepository _configRepository;
    private readonly ILogger<NotificationService> _logger;

    private readonly ConcurrentDictionary<string, ConcurrentQueue<PendingAlarm>> _alarmBuffer = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastSentTime = new();
    private readonly TimeSpan _aggregationWindow = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _maxDelay = TimeSpan.FromMinutes(2);
    private readonly object _flushLock = new();

    public NotificationService(
        HttpClient httpClient,
        ISystemConfigRepository configRepository,
        ILogger<NotificationService> logger)
    {
        _httpClient = httpClient;
        _configRepository = configRepository;
        _logger = logger;
    }

    public async Task PushWeChatAlarmAsync(Alarm alarm)
    {
        var bufferKey = GetAlarmBufferKey(alarm);

        var queue = _alarmBuffer.GetOrAdd(bufferKey, _ => new ConcurrentQueue<PendingAlarm>());
        queue.Enqueue(new PendingAlarm { Alarm = alarm, QueuedAt = DateTime.UtcNow });

        _logger.LogDebug("告警已加入聚合队列，Key: {Key}, 队列长度: {Count}", bufferKey, queue.Count);

        var lastSent = _lastSentTime.GetOrAdd(bufferKey, _ => DateTime.UtcNow.AddMinutes(-1));
        if (DateTime.UtcNow - lastSent >= _aggregationWindow)
        {
            await FlushAlarmBufferAsync(bufferKey);
        }
    }

    private string GetAlarmBufferKey(Alarm alarm)
    {
        var devicePart = alarm.DeviceId ?? "SYSTEM";
        var typePart = alarm.AlarmType.ToString();
        var levelPart = alarm.AlarmLevel.ToString();
        var paramPart = alarm.ParameterName ?? "General";

        return $"{levelPart}_{typePart}_{paramPart}_{devicePart}";
    }

    public async Task StartAggregationLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("告警聚合发送循环已启动，聚合窗口: {Window} 分钟", _aggregationWindow.TotalMinutes);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                await CheckAndFlushAllBuffersAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "告警聚合循环发生错误");
            }
        }

        await CheckAndFlushAllBuffersAsync();
        _logger.LogInformation("告警聚合发送循环已停止");
    }

    private async Task CheckAndFlushAllBuffersAsync()
    {
        var keys = _alarmBuffer.Keys.ToList();
        foreach (var key in keys)
        {
            var lastSent = _lastSentTime.GetOrAdd(key, _ => DateTime.UtcNow);
            var queue = _alarmBuffer.GetOrAdd(key, _ => new ConcurrentQueue<PendingAlarm>());

            bool shouldFlush = false;
            if (!queue.IsEmpty)
            {
                var firstAlarm = queue.FirstOrDefault();
                if (firstAlarm != null && DateTime.UtcNow - firstAlarm.QueuedAt >= _maxDelay)
                {
                    shouldFlush = true;
                    _logger.LogDebug("缓冲区 {Key} 已达到最大延迟 {MaxDelay} 秒，强制发送", key, _maxDelay.TotalSeconds);
                }
                else if (DateTime.UtcNow - lastSent >= _aggregationWindow)
                {
                    shouldFlush = true;
                }
            }

            if (shouldFlush)
            {
                await FlushAlarmBufferAsync(key);
            }
        }
    }

    private async Task FlushAlarmBufferAsync(string bufferKey)
    {
        if (!_alarmBuffer.TryGetValue(bufferKey, out var queue) || queue.IsEmpty)
        {
            return;
        }

        if (!Monitor.TryEnter(_flushLock, 100))
        {
            return;
        }

        try
        {
            var alarms = new List<PendingAlarm>();
            while (queue.TryDequeue(out var pending))
            {
                alarms.Add(pending);
            }

            if (!alarms.Any())
            {
                return;
            }

            _logger.LogInformation("聚合发送 {Count} 条告警，Key: {Key}", alarms.Count, bufferKey);

            var content = BuildAggregatedAlarmContent(alarms);
            var title = alarms.Count == 1 ? "冷站系统告警" : $"冷站系统告警 (聚合{alarms.Count}条)";

            await PushWeChatNotificationAsync(title, content);
            _lastSentTime.AddOrUpdate(bufferKey, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送聚合告警失败，Key: {Key}", bufferKey);
        }
        finally
        {
            Monitor.Exit(_flushLock);
        }
    }

    private string BuildAggregatedAlarmContent(List<PendingAlarm> pendingAlarms)
    {
        var firstAlarm = pendingAlarms.First().Alarm;
        var levelEmoji = firstAlarm.AlarmLevel == AlarmLevel.Level2 ? "🔴" : "🟡";
        var levelText = firstAlarm.AlarmLevel == AlarmLevel.Level2 ? "【二级告警】" : "【一级告警】";

        if (pendingAlarms.Count == 1)
        {
            var alarm = pendingAlarms[0].Alarm;
            return $@"{levelEmoji} {levelText} 智能建筑冷站告警

**告警时间**: {alarm.StartTime:yyyy-MM-dd HH:mm:ss}
**告警类型**: {GetAlarmTypeText(alarm.AlarmType)}
**告警设备**: {alarm.Device?.Name ?? "系统整体"}
**告警内容**: {alarm.Message}

**当前值**: {alarm.ParameterValue:F2}
**阈值**: {alarm.ThresholdValue:F2}
**持续时间**: {alarm.DurationMinutes} 分钟

> 请运维工程师尽快处理！";
        }
        else
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{levelEmoji} {levelText} 智能建筑冷站告警 (聚合{pendingAlarms.Count}条)");
            sb.AppendLine();
            sb.AppendLine($"**汇总时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**告警类型**: {GetAlarmTypeText(firstAlarm.AlarmType)}");
            sb.AppendLine($"**告警级别**: {(firstAlarm.AlarmLevel == AlarmLevel.Level2 ? "二级" : "一级")}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            for (int i = 0; i < pendingAlarms.Count; i++)
            {
                var alarm = pendingAlarms[i].Alarm;
                sb.AppendLine($"**{i + 1}. {alarm.Device?.Name ?? "系统整体"}**");
                sb.AppendLine($"- 告警时间: {alarm.StartTime:HH:mm:ss}");
                sb.AppendLine($"- 告警内容: {alarm.Message}");
                sb.AppendLine($"- 当前值: {alarm.ParameterValue:F2}, 阈值: {alarm.ThresholdValue:F2}");
                sb.AppendLine($"- 持续时间: {alarm.DurationMinutes} 分钟");
                sb.AppendLine();
            }

            sb.AppendLine("> 请运维工程师尽快处理上述告警！");
            return sb.ToString();
        }
    }

    public async Task PushWeChatNotificationAsync(string title, string content, string? userId = null)
    {
        try
        {
            var webhookUrl = await _configRepository.GetSettingAsync("WeChatWebhookUrl");
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                _logger.LogWarning("企业微信Webhook未配置，跳过消息推送");
                return;
            }

            var message = new WeChatMessage
            {
                markdown = new MarkdownContent
                {
                    content = $"## {title}\n\n{content}"
                }
            };

            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(webhookUrl, httpContent);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("企业微信消息推送成功");
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("企业微信消息推送失败: {StatusCode} - {Response}", response.StatusCode, responseContent);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("触发企业微信限流，将在下个周期重试");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送企业微信消息时发生异常");
        }
    }

    private string GetAlarmTypeText(AlarmType type)
    {
        return type switch
        {
            AlarmType.ParameterExceedance => "参数超限",
            AlarmType.LowEfficiency => "能效过低",
            AlarmType.SystemFault => "系统故障",
            AlarmType.CommunicationError => "通信故障",
            _ => "未知类型"
        };
    }
}
