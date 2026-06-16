using BusFire;
using BusFire.Infrastructure;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BusFire.Tests;

public class AddBusFireTests
{
    private static ServiceProvider BuildHost()
    {
        var services = new ServiceCollection();
        services.AddSingleton<InvocationRecorder>();
        // The storage-agnostic overload doesn't configure Hangfire, so the host provides the job client.
        services.AddSingleton<IBackgroundJobClient>(new RecordingBackgroundJobClient());
        services.AddBusFire(cfg => cfg.RegisterServicesFromAssemblyContaining<Ping>());
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Registers_the_bus_and_its_aliases()
    {
        using var sp = BuildHost();

        Assert.NotNull(sp.GetService<IBus>());
        Assert.Same(sp.GetService<IBus>(), sp.GetService<ISender>());
        Assert.Same(sp.GetService<IBus>(), sp.GetService<IPublisher>());
        Assert.NotNull(sp.GetService<IBusInternal>());
    }

    [Fact]
    public void Registers_the_message_type_registry_from_scanned_assemblies()
    {
        using var sp = BuildHost();

        var registry = sp.GetRequiredService<IMessageTypeRegistry>();
        Assert.Equal(typeof(NamedCommand), registry.Resolve("custom-command-name"));
    }

    [Fact]
    public void Scans_and_registers_command_and_event_handlers()
    {
        using var sp = BuildHost();

        Assert.NotNull(sp.GetService<ICommandHandler<Ping>>());
        Assert.Equal(2, sp.GetServices<IEventHandler<Pinged>>().Count());
    }

    [Fact]
    public void Registers_the_service_factory_for_exception_action_resolution()
    {
        using var sp = BuildHost();

        var factory = sp.GetService<ServiceFactory>();
        Assert.NotNull(factory);
        Assert.NotNull(factory!.GetInstance<IMessageTypeRegistry>());
    }

    [Fact]
    public void Throws_when_no_assemblies_are_supplied()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddBusFire(_ => { }));
    }

    [Fact]
    public void Storage_delegate_overload_wires_both_busfire_and_hangfire()
    {
        var services = new ServiceCollection();
        services.AddSingleton<InvocationRecorder>();

        services.AddBusFire(
            cfg => cfg.RegisterServicesFromAssemblyContaining<Ping>(),
            hangfire => { /* host supplies storage here */ });

        // BusFire core registered, and AddHangfire was invoked (registers the job client descriptor).
        Assert.Contains(services, d => d.ServiceType == typeof(IMessageTypeRegistry));
        Assert.Contains(services, d => d.ServiceType == typeof(IBackgroundJobClient));
    }
}
