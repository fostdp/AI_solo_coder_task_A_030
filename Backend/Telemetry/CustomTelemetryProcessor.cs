using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace ChillerPlant.Telemetry;

public class CustomTelemetryProcessor : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomTelemetryProcessor(ITelemetryProcessor next, IHttpContextAccessor httpContextAccessor)
    {
        _next = next;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Process(ITelemetry item)
    {
        if (item is RequestTelemetry request)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context != null)
            {
                request.Properties["UserAgent"] = context.Request.Headers["User-Agent"].ToString();
                request.Properties["RequestPath"] = context.Request.Path;
            }

            if (request.Name.StartsWith("GET /health"))
            {
                return;
            }

            if (double.TryParse(request.Metrics["Duration"], out var duration) && duration > 5000)
            {
                request.Properties["SlowRequest"] = "true";
            }
        }

        if (item is ExceptionTelemetry exception)
        {
            exception.Properties["MachineName"] = Environment.MachineName;
            exception.Properties["ProcessId"] = Environment.ProcessId.ToString();
        }

        if (item is TraceTelemetry trace)
        {
            if (trace.Message.Contains("BACnet") || trace.Message.Contains("bacnet"))
            {
                trace.Properties["Category"] = "BACnet";
            }
            else if (trace.Message.Contains("Alarm") || trace.Message.Contains("alarm"))
            {
                trace.Properties["Category"] = "Alarm";
            }
            else if (trace.Message.Contains("Optimization") || trace.Message.Contains("optimization"))
            {
                trace.Properties["Category"] = "Optimization";
            }
        }

        item.Context.GlobalProperties["Application"] = "ChillerPlant";
        item.Context.GlobalProperties["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        _next.Process(item);
    }
}
