using BusFire.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusFire
{
	public class BusInternal : IBusInternal
	{
		private readonly IServiceProvider _serviceProvider;
        private readonly IEventPublisher _publisher;
        private static readonly ConcurrentDictionary<Type, CommandHandlerBase> _commandHandlers = new();
		private static readonly ConcurrentDictionary<Type, EventHandlerWrapper> _eventHandlers = new();

        //private readonly IMediator _mediator;

        //public Bus(IMediator mediator)
        //{
        //	_mediator = mediator;
        //}
        /// <summary>
        /// Initializes a new instance of the <see cref="Bus"/> class.
        /// </summary>
        /// <param name="serviceProvider">The single instance factory.</param>
        public BusInternal(IServiceProvider serviceProvider, IEventPublisher publisher)
        {
            _serviceProvider = serviceProvider;
            _publisher = publisher;
        }

        public Task Send(ICommand command, CancellationToken cancellationToken = default)
		{
			if (command == null)
			{
				throw new ArgumentNullException(nameof(command));
			}

			var commandType = command.GetType();

			//var handler = (CommandHandlerWrapper)_commandHandlers.GetOrAdd(commandType,
			//	static t => (CommandHandlerBase)(Activator.CreateInstance(typeof(CommandHandlerWrapperImpl<>).MakeGenericType(t))
			//	                                 ?? throw new InvalidOperationException($"Could not create wrapper type for {t}")));
			var handler = _commandHandlers.GetOrAdd(commandType,
				t => (CommandHandlerBase)Activator.CreateInstance(typeof(CommandHandlerWrapperImpl<>).MakeGenericType(t)));

			return handler.Handle(command, _serviceProvider, cancellationToken);
		}

		public Task Publish(IEvent @event, CancellationToken cancellationToken = default)
		{
			if (@event == null)
			{
				throw new ArgumentNullException(nameof(@event));
			}

			return PublishEvent(@event, cancellationToken);
		}

        /// <summary>
        /// Override in a derived class to control how the tasks are awaited. By default, the implementation is a foreach and await of each handler
        /// </summary>
        /// <param name="handlerExecutors">Enumerable of tasks representing invoking each notification handler</param>
        /// <param name="event">The event being published</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A task representing invoking all handlers</returns>
        protected virtual async Task PublishCore(IEnumerable<EventHandlerExecutor> handlerExecutors, IEvent @event, CancellationToken cancellationToken)
            => await _publisher.Publish(handlerExecutors, @event, cancellationToken);

		private Task PublishEvent(IEvent @event, CancellationToken cancellationToken = default)
		{
			return GetEventHandlerWrapper(@event.GetType()).Handle(@event, _serviceProvider, PublishCore, cancellationToken);
		}

		public IReadOnlyList<string> GetEventHandlerTypeNames(IEvent @event)
		{
			if (@event == null)
			{
				throw new ArgumentNullException(nameof(@event));
			}

			using (var scope = _serviceProvider.CreateScope())
			{
				return GetEventHandlerWrapper(@event.GetType()).GetHandlerTypeNames(scope.ServiceProvider);
			}
		}

		public async Task PublishToHandler(IEvent @event, string handlerTypeName, CancellationToken token)
		{
			if (@event == null)
			{
				throw new ArgumentNullException(nameof(@event));
			}

			// Fresh scope per handler job, mirroring the command path, so scoped dependencies don't leak across jobs.
			using (var scope = _serviceProvider.CreateScope())
			{
				await GetEventHandlerWrapper(@event.GetType()).HandleOne(@event, scope.ServiceProvider, handlerTypeName, token);
			}
		}

		private static EventHandlerWrapper GetEventHandlerWrapper(Type eventType)
		{
			return _eventHandlers.GetOrAdd(eventType,
				t => (EventHandlerWrapper)Activator.CreateInstance(typeof(EventHandlerWrapperImpl<>).MakeGenericType(t)));
		}

	}
}
