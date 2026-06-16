using System;
using System.Threading.Tasks;

namespace BusFire.Pipeline
{
	/// <summary>
	/// Defines an exception action for a Command
	/// </summary>
	/// <typeparam name="TCommand">Command type</typeparam>
	/// <typeparam name="TException">Exception type</typeparam>
	public interface ICommandExceptionAction<in TCommand, in TException>
        where TCommand : notnull
        where TException : Exception
    {
        /// <summary>
        /// Called when the Command handler throws an exception
        /// </summary>
        /// <param name="command">Command instance</param>
        /// <param name="exception">The thrown exception</param>
        /// <returns>An awaitable task</returns>
        Task Execute(TCommand command, TException exception);
    }

    /// <summary>
    /// Defines the base exception action for a Command.
    ///     You do not need to register this interface explicitly
    ///     with a container as it inherits from the base
    ///     <see cref="ICommandExceptionAction{TCommand, TException}" /> interface.
    /// </summary>
    /// <typeparam name="TCommand">The type of failed Command</typeparam>
    public interface ICommandExceptionAction<in TCommand> : ICommandExceptionAction<TCommand, Exception>
        where TCommand : notnull
    {
    }

    /// <summary>
    /// Wrapper class that asynchronously performs an action on a Command for base exception
    /// </summary>
    /// <typeparam name="TCommand">The type of failed Command</typeparam>
    public abstract class AsyncCommandExceptionAction<TCommand> : ICommandExceptionAction<TCommand>
        where TCommand : ICommand
    {
        async Task ICommandExceptionAction<TCommand, Exception>.Execute(TCommand command, Exception exception)
            => await Execute(command, exception).ConfigureAwait(false);

        /// <summary>
        /// Override in a derived class for the action logic
        /// </summary>
        /// <param name="command">Failed Command</param>
        /// <param name="exception">Original exception from Command handler</param>
        protected abstract Task Execute(TCommand command, Exception exception);
    }

    /// <summary>
    /// Wrapper class that synchronously performs an action on a Command for specific exception
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TException">Exception type</typeparam>
    public abstract class CommandExceptionAction<TCommand, TException> : ICommandExceptionAction<TCommand, TException>
        where TCommand : notnull
        where TException : Exception
    {
        Task ICommandExceptionAction<TCommand, TException>.Execute(TCommand command, TException exception)
        {
            Execute(command, exception);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Override in a derived class for the action logic
        /// </summary>
        /// <param name="command">Failed Command</param>
        /// <param name="exception">Original exception from Command handler</param>
        protected abstract void Execute(TCommand command, TException exception);
    }

    /// <summary>
    /// Wrapper class that synchronously performs an action on a command for base exception
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    public abstract class CommandExceptionAction<TCommand> : ICommandExceptionAction<TCommand>
        where TCommand : notnull
    {
        Task ICommandExceptionAction<TCommand, Exception>.Execute(TCommand command, Exception exception)
        {
            Execute(command, exception);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Override in a derived class for the action logic
        /// </summary>
        /// <param name="command">Failed Command</param>
        /// <param name="exception">Original exception from Command handler</param>
        protected abstract void Execute(TCommand command, Exception exception);
    }
}
