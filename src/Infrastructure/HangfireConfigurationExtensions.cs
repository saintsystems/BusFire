using System;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace BusFire.Infrastructure
{
    public static class HangfireConfigurationExtensions
    {
        /// <summary>
        /// Applies BusFire's serializer settings and failure filter to a Hangfire configuration the host
        /// owns. Call this inside your own <c>AddHangfire(...)</c> after choosing storage, e.g.:
        /// <code>
        /// services.AddBusFire(cfg => cfg.RegisterServicesFromAssemblies(...));
        /// services.AddHangfire((provider, config) =>
        /// {
        ///     config.UsePostgreSqlStorage(connectionString); // or any storage
        ///     config.UseBusFire(provider);
        /// });
        /// </code>
        /// </summary>
        public static void UseBusFire(this IGlobalConfiguration configuration, IServiceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            configuration.UseBusFire(provider.GetRequiredService<IMessageTypeRegistry>());
            // NotifyOnFailureAttribute is scoped (it depends on the scoped IFailureHandler); resolve it
            // through a scope, then hold it as a process-wide Hangfire filter.
            configuration.UseFilter(provider.CreateScope().ServiceProvider.GetRequiredService<NotifyOnFailureAttribute>());
        }

        /// <summary>
        /// Configures Hangfire's serializer for BusFire: messages are persisted with a stable logical type
        /// name (via <paramref name="registry"/>) and <c>TypeNameHandling.None</c>, so jobs carry no
        /// assembly-qualified type names — closing the <c>TypeNameHandling.All</c> deserialization-gadget
        /// vector and surviving type/assembly renames. (Lower-level; most hosts use the
        /// <see cref="UseBusFire(IGlobalConfiguration, IServiceProvider)"/> overload instead.)
        /// </summary>
        public static void UseBusFire(this IGlobalConfiguration configuration, IMessageTypeRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
            };
            jsonSettings.Converters.Add(new MessageJsonConverter(registry));

            configuration.UseSerializerSettings(jsonSettings);
        }
    }
}
