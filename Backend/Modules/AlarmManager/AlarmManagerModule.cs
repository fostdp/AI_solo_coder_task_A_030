using Microsoft.Extensions.DependencyInjection;
using ChillerPlant.Modules.AlarmManager.Models;

namespace ChillerPlant.Modules.AlarmManager
{
    public static class AlarmManagerModule
    {
        public static IServiceCollection AddAlarmManagerModule(this IServiceCollection services)
        {
            services.AddOptions<WechatPushConfig>()
                .BindConfiguration("Wechat")
                .ValidateOnStart();

            services.AddScoped<Services.AlarmEvaluationService>();
            services.AddSingleton<Services.WechatAlarmAggregatorService>();
            services.AddHostedService<Services.AlarmBackgroundService>();
            
            return services;
        }
    }
}
