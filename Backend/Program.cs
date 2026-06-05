using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ChillerPlant.Data;
using ChillerPlant.Data.Repositories;
using ChillerPlant.Services;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.Configure<WechatWorkSettings>(builder.Configuration.GetSection("WechatWork"));

builder.Services.AddHttpClient();

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

builder.Services.AddHostedService<SystemEfficiencyService>();

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

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHub<RealtimeHub>("/realtimeHub");
});

app.Run();
