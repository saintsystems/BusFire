using BusFire;
using BusFire.EventPublishers;
using BusFire.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BusFire.Tests;

// Mirrors MediatR's ExceptionTests / RequestExceptionActionProcessorTests for the command pipeline.
public class CommandExceptionPipelineTests
{
    public sealed record Boom(string Message) : ICommand;

    public sealed class BoomHandler : ICommandHandler<Boom>
    {
        public Task Handle(Boom command, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom:" + command.Message);
    }

    public sealed class SwallowingExceptionHandler : ICommandExceptionHandler<Boom, InvalidOperationException>
    {
        private readonly InvocationRecorder _recorder;
        public SwallowingExceptionHandler(InvocationRecorder recorder) => _recorder = recorder;
        public Task Handle(Boom command, InvalidOperationException exception, CommandExceptionHandlerState<Boom> state)
        {
            _recorder.Record("handled:" + exception.Message);
            state.SetHandled(command);
            return Task.CompletedTask;
        }
    }

    public sealed class ObservingExceptionHandler : ICommandExceptionHandler<Boom, InvalidOperationException>
    {
        private readonly InvocationRecorder _recorder;
        public ObservingExceptionHandler(InvocationRecorder recorder) => _recorder = recorder;
        public Task Handle(Boom command, InvalidOperationException exception, CommandExceptionHandlerState<Boom> state)
        {
            _recorder.Record("observed");
            return Task.CompletedTask; // does NOT set handled
        }
    }

    public sealed class RecordingExceptionAction : ICommandExceptionAction<Boom, InvalidOperationException>
    {
        private readonly InvocationRecorder _recorder;
        public RecordingExceptionAction(InvocationRecorder recorder) => _recorder = recorder;
        public Task Execute(Boom command, InvalidOperationException exception)
        {
            _recorder.Record("action");
            return Task.CompletedTask;
        }
    }

    private static (BusInternal bus, InvocationRecorder recorder) Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        var recorder = new InvocationRecorder();
        services.AddSingleton(recorder);
        services.AddTransient<ServiceFactory>(sp => type => sp.GetService(type)!);
        configure(services);
        var provider = services.BuildServiceProvider();
        return (new BusInternal(provider, new ForEachAwaitPublisher()), recorder);
    }

    [Fact]
    public async Task Exception_handler_that_sets_handled_swallows_the_exception()
    {
        var (bus, recorder) = Build(s =>
        {
            s.AddTransient<ICommandHandler<Boom>, BoomHandler>();
            s.AddTransient<ICommandExceptionHandler<Boom, InvalidOperationException>, SwallowingExceptionHandler>();
            s.AddTransient<IPipelineBehavior<Boom>, CommandExceptionProcessorBehavior<Boom>>();
        });

        await bus.Send(new Boom("x"), default); // does not throw

        Assert.Contains("handled:boom:x", recorder.Entries);
    }

    [Fact]
    public async Task Exception_handler_that_does_not_set_handled_rethrows()
    {
        var (bus, recorder) = Build(s =>
        {
            s.AddTransient<ICommandHandler<Boom>, BoomHandler>();
            s.AddTransient<ICommandExceptionHandler<Boom, InvalidOperationException>, ObservingExceptionHandler>();
            s.AddTransient<IPipelineBehavior<Boom>, CommandExceptionProcessorBehavior<Boom>>();
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.Send(new Boom("x"), default));
        Assert.Contains("observed", recorder.Entries);
    }

    [Fact]
    public async Task Exception_action_runs_then_the_exception_still_propagates()
    {
        var (bus, recorder) = Build(s =>
        {
            s.AddTransient<ICommandHandler<Boom>, BoomHandler>();
            s.AddTransient<ICommandExceptionAction<Boom, InvalidOperationException>, RecordingExceptionAction>();
            s.AddTransient<IPipelineBehavior<Boom>, CommandExceptionActionProcessorBehavior<Boom>>();
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.Send(new Boom("x"), default));
        Assert.Contains("action", recorder.Entries);
    }
}
