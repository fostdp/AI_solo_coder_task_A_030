using Microsoft.Extensions.DependencyInjection;
using ChillerPlant.Modules.EfficiencyOptimizer.Configuration;

namespace ChillerPlant.Modules.EfficiencyOptimizer
{
    public static class EfficiencyOptimizerModule
    {
        public static IServiceCollection AddEfficiencyOptimizerModule(this IServiceCollection services)
        {
            services.AddOptions<OptimizationSettings>()
                .BindConfiguration("Optimization")
                .ValidateOnStart();
            
            services.AddSingleton<Services.NeuralNetworkOptimizationService>();
            services.AddHostedService<Services.EfficiencyBackgroundService>();
            
            return services;
        }
    }
}
