using BusFire;
using BusFire.EventPublishers;
using BusFire.Infrastructure;
using BusFire.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BusFire.Tests;

public class BusFireServiceConfigurationTests
{
    [Fact]
    public void Assembly_registration_methods_all_work()
    {
        var config = new BusFireServiceConfiguration();

        var returned = config
            .RegisterServicesFromAssemblyContaining<Ping>()
            .RegisterServicesFromAssemblyContaining(typeof(Pinged))
            .RegisterServicesFromAssembly(typeof(Ping).Assembly)
            .RegisterServicesFromAssemblies(typeof(Ping).Assembly);

        Assert.Same(config, returned); // fluent
    }

    [Fact]
    public void Behavior_registration_methods_populate_BehaviorsToRegister()
    {
        var config = new BusFireServiceConfiguration();

        config.AddBehavior<IPipelineBehavior<Ping>, RecordingBehavior>();
        config.AddBehavior(typeof(IPipelineBehavior<Ping>), typeof(EmptyPipelineBehavior<Ping>), ServiceLifetime.Singleton);
        config.AddOpenBehavior(typeof(EmptyPipelineBehavior<>));

        Assert.Equal(3, config.BehaviorsToRegister.Count);
    }

    [Fact]
    public void Properties_are_settable_and_have_sensible_defaults()
    {
        var config = new BusFireServiceConfiguration();

        Assert.Equal(typeof(Bus), config.BusImplementationType);
        Assert.IsType<ForEachAwaitPublisher>(config.EventPublisher);
        Assert.True(config.TypeEvaluator(typeof(Ping)));
        Assert.Equal(ServiceLifetime.Transient, config.Lifetime);

        config.EventPublisherType = typeof(ForEachAwaitPublisher);
        config.Lifetime = ServiceLifetime.Scoped;
        config.CommandExceptionActionProcessorStrategy = CommandExceptionActionProcessorStrategy.ApplyForUnhandledExceptions;

        Assert.Equal(typeof(ForEachAwaitPublisher), config.EventPublisherType);
        Assert.Equal(ServiceLifetime.Scoped, config.Lifetime);
    }
}
