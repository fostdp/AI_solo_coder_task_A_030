using Microsoft.EntityFrameworkCore;
using Serilog;
using ChillerPlantOptimization.Data;
using ChillerPlantOptimization.Repositories;
using ChillerPlantOptimization.Services;
using ChillerPlantOptimization.Hubs;
using ChillerPlantOptimization.BackgroundServices;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("正在启动冷站群控与能效优化系统...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "冷站群控与能效优化系统 API",
            Version = "v1",
            Description = "智能建筑中央空调冷站群控与能效优化系统后端API"
        });
    });

    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Configuration.GetValue<bool>("SignalR:EnableDetailedErrors");
        options.KeepAliveInterval = TimeSpan.FromSeconds(
            builder.Configuration.GetValue<int>("SignalR:KeepAliveInterval", 15));
        options.HandshakeTimeout = TimeSpan.FromSeconds(
            builder.Configuration.GetValue<int>("SignalR:HandshakeTimeout", 30));
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .WithExposedHeaders("Content-Disposition");
        });
    });

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions =>
            {
                sqlOptions.CommandTimeout(120);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });
    });

    builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
    builder.Services.AddScoped<ITimeSeriesRepository, TimeSeriesRepository>();
    builder.Services.AddScoped<IEfficiencyRepository, EfficiencyRepository>();
    builder.Services.AddScoped<IAlarmRepository, AlarmRepository>();
    builder.Services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
    builder.Services.AddScoped<IOptimizationRepository, OptimizationRepository>();
    builder.Services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();

    builder.Services.AddScoped<IDeviceDataService, DeviceDataService>();
    builder.Services.AddScoped<IEfficiencyService, EfficiencyService>();
    builder.Services.AddScoped<IOptimizationModelService, OptimizationModelService>();
    builder.Services.AddScoped<IAlarmEngineService, AlarmEngineService>();
    builder.Services.AddScoped<INotificationService, NotificationService>();
    builder.Services.AddScoped<IWorkOrderService, WorkOrderService>();
    builder.Services.AddScoped<ISystemConfigService, SystemConfigService>();
    builder.Services.AddSingleton<IBACnetDataCollectionService, BACnetDataCollectionService>();

    builder.Services.AddHttpClient();
    builder.Services.AddMemoryCache();

    builder.Services.AddHostedService<EfficiencyBackgroundService>();
    builder.Services.AddHostedService<BACnetCollectionBackgroundService>();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            await dbContext.Database.EnsureCreatedAsync();
            Log.Information("数据库初始化检查完成");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "数据库初始化失败");
        }
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "冷站优化系统 API v1");
        });
    }

    app.UseCors("AllowAll");

    app.UseSerilogRequestLogging();

    app.UseRouting();

    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<RealtimeHub>("/hubs/realtime");

    app.MapGet("/", () => new
    {
        Name = "智能建筑中央空调冷站群控与能效优化系统",
        Version = "1.0.0",
        Status = "Running",
        Time = DateTime.UtcNow,
        ApiDoc = "/swagger",
        RealtimeHub = "/hubs/realtime"
    });

    Log.Information("系统启动完成，正在监听请求...");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "系统启动失败");
}
finally
{
    Log.CloseAndFlush();
}
