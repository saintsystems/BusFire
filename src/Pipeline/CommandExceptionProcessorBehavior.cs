using BusFire.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace BusFire.Pipeline
{
	/// <summary>
	/// Behavior for executing all <see cref="ICommandExceptionHandler{TCommand,TException}"/>
	///     or <see cref="CommandExceptionHandler{TCommand}"/> instances
	///     after an exception is thrown by the following pipeline steps
	/// </summary>
	/// <typeparam name="TCommand">Command type</typeparam>
	public class CommandExceptionProcessorBehavior<TCommand> : IPipelineBehavior<TCommand>
        where TCommand : ICommand
    {
        private readonly IServiceProvider _serviceProvider;

        public CommandExceptionProcessorBehavior(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        public async Task Handle(TCommand command, CommandHandlerDelegate next, CancellationToken cancellationToken)
        {
            try
            {
                await next().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                var state = new CommandExceptionHandlerState<TCommand>();

                var exceptionTypes = GetExceptionTypes(exception.GetType());

                var handlersForException = exceptionTypes
                    .SelectMany(exceptionType => GetHandlersForException(exceptionType, command))
                    .GroupBy(static handlerForException => handlerForException.Handler.GetType())
                    .Select(static handlerForException => handlerForException.First())
                    .Select(static handlerForException => (MethodInfo: GetMethodInfoForHandler(handlerForException.ExceptionType), handlerForException.Handler))
                    .ToList();

                foreach (var handlerForException in handlersForException)
                {
                    try
                    {
                        // ICommandExceptionHandler<TCommand, TException>.Handle takes (command, exception, state) —
                        // no CancellationToken. The arg array must match that arity exactly.
                        await ((Task)(handlerForException.MethodInfo.Invoke(handlerForException.Handler, new object[] { command, exception, state })
                                      ?? throw new InvalidOperationException("Did not return a Task from the exception handler."))).ConfigureAwait(false);
                    }
                    catch (TargetInvocationException invocationException) when (invocationException.InnerException != null)
                    {
                        // Unwrap invocation exception to throw the actual error
                        ExceptionDispatchInfo.Capture(invocationException.InnerException).Throw();
                    }

                    if (state.Handled)
                    {
                        break;
                    }
                }

                if (!state.Handled)
                {
                    throw;
                }

                if (state.Command is null)
                {
                    throw;
                }

                return;// state.Command; //cannot be null if Handled
            }
        }

        private static IEnumerable<Type> GetExceptionTypes(Type? exceptionType)
        {
            while (exceptionType != null && exceptionType != typeof(object))
            {
                yield return exceptionType;
                exceptionType = exceptionType.BaseType;
            }
        }

        private IEnumerable<(Type ExceptionType, object Handler)> GetHandlersForException(Type exceptionType, TCommand command)
        {
            var exceptionHandlerInterfaceType = typeof(ICommandExceptionHandler<,>).MakeGenericType(typeof(TCommand), exceptionType);
            var enumerableExceptionHandlerInterfaceType = typeof(IEnumerable<>).MakeGenericType(exceptionHandlerInterfaceType);

            var exceptionHandlers = (IEnumerable<object>)_serviceProvider.GetRequiredService(enumerableExceptionHandlerInterfaceType);

            return HandlersOrderer.Prioritize(exceptionHandlers.ToList(), command)
                .Select(handler => (exceptionType, action: handler));
        }

        private static MethodInfo GetMethodInfoForHandler(Type exceptionType)
        {
            var exceptionHandlerInterfaceType = typeof(ICommandExceptionHandler<,>).MakeGenericType(typeof(TCommand), exceptionType);

            var handleMethodInfo = exceptionHandlerInterfaceType.GetMethod(nameof(ICommandExceptionHandler<TCommand, Exception>.Handle))
                                   ?? throw new InvalidOperationException($"Could not find method {nameof(ICommandExceptionHandler<TCommand, Exception>.Handle)} on type {exceptionHandlerInterfaceType}");

            return handleMethodInfo;
        }
    }
}
