using Microsoft.AspNetCore.Mvc;
using ChillerPlantOptimization.Services;
using ChillerPlantOptimization.Models;

namespace ChillerPlantOptimization.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly ISystemConfigService _configService;
    private readonly IBACnetDataCollectionService _bacnetService;
    private readonly ILogger<SystemController> _logger;

    public SystemController(
        ISystemConfigService configService,
        IBACnetDataCollectionService bacnetService,
        ILogger<SystemController> logger)
    {
        _configService = configService;
        _bacnetService = bacnetService;
        _logger = logger;
    }

    [HttpGet("config")]
    public async Task<ActionResult<IEnumerable<SystemConfig>>> GetAllConfigs()
    {
        var configs = await _configService.GetAllConfigsAsync();
        return Ok(configs);
    }

    [HttpGet("config/{key}")]
    public async Task<ActionResult<SystemConfig>> GetConfig(string key)
    {
        var config = await _configService.GetConfigAsync(key);
        if (config == null)
        {
            return NotFound();
        }
        return Ok(config);
    }

    [HttpPut("config/{key}")]
    public async Task<ActionResult> UpdateConfig(string key, [FromBody] string value)
    {
        var result = await _configService.UpdateConfigAsync(key, value);
        if (result)
        {
            return Ok(new { success = true, message = "配置已更新" });
        }
        return BadRequest(new { success = false, message = "更新失败" });
    }

    [HttpGet("design/cop")]
    public async Task<ActionResult<decimal>> GetSystemDesignCOP()
    {
        var cop = await _configService.GetSystemDesignCOPAsync();
        return Ok(new { DesignCOP = cop });
    }

    [HttpGet("bacnet/status")]
    public ActionResult GetBACnetStatus()
    {
        var status = _bacnetService.IsRunning;
        return Ok(new
        {
            IsRunning = status,
            CollectionIntervalSeconds = 30,
            Description = "BACnet/IP数据采集服务"
        });
    }

    [HttpPost("bacnet/start")]
    public async Task<ActionResult> StartBACnetCollection()
    {
        var cts = new CancellationTokenSource();
        await _bacnetService.StartCollectionAsync(cts.Token);
        return Ok(new { success = true, message = "BACnet数据采集已启动" });
    }

    [HttpGet("health")]
    public ActionResult HealthCheck()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0"
        });
    }

    [HttpGet("dashboard/summary")]
    public async Task<ActionResult> GetDashboardSummary()
    {
        var summary = await _configService.GetDashboardSummaryAsync();
        return Ok(summary);
    }

    [HttpGet("device-count")]
    public async Task<ActionResult> GetDeviceCount()
    {
        var count = await _configService.GetDeviceCountByTypeAsync();
        return Ok(count);
    }
}
