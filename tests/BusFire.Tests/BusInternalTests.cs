using BusFire;
using BusFire.EventPublishers;
using BusFire.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BusFire.Tests;

public class BusInternalTests
{
    private static (BusInternal bus, InvocationRecorder recorder) Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        var recorder = new InvocationRecorder();
        services.AddSingleton(recorder);
        configure(services);
        var provider = services.BuildServiceProvider();
        return (new BusInternal(provider, new ForEachAwaitPublisher()), recorder);
    }

    [Fact]
    public async Task Send_invokes_the_command_handler()
    {
        var (bus, recorder) = Build(s => s.AddTransient<ICommandHandler<Ping>, PingHandler>());

        await bus.Send(new Ping("hi"), default);

        Assert.Equal(new[] { "PingHandler:hi" }, recorder.Entries);
    }

    [Fact]
    public async Task Publish_invokes_all_event_handlers()
    {
        var (bus, recorder) = Build(s =>
        {
            s.AddTransient<IEventHandler<Pinged>, PingedHandlerA>();
            s.AddTransient<IEventHandler<Pinged>, PingedHandlerB>();
        });

        await bus.Publish(new Pinged("e"), default);

        Assert.Contains("A:e", recorder.Entries);
        Assert.Contains("B:e", recorder.Entries);
    }

    [Fact]
    public void GetEventHandlerTypeNames_returns_every_handler()
    {
        var (bus, _) = Build(s =>
        {
            s.AddTransient<IEventHandler<Pinged>, PingedHandlerA>();
            s.AddTransient<IEventHandler<Pinged>, PingedHandlerB>();
        });

        var names = bus.GetEventHandlerTypeNames(new Pinged("e"));

        Assert.Contains(typeof(PingedHandlerA).FullName, names);
        Assert.Contains(typeof(PingedHandlerB).FullName, names);
    }

    [Fact]
    public async Task PublishToHandler_runs_only_the_named_handler()
    {
        var (bus, recorder) = Build(s =>
        {
            s.AddTransient<IEventHandler<Pinged>, PingedHandlerA>();
            s.AddTransient<IEventHandler<Pinged>, PingedHandlerB>();
        });

        await bus.PublishToHandler(new Pinged("e"), typeof(PingedHandlerA).FullName!, default);

        Assert.Equal(new[] { "A:e" }, recorder.Entries);
    }

    [Fact]
    public async Task PublishToHandler_unknown_handler_throws()
    {
        var (bus, _) = Build(s => s.AddTransient<IEventHandler<Pinged>, PingedHandlerA>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => bus.PublishToHandler(new Pinged("e"), "Does.Not.Exist", default));
    }

    [Fact]
    public async Task Pipeline_behavior_wraps_the_handler()
    {
        var (bus, recorder) = Build(s =>
        {
            s.AddTransient<ICommandHandler<Ping>, PingHandler>();
            s.AddTransient<IPipelineBehavior<Ping>, RecordingBehavior>();
        });

        await bus.Send(new Ping("x"), default);

        Assert.Equal(new[] { "behavior:before", "PingHandler:x", "behavior:after" }, recorder.Entries);
    }

    [Fact]
    public async Task PreProcessor_behavior_runs_processors_before_the_handler()
    {
        var (bus, recorder) = Build(s =>
        {
            s.AddTransient<ICommandHandler<Ping>, PingHandler>();
            s.AddTransient<ICommandPreProcessor<Ping>, RecordingPreProcessor>();
            s.AddTransient<IPipelineBehavior<Ping>, CommandPreProcessorBehavior<Ping>>();
        });

        await bus.Send(new Ping("x"), default);

        Assert.Equal(new[] { "pre", "PingHandler:x" }, recorder.Entries);
    }

    [Fact]
    public async Task PostProcessor_behavior_runs_processors_after_the_handler()
    {
        var (bus, recorder) = Build(s =>
        {
            s.AddTransient<ICommandHandler<Ping>, PingHandler>();
            s.AddTransient<ICommandPostProcessor<Ping>, RecordingPostProcessor>();
            s.AddTransient<IPipelineBehavior<Ping>, CommandPostProcessorBehavior<Ping>>();
        });

        await bus.Send(new Ping("x"), default);

        Assert.Equal(new[] { "PingHandler:x", "post" }, recorder.Entries);
    }

    [Fact]
    public void Send_null_command_throws()
    {
        var (bus, _) = Build(_ => { });
        Assert.Throws<ArgumentNullException>(() => { _ = bus.Send(null!, default); });
    }

    [Fact]
    public void Publish_null_event_throws()
    {
        var (bus, _) = Build(_ => { });
        Assert.Throws<ArgumentNullException>(() => { _ = bus.Publish(null!, default); });
    }
}
