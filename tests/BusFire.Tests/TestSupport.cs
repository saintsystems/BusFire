using System.Collections.Concurrent;
using BusFire;
using BusFire.Pipeline;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;

namespace BusFire.Tests;

/// <summary>Thread-safe ordered log of handler/behavior invocations for assertions.</summary>
public sealed class InvocationRecorder
{
    private readonly ConcurrentQueue<string> _entries = new();
    public void Record(string entry) => _entries.Enqueue(entry);
    public IReadOnlyList<string> Entries => _entries.ToList();
}

// ---- Commands ----

public sealed record Ping(string Message) : ICommand;

public sealed record QueuedPing(string Message) : ICommand, IShouldQueue;

public sealed record RoutedPing(string Message) : ICommand, IQueueable
{
    public string? Queue => "routed";
    public TimeSpan? Delay => null;
}

public sealed record DelayedPing(string Message) : ICommand, IQueueable
{
    public string? Queue => "delayed";
    public TimeSpan? Delay => TimeSpan.FromMinutes(5);
}

[MessageName("custom-command-name")]
public sealed record NamedCommand(int Id, string Note) : ICommand;

public sealed class PingHandler : ICommandHandler<Ping>
{
    private readonly InvocationRecorder _recorder;
    public PingHandler(InvocationRecorder recorder) => _recorder = recorder;
    public Task Handle(Ping command, CancellationToken cancellationToken)
    {
        _recorder.Record($"PingHandler:{command.Message}");
        return Task.CompletedTask;
    }
}

// ---- Events ----

public sealed record Pinged(string Message) : IEvent;

public sealed record QueuedPinged(string Message) : IEvent, IShouldQueue;

public sealed class PingedHandlerA : IEventHandler<Pinged>
{
    private readonly InvocationRecorder _recorder;
    public PingedHandlerA(InvocationRecorder recorder) => _recorder = recorder;
    public Task Handle(Pinged @event, CancellationToken cancellationToken)
    {
        _recorder.Record($"A:{@event.Message}");
        return Task.CompletedTask;
    }
}

public sealed class PingedHandlerB : IEventHandler<Pinged>
{
    private readonly InvocationRecorder _recorder;
    public PingedHandlerB(InvocationRecorder recorder) => _recorder = recorder;
    public Task Handle(Pinged @event, CancellationToken cancellationToken)
    {
        _recorder.Record($"B:{@event.Message}");
        return Task.CompletedTask;
    }
}

// ---- Pipeline ----

public sealed class RecordingPreProcessor : ICommandPreProcessor<Ping>
{
    private readonly InvocationRecorder _recorder;
    public RecordingPreProcessor(InvocationRecorder recorder) => _recorder = recorder;
    public Task Process(Ping command, CancellationToken cancellationToken)
    {
        _recorder.Record("pre");
        return Task.CompletedTask;
    }
}

public sealed class RecordingPostProcessor : ICommandPostProcessor<Ping>
{
    private readonly InvocationRecorder _recorder;
    public RecordingPostProcessor(InvocationRecorder recorder) => _recorder = recorder;
    public Task Process(Ping command, CancellationToken cancellationToken)
    {
        _recorder.Record("post");
        return Task.CompletedTask;
    }
}

public sealed class RecordingBehavior : IPipelineBehavior<Ping>
{
    private readonly InvocationRecorder _recorder;
    public RecordingBehavior(InvocationRecorder recorder) => _recorder = recorder;
    public async Task Handle(Ping command, CommandHandlerDelegate next, CancellationToken cancellationToken)
    {
        _recorder.Record("behavior:before");
        await next();
        _recorder.Record("behavior:after");
    }
}

// ---- Fakes ----

/// <summary>Records the jobs that would be created, without touching any storage.</summary>
public sealed class RecordingBackgroundJobClient : IBackgroundJobClient
{
    public List<(Job Job, IState State)> Created { get; } = new();

    public string Create(Job job, IState state)
    {
        Created.Add((job, state));
        return Guid.NewGuid().ToString("N");
    }

    public bool ChangeState(string jobId, IState state, string expectedState) => true;
}

/// <summary>Records calls made to the in-process mediator so producer routing can be asserted in isolation.</summary>
public sealed class RecordingBusInternal : IBusInternal
{
    public List<ICommand> Sent { get; } = new();
    public List<IEvent> Published { get; } = new();
    public List<(IEvent Event, string Handler)> HandlerRuns { get; } = new();
    public Func<IEvent, IReadOnlyList<string>> HandlerNames { get; set; } = _ => Array.Empty<string>();

    public Task Send(ICommand command, CancellationToken token)
    {
        Sent.Add(command);
        return Task.CompletedTask;
    }

    public Task Publish(IEvent @event, CancellationToken token)
    {
        Published.Add(@event);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> GetEventHandlerTypeNames(IEvent @event) => HandlerNames(@event);

    public Task PublishToHandler(IEvent @event, string handlerTypeName, CancellationToken token)
    {
        HandlerRuns.Add((@event, handlerTypeName));
        return Task.CompletedTask;
    }
}
