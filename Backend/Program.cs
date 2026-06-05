using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MediatR;
using ChillerPlant.Data;
using ChillerPlant.Data.Repositories;
using ChillerPlant.Services;
using ChillerPlant.Modules.BacnetGateway;
using ChillerPlant.Modules.EfficiencyOptimizer;
using ChillerPlant.Modules.AlarmManager;
using Serilog;
using Serilog.Events;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Diagnostics.HealthChecks;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ChillerPlant")
    .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Conditional(
        evt => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")),
        wt => wt.ApplicationInsights(
            Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING"),
            TelemetryConverter.Traces))
    .CreateLogger();

try
{
    Log.Information("Starting Chiller Plant Optimization System...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll",
            builder =>
            {
                builder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
            });
    });

    builder.Services.AddControllers()
        .AddNewtonsoftJson(options =>
        {
            options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        });

    builder.Services.AddSignalR();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Chiller Plant Optimization API",
            Version = "v1",
            Description = "智能建筑中央空调冷站群控与能效优化系统 API"
        });
    });

    var aiConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
    if (!string.IsNullOrEmpty(aiConnectionString))
    {
        builder.Services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = aiConnectionString;
            options.EnableAdaptiveSampling = true;
            options.IncludePerformanceCountersCollection = true;
            options.EnableHeartbeat = true;
        });

        builder.Services.AddApplicationInsightsTelemetryProcessor<ChillerPlant.Telemetry.CustomTelemetryProcessor>();
    }

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.CommandTimeout(120);
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

    builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
    builder.Services.Configure<WechatWorkSettings>(builder.Configuration.GetSection("Wechat"));

    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
        typeof(Program).Assembly,
        typeof(Modules.Shared.Commands.InsertDeviceDataCommand).Assembly));

    builder.Services.AddHttpClient();
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddScoped<IDeviceRepository>(provider => 
        new DeviceRepository(
            provider.GetRequiredService<ApplicationDbContext>(),
            connectionString));

    builder.Services.AddScoped<IEfficiencyRepository>(provider =>
        new EfficiencyRepository(
            provider.GetRequiredService<ApplicationDbContext>(),
            connectionString,
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppSettings>>().Value.DesignSystemCOP));

    builder.Services.AddScoped<IAlarmRepository>(provider =>
        new AlarmRepository(
            provider.GetRequiredService<ApplicationDbContext>(),
            connectionString,
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppSettings>>(),
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WechatWorkSettings>>(),
            provider.GetRequiredService<System.Net.Http.IHttpClientFactory>()));

    builder.Services.AddScoped<IOptimizationRepository>(provider =>
        new OptimizationRepository(
            provider.GetRequiredService<ApplicationDbContext>(),
            connectionString,
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppSettings>>()));

    builder.Services.AddHealthChecks()
        .AddSqlServer(
            connectionString: connectionString,
            healthQuery: "SELECT 1",
            name: "sqlserver",
            failureStatus: HealthStatus.Unhealthy,
            tags: new[] { "db", "sql" })
        .AddCheck("self", () => HealthCheckResult.Healthy("Application is running"), tags: new[] { "self" })
        .AddDiskStorageHealthCheck(options =>
        {
            options.AddDrive("C:\\", 1024 * 1024 * 1024);
        }, name: "disk", failureStatus: HealthStatus.Degraded, tags: new[] { "system" });

    builder.Services.AddBacnetGatewayModule();
    builder.Services.AddEfficiencyOptimizerModule();
    builder.Services.AddAlarmManagerModule();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Chiller Plant API V1");
    });

    app.UseCors("AllowAll");

    app.UseHttpsRedirection();

    app.UseRouting();

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (ex != null) return LogEventLevel.Error;
            if (httpContext.Response.StatusCode >= 500) return LogEventLevel.Error;
            if (httpContext.Response.StatusCode >= 400) return LogEventLevel.Warning;
            return LogEventLevel.Information;
        };
    });

    app.UseAuthorization();

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();
        endpoints.MapHub<RealtimeHub>("/realtimeHub");
        
        endpoints.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var result = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        description = e.Value.Description,
                        duration = e.Value.Duration.ToString()
                    }),
                    totalDuration = report.TotalDuration.ToString(),
                    timestamp = DateTime.UtcNow
                };
                await context.Response.WriteAsJsonAsync(result);
            }
        });

        endpoints.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = (check) => check.Tags.Contains("self"),
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { status = report.Status.ToString(), timestamp = DateTime.UtcNow });
            }
        });

        endpoints.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = (check) => check.Tags.Contains("db") || check.Tags.Contains("self"),
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { status = report.Status.ToString(), timestamp = DateTime.UtcNow });
            }
        });
    });

    Log.Information("Chiller Plant Optimization System started successfully");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
