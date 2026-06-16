using BusFire.Infrastructure;
using Xunit;

namespace BusFire.Tests;

public class HangfireBridgeTests
{
    [Fact]
    public async Task Send_delegates_to_the_in_process_bus()
    {
        var inner = new RecordingBusInternal();
        var bridge = new HangfireBridge(inner, new RecordingBackgroundJobClient());
        var command = new Ping("x");

        await bridge.Send("job", command, "default", default);

        Assert.Same(command, Assert.Single(inner.Sent));
    }

    [Fact]
    public async Task Publish_fans_out_one_job_per_handler()
    {
        var inner = new RecordingBusInternal { HandlerNames = _ => new[] { "H1", "H2" } };
        var client = new RecordingBackgroundJobClient();
        var bridge = new HangfireBridge(inner, client);

        await bridge.Publish("evt", new Pinged("e"), "q", default);

        Assert.Equal(2, client.Created.Count);
        Assert.All(client.Created, c => Assert.Equal(nameof(HangfireBridge.RunEventHandler), c.Job.Method.Name));
        var handlerArgs = client.Created.Select(c => (string)c.Job.Args[2]!).ToList();
        Assert.Contains("H1", handlerArgs);
        Assert.Contains("H2", handlerArgs);
    }

    [Fact]
    public async Task Publish_with_no_handlers_enqueues_nothing()
    {
        var inner = new RecordingBusInternal { HandlerNames = _ => Array.Empty<string>() };
        var client = new RecordingBackgroundJobClient();
        var bridge = new HangfireBridge(inner, client);

        await bridge.Publish("evt", new Pinged("e"), "q", default);

        Assert.Empty(client.Created);
    }

    [Fact]
    public async Task RunEventHandler_delegates_to_the_single_handler_path()
    {
        var inner = new RecordingBusInternal();
        var bridge = new HangfireBridge(inner, new RecordingBackgroundJobClient());
        var @event = new Pinged("e");

        await bridge.RunEventHandler("evt", @event, "HandlerX", "q", default);

        var (recordedEvent, handler) = Assert.Single(inner.HandlerRuns);
        Assert.Same(@event, recordedEvent);
        Assert.Equal("HandlerX", handler);
    }
}
