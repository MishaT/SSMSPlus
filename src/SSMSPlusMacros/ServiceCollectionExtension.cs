using Microsoft.Extensions.DependencyInjection;
using SSMSPlusMacros.Services;
using SSMSPlusMacros.UI;

namespace SSMSPlusMacros
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddSSMSPlusMacrosServices(this IServiceCollection services)
        {
            services.AddSingleton<MacrosUI>();
            services.AddSingleton<MacroStore>();
            services.AddSingleton<MacroRunner>();
            services.AddSingleton<MacroKeyBinder>();
            services.AddTransient<MacrosWindowVM>();
            return services;
        }
    }
}
