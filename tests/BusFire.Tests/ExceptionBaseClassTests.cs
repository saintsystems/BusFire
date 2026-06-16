using BusFire;
using BusFire.Pipeline;
using Xunit;

namespace BusFire.Tests;

// Covers the convenience base classes carried over from MediatR for exception handlers/actions.
public class ExceptionBaseClassTests
{
    public sealed record Cmd : ICommand;

    private sealed class SyncBaseHandler : CommandExceptionHandler<Cmd>
    {
        public bool Called;
        protected override void Handle(Cmd command, Exception exception, CommandExceptionHandlerState<Cmd> state) => Called = true;
    }

    private sealed class SyncTypedHandler : CommandExceptionHandler<Cmd, InvalidOperationException>
    {
        public bool Called;
        protected override void Handle(Cmd command, InvalidOperationException exception, CommandExceptionHandlerState<Cmd> state) => Called = true;
    }

    private sealed class AsyncBaseHandler : AsyncCommandExceptionHandler<Cmd>
    {
        public bool Called;
        protected override Task Handle(Cmd command, Exception exception, CommandExceptionHandlerState<Cmd> state)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }

    private sealed class SyncBaseAction : CommandExceptionAction<Cmd>
    {
        public bool Called;
        protected override void Execute(Cmd command, Exception exception) => Called = true;
    }

    private sealed class SyncTypedAction : CommandExceptionAction<Cmd, InvalidOperationException>
    {
        public bool Called;
        protected override void Execute(Cmd command, InvalidOperationException exception) => Called = true;
    }

    private sealed class AsyncBaseAction : AsyncCommandExceptionAction<Cmd>
    {
        public bool Called;
        protected override Task Execute(Cmd command, Exception exception)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Exception_handler_base_classes_invoke_the_overridden_method()
    {
        var state = new CommandExceptionHandlerState<Cmd>();
        var exception = new InvalidOperationException();
        var command = new Cmd();

        var sync = new SyncBaseHandler();
        await ((ICommandExceptionHandler<Cmd, Exception>)sync).Handle(command, exception, state);
        Assert.True(sync.Called);

        var typed = new SyncTypedHandler();
        await ((ICommandExceptionHandler<Cmd, InvalidOperationException>)typed).Handle(command, exception, state);
        Assert.True(typed.Called);

        var async = new AsyncBaseHandler();
        await ((ICommandExceptionHandler<Cmd, Exception>)async).Handle(command, exception, state);
        Assert.True(async.Called);
    }

    [Fact]
    public async Task Exception_action_base_classes_invoke_the_overridden_method()
    {
        var exception = new InvalidOperationException();
        var command = new Cmd();

        var sync = new SyncBaseAction();
        await ((ICommandExceptionAction<Cmd, Exception>)sync).Execute(command, exception);
        Assert.True(sync.Called);

        var typed = new SyncTypedAction();
        await ((ICommandExceptionAction<Cmd, InvalidOperationException>)typed).Execute(command, exception);
        Assert.True(typed.Called);

        var async = new AsyncBaseAction();
        await ((ICommandExceptionAction<Cmd, Exception>)async).Execute(command, exception);
        Assert.True(async.Called);
    }
}
