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

public class NotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ISystemConfigRepository _configRepository;
    private readonly ILogger<NotificationService> _logger;

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
        var levelEmoji = alarm.AlarmLevel == AlarmLevel.Level2 ? "🔴" : "🟡";
        var levelText = alarm.AlarmLevel == AlarmLevel.Level2 ? "【二级告警】" : "【一级告警】";

        var content = $@"{levelEmoji} {levelText} 智能建筑冷站告警

**告警时间**: {alarm.StartTime:yyyy-MM-dd HH:mm:ss}
**告警类型**: {GetAlarmTypeText(alarm.AlarmType)}
**告警设备**: {alarm.Device?.Name ?? "系统整体"}
**告警内容**: {alarm.Message}

**当前值**: {alarm.ParameterValue:F2}
**阈值**: {alarm.ThresholdValue:F2}
**持续时间**: {alarm.DurationMinutes} 分钟

> 请运维工程师尽快处理！";

        await PushWeChatNotificationAsync("冷站系统告警", content);
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
