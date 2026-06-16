using BusFire;
using BusFire.Infrastructure;
using Hangfire.States;
using Xunit;

namespace BusFire.Tests;

public class BusTests
{
    private static (Bus bus, RecordingBackgroundJobClient client, RecordingBusInternal inner) Create()
    {
        var client = new RecordingBackgroundJobClient();
        var inner = new RecordingBusInternal();
        return (new Bus(client, inner), client, inner);
    }

    [Fact]
    public async Task Send_plain_command_runs_inline_and_does_not_enqueue()
    {
        var (bus, client, inner) = Create();

        await bus.Send(new Ping("x"));

        Assert.Single(inner.Sent);
        Assert.Empty(client.Created);
    }

    [Fact]
    public async Task Send_IShouldQueue_command_enqueues_and_does_not_run_inline()
    {
        var (bus, client, inner) = Create();

        await bus.Send(new QueuedPing("x"));

        Assert.Empty(inner.Sent);
        var (job, state) = Assert.Single(client.Created);
        Assert.Equal(nameof(HangfireBridge.Send), job.Method.Name);
        Assert.IsType<EnqueuedState>(state);
        Assert.Equal("default", job.Args[2]);
    }

    [Fact]
    public async Task Send_IQueueable_routes_to_declared_queue()
    {
        var (bus, client, _) = Create();

        await bus.Send(new RoutedPing("x"));

        var (job, _) = Assert.Single(client.Created);
        Assert.Equal("routed", job.Args[2]);
    }

    [Fact]
    public async Task Send_explicit_queue_argument_overrides_IQueueable()
    {
        var (bus, client, _) = Create();

        await bus.Send(new RoutedPing("x"), queue: "override");

        var (job, _) = Assert.Single(client.Created);
        Assert.Equal("override", job.Args[2]);
    }

    [Fact]
    public async Task Send_IQueueable_with_delay_schedules_instead_of_enqueues()
    {
        var (bus, client, _) = Create();

        await bus.Send(new DelayedPing("x"));

        var (job, state) = Assert.Single(client.Created);
        Assert.IsType<ScheduledState>(state);
        Assert.Equal("delayed", job.Args[2]);
    }

    [Fact]
    public async Task Publish_plain_event_runs_inline()
    {
        var (bus, client, inner) = Create();

        await bus.Publish(new Pinged("e"));

        Assert.Single(inner.Published);
        Assert.Empty(client.Created);
    }

    [Fact]
    public async Task Publish_IShouldQueue_event_enqueues_dispatcher()
    {
        var (bus, client, inner) = Create();

        await bus.Publish(new QueuedPinged("e"));

        Assert.Empty(inner.Published);
        var (job, state) = Assert.Single(client.Created);
        Assert.Equal(nameof(HangfireBridge.Publish), job.Method.Name);
        Assert.IsType<EnqueuedState>(state);
    }

    [Fact]
    public async Task Defer_command_schedules_with_resolved_queue()
    {
        var (bus, client, inner) = Create();

        await bus.Defer(new Ping("x"), TimeSpan.FromMinutes(1));

        Assert.Empty(inner.Sent);
        var (job, state) = Assert.Single(client.Created);
        Assert.Equal(nameof(HangfireBridge.Send), job.Method.Name);
        Assert.IsType<ScheduledState>(state);
        Assert.Equal("default", job.Args[2]);
    }

    [Fact]
    public async Task Defer_event_schedules_dispatcher()
    {
        var (bus, client, _) = Create();

        await bus.Defer(new Pinged("e"), TimeSpan.FromMinutes(1), queue: "later");

        var (job, state) = Assert.Single(client.Created);
        Assert.Equal(nameof(HangfireBridge.Publish), job.Method.Name);
        Assert.IsType<ScheduledState>(state);
        Assert.Equal("later", job.Args[2]);
    }

    [Fact]
    public void Send_null_command_throws()
    {
        var (bus, _, _) = Create();
        Assert.Throws<ArgumentNullException>(() => { _ = bus.Send(null!); });
    }

    [Fact]
    public void Publish_null_event_throws()
    {
        var (bus, _, _) = Create();
        Assert.Throws<ArgumentNullException>(() => { _ = bus.Publish(null!); });
    }

    [Fact]
    public void Defer_null_command_throws()
    {
        var (bus, _, _) = Create();
        Assert.Throws<ArgumentNullException>(() => { _ = bus.Defer((ICommand)null!, TimeSpan.Zero); });
    }
}
