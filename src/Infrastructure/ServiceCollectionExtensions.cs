using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace BusFire.Infrastructure
{
    public static class ServiceCollectionExtensions
	{
        public static IServiceCollection AddBusFire(this IServiceCollection services, BusOptions options, Action<BusFireServiceConfiguration> configuration)
		{
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            services.AddScoped<NotifyOnFailureAttribute>();
            services.AddHangfire((provider, config) =>
            {
                config.UseSqlServerStorage(options.ConnectionStringOrName,
					new SqlServerStorageOptions()
					{
						SchemaName = options.SchemaName,
					});

                config.UseFilter(provider.CreateScope().ServiceProvider.GetRequiredService<NotifyOnFailureAttribute>());
                config.UseBusFire();
			});

            //if (options.EnableQueueProcessor)
            //{
            //	services.AddHangfireServer(o => o.Queues = options.Queues);
            //}

            services.AddSingleton<IBusInternal, BusInternal>();
            services.AddSingleton<IBus, Bus>();

            var serviceConfig = BusFireGlobalConfiguration.Configuration;

            configuration?.Invoke(serviceConfig);

            if (serviceConfig.FailureHandler is NullFailureHandler)
            {
                services.AddScoped<IFailureHandler, NullFailureHandler>();
            }

            if (!serviceConfig.AssembliesToRegister.Any())
            {
                throw new ArgumentException("No assemblies found to scan. Supply at least one assembly to scan for handlers.");
            }
            ServiceRegistrar.AddBusFireClasses(services, serviceConfig);

            ServiceRegistrar.AddRequiredServices(services, serviceConfig);

            return services;

		}

		public static IServiceCollection AddBusFireServer(this IServiceCollection services, BusOptions options)
		{
			services.AddHangfireServer(o => o.Queues = options.Queues);

			return services;
		}

		public static IServiceCollection AddBusFireServer(this IServiceCollection services)
		{
			return services.AddBusFireServer(new BusOptions());
		}
	}
}
