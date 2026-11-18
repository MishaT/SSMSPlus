using Microsoft.Extensions.DependencyInjection;

using SSMSPlusVisualizers.Services;
using SSMSPlusVisualizers.UI;

namespace SSMSPlusVisualizers
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddSSMSPlusVisualizersServices(this IServiceCollection services)
        {
            services.AddSingleton<VisualizersUI>();
            services.AddSingleton<VisualizerStore>();
            services.AddSingleton<VisualizerRunner>();
            services.AddTransient<VisualizersWindowVM>();
            return services;
        }
    }
}
