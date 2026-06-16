using Hangfire;
using Hangfire.SqlServer;
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
        /// This is the storage-agnostic entry point; use it for PostgreSQL or any host that already owns its
        /// Hangfire setup. For a batteries-included SQL Server setup, use the
        /// <see cref="AddBusFire(IServiceCollection, BusOptions, Action{BusFireServiceConfiguration})"/> overload.
        /// </summary>
        public static IServiceCollection AddBusFire(this IServiceCollection services, Action<BusFireServiceConfiguration> configuration)
		{
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var serviceConfig = BusFireGlobalConfiguration.Configuration;

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

            if (serviceConfig.FailureHandler is NullFailureHandler)
            {
                services.AddScoped<IFailureHandler, NullFailureHandler>();
            }

            ServiceRegistrar.AddBusFireClasses(services, serviceConfig);

            ServiceRegistrar.AddRequiredServices(services, serviceConfig);

            return services;
		}

        /// <summary>
        /// Batteries-included convenience overload: registers BusFire (via the storage-agnostic
        /// <see cref="AddBusFire(IServiceCollection, Action{BusFireServiceConfiguration})"/> overload) and
        /// also configures Hangfire with SQL Server storage from <paramref name="options"/>. Use this only
        /// when you want BusFire to own the Hangfire/SQL Server bootstrap; for PostgreSQL or any host that
        /// already configures Hangfire, use the storage-agnostic overload and call <c>config.UseBusFire(provider)</c>
        /// inside your own <c>AddHangfire(...)</c>.
        /// </summary>
        public static IServiceCollection AddBusFire(this IServiceCollection services, BusOptions options, Action<BusFireServiceConfiguration> configuration)
		{
            if (options == null) throw new ArgumentNullException(nameof(options));

            services.AddBusFire(configuration);

            services.AddHangfire((provider, config) =>
            {
                config.UseSqlServerStorage(options.ConnectionStringOrName,
					new SqlServerStorageOptions()
					{
						SchemaName = options.SchemaName,
					});

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
