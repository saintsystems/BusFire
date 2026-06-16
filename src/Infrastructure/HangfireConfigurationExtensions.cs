using System;
using Hangfire;
using Newtonsoft.Json;

namespace BusFire.Infrastructure
{
    public static class HangfireConfigurationExtensions
    {
        /// <summary>
        /// Configures Hangfire's serializer for BusFire: messages are persisted with a stable logical type
        /// name (via <paramref name="registry"/>) and <c>TypeNameHandling.None</c>, so jobs carry no
        /// assembly-qualified type names — closing the <c>TypeNameHandling.All</c> deserialization-gadget
        /// vector and surviving type/assembly renames.
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
