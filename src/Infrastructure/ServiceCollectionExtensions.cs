using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace BusFire.Infrastructure
{
    public static class ServiceCollectionExtensions
	{
        /// <summary>
        /// Registers BusFire (handlers, pipeline behaviors, the bus, the message-type registry, and the
        /// failure filter) without configuring Hangfire or any storage — the host owns those. After calling
        /// this, configure Hangfire yourself and apply BusFire's serializer + filter via
        /// <c>config.UseBusFire(provider)</c>:
        /// <code>
        /// services.AddBusFire(cfg => cfg.RegisterServicesFromAssemblies(...));
        /// services.AddHangfire((provider, config) =>
        /// {
        ///     config.UsePostgreSqlStorage(connectionString); // or SQL Server, Redis, in-memory, ...
        ///     config.UseBusFire(provider);
        /// });
        /// </code>
        /// Use this when the host already owns its Hangfire setup. To let BusFire own the
        /// <c>AddHangfire</c> call instead, use the
        /// <see cref="AddBusFire(IServiceCollection, Action{BusFireServiceConfiguration}, Action{IGlobalConfiguration})"/>
        /// overload and just pass your storage.
        /// </summary>
        public static IServiceCollection AddBusFire(this IServiceCollection services, Action<BusFireServiceConfiguration> configuration)
		{
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var serviceConfig = new BusFireServiceConfiguration();

            configuration.Invoke(serviceConfig);

            if (!serviceConfig.AssembliesToRegister.Any())
            {
                throw new ArgumentException("No assemblies found to scan. Supply at least one assembly to scan for handlers.");
            }

            // Build the logical message-type registry from the scanned assemblies so persisted jobs can use
            // stable type names (TypeNameHandling.None) instead of assembly-qualified $type metadata.
            var messageRegistry = new MessageTypeRegistry(
                MessageTypeRegistry.DiscoverMessageTypes(serviceConfig.AssembliesToRegister));
            services.AddSingleton<IMessageTypeRegistry>(messageRegistry);

            services.AddScoped<NotifyOnFailureAttribute>();

            services.AddSingleton<IBusInternal, BusInternal>();
            services.AddSingleton<IBus, Bus>();
            // Recurring (cron) dispatch — depends on the host's Hangfire IRecurringJobManager/JobStorage at resolve time.
            services.AddSingleton<IRecurringJobStore, HangfireRecurringJobStore>();
            services.AddSingleton<IBusFireScheduler, BusFireScheduler>();

            if (serviceConfig.FailureHandler is NullFailureHandler)
            {
                services.AddScoped<IFailureHandler, NullFailureHandler>();
            }

            ServiceRegistrar.AddBusFireClasses(services, serviceConfig);

            ServiceRegistrar.AddRequiredServices(services, serviceConfig);

            return services;
		}

        /// <summary>
        /// Convenience overload: registers BusFire (via the storage-agnostic
        /// <see cref="AddBusFire(IServiceCollection, Action{BusFireServiceConfiguration})"/> overload) and
        /// also owns the <c>AddHangfire</c> call, applying BusFire's serializer + failure filter for you. The
        /// host supplies only the storage via <paramref name="configureStorage"/> — any Hangfire storage works:
        /// <code>
        /// services.AddBusFire(
        ///     cfg => cfg.RegisterServicesFromAssemblies(...),
        ///     hangfire => hangfire.UsePostgreSqlStorage(connectionString));
        /// </code>
        /// Use this when you want BusFire to bootstrap Hangfire. Do not also call <c>AddHangfire</c> yourself —
        /// if the host already owns Hangfire, use the storage-agnostic overload instead.
        /// </summary>
        public static IServiceCollection AddBusFire(this IServiceCollection services, Action<BusFireServiceConfiguration> configuration, Action<IGlobalConfiguration> configureStorage)
		{
            if (configureStorage == null) throw new ArgumentNullException(nameof(configureStorage));

            services.AddBusFire(configuration);

            services.AddHangfire((provider, config) =>
            {
                configureStorage(config);
                config.UseBusFire(provider);
			});

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
