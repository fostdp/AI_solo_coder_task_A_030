using Microsoft.Extensions.DependencyInjection;
using ChillerPlant.Configuration;

namespace ChillerPlant.Modules.BacnetGateway
{
    public static class BacnetGatewayModule
    {
        public static IServiceCollection AddBacnetGatewayModule(this IServiceCollection services)
        {
            services.AddOptions<BacnetSettings>()
                .BindConfiguration("BACnet")
                .ValidateOnStart();
            
            services.AddSingleton<Services.BacnetProtocolParser>();
            services.AddHostedService<Services.BacnetUdpListenerService>();
            
            return services;
        }
    }
}
