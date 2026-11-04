using Microsoft.Extensions.DependencyInjection;

using SSMSPlusWarnings.Checks;
using SSMSPlusWarnings.Guards;
using SSMSPlusWarnings.Services;
using SSMSPlusWarnings.UI;

namespace SSMSPlusWarnings
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddSSMSPlusWarningsServices(this IServiceCollection services)
        {
            services.AddSingleton<WarningsPlugin>();
            services.AddSingleton<QueryWindowMonitor>();
            services.AddSingleton<WarningCenter>();
            services.AddSingleton<ExecutionGuard>();
            services.AddSingleton<SettingsUI>();
            services.AddTransient<ActionGuardVM>();
            services.AddTransient<LongExecutionVM>();

            // the checks run after each execution of a query window, add new warnings here
            services.AddSingleton<IQueryWindowCheck, OpenTransactionCheck>();
            services.AddSingleton<IQueryWindowCheck, LongExecutionCheck>();
            return services;
        }
    }
}
