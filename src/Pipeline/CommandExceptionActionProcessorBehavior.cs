using BusFire.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace BusFire.Pipeline
{
	/// <summary>
	/// Behavior for executing all <see cref="ICommandExceptionHandler{TCommand,TException}"/>
	///     or <see cref="CommandExceptionHandler{TCommand}"/> instances
	///     after an exception is thrown by the following pipeline steps
	/// </summary>
	/// <typeparam name="TCommand">Command type</typeparam>
	public class CommandExceptionActionProcessorBehavior<TCommand> : IPipelineBehavior<TCommand>
        where TCommand : ICommand
    {
        private readonly ServiceFactory _serviceFactory;

        public CommandExceptionActionProcessorBehavior(ServiceFactory serviceFactory) => _serviceFactory = serviceFactory;

        public async Task Handle(TCommand command, CommandHandlerDelegate next, CancellationToken cancellationToken)
        {
            try
            {
                await next().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                for (Type exceptionType = exception.GetType(); exceptionType != typeof(object); exceptionType = exceptionType.BaseType)
                {
                    var actionsForException = GetActionsForException(exceptionType, command, out MethodInfo actionMethod);

                    foreach (var actionForException in actionsForException)
                    {
                        try
                        {
                            await ((Task)(actionMethod.Invoke(actionForException, new object[] { command, exception })
                                          ?? throw new InvalidOperationException($"Could not create task for action method {actionMethod}."))).ConfigureAwait(false);
                        }
                        catch (TargetInvocationException invocationException) when (invocationException.InnerException != null)
                        {
                            // Unwrap invocation exception to throw the actual error
                            ExceptionDispatchInfo.Capture(invocationException.InnerException).Throw();
                        }
                    }
                }

                throw;
            }
        }

        private IList<object> GetActionsForException(Type exceptionType, TCommand command, out MethodInfo actionMethodInfo)
        {
            var exceptionActionInterfaceType = typeof(ICommandExceptionAction<,>).MakeGenericType(typeof(TCommand), exceptionType);
            var enumerableExceptionActionInterfaceType = typeof(IEnumerable<>).MakeGenericType(exceptionActionInterfaceType);
            actionMethodInfo = exceptionActionInterfaceType.GetMethod(nameof(ICommandExceptionAction<TCommand, Exception>.Execute))
                               ?? throw new InvalidOperationException($"Could not find method {nameof(ICommandExceptionAction<TCommand, Exception>.Execute)} on type {exceptionActionInterfaceType}");

            var actionsForException = (IEnumerable<object>)_serviceFactory(enumerableExceptionActionInterfaceType);

            return HandlersOrderer.Prioritize(actionsForException.ToList(), command);
        }
    }
}
