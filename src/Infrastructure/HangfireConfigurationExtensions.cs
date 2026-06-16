using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace BusFire.Infrastructure
{
    public static class HangfireConfigurationExtensions
    {
        public static void UseBusFire(this IGlobalConfiguration configuration)
        {
            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            };
            configuration.UseSerializerSettings(jsonSettings);
        }
    }
}
