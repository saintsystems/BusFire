using System;
using System.Threading.Tasks;

namespace BusFire.Pipeline
{
	/// <summary>
	/// Defines an exception handler for a command and response
	/// </summary>
	/// <typeparam name="TCommand">Command type</typeparam>
	/// <typeparam name="TException">Exception type</typeparam>
	public interface ICommandExceptionHandler<TCommand, in TException>
        where TCommand : ICommand
        where TException : Exception
    {
        /// <summary>
        /// Called when the command handler throws an exception
        /// </summary>
        /// <param name="command">Command instance</param>
        /// <param name="exception">The thrown exception</param>
        /// <param name="state">The current state of handling the exception</param>
        /// <returns>An awaitable task</returns>
        Task Handle(TCommand command, TException exception, CommandExceptionHandlerState<TCommand> state);
    }

    /// <summary>
    /// Defines the base exception handler for a command and response
    /// </summary>
    /// <typeparam name="TCommand">Request type</typeparam>
    public interface ICommandExceptionHandler<TCommand> : ICommandExceptionHandler<TCommand, Exception>
        where TCommand : ICommand
    {
    }

    /// <summary>
    /// Wrapper class that asynchronously handles a base exception from command
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    public abstract class AsyncCommandExceptionHandler<TCommand> : ICommandExceptionHandler<TCommand>
        where TCommand : ICommand
    {
        async Task ICommandExceptionHandler<TCommand, Exception>.Handle(TCommand command, Exception exception, CommandExceptionHandlerState<TCommand> state)
            => await Handle(command, exception, state).ConfigureAwait(false);

        /// <summary>
        /// Override in a derived class for the handler logic
        /// </summary>
        /// <param name="command">Failed command</param>
        /// <param name="exception">The thrown exception</param>
        /// <param name="state">The current state of handling the exception</param>
        protected abstract Task Handle(TCommand command, Exception exception, CommandExceptionHandlerState<TCommand> state);
    }

    /// <summary>
    /// Wrapper class that synchronously handles an exception from command
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TException">Exception type</typeparam>
    public abstract class CommandExceptionHandler<TCommand, TException> : ICommandExceptionHandler<TCommand, TException>
        where TCommand : ICommand
        where TException : Exception
    {
        Task ICommandExceptionHandler<TCommand, TException>.Handle(TCommand command, TException exception, CommandExceptionHandlerState<TCommand> state)
        {
            Handle(command, exception, state);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Override in a derived class for the handler logic
        /// </summary>
        /// <param name="command">Failed command</param>
        /// <param name="exception">The thrown exception</param>
        /// <param name="state">The current state of handling the exception</param>
        protected abstract void Handle(TCommand command, TException exception, CommandExceptionHandlerState<TCommand> state);
    }

    /// <summary>
    /// Wrapper class that synchronously handles a base exception from command
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    public abstract class CommandExceptionHandler<TCommand> : ICommandExceptionHandler<TCommand>
        where TCommand : ICommand
    {
        Task ICommandExceptionHandler<TCommand, Exception>.Handle(TCommand command, Exception exception, CommandExceptionHandlerState<TCommand> state)
        {
            Handle(command, exception, state);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Override in a derived class for the handler logic
        /// </summary>
        /// <param name="command">Failed command</param>
        /// <param name="exception">The thrown exception</param>
        /// <param name="state">The current state of handling the exception</param>
        protected abstract void Handle(TCommand command, Exception exception, CommandExceptionHandlerState<TCommand> state);
    }
}
