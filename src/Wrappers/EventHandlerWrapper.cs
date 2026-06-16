using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BusFire.Wrappers
{
	public abstract class EventHandlerWrapper
	{
		public abstract Task Handle(IEvent @event, IServiceProvider serviceFactory,
			Func<IEnumerable<EventHandlerExecutor>, IEvent, CancellationToken, Task> publish, CancellationToken cancellationToken);

		/// <summary>The concrete type names of every handler registered for this event type.</summary>
		public abstract IReadOnlyList<string> GetHandlerTypeNames(IServiceProvider serviceFactory);

		/// <summary>Runs only the handler whose concrete type name matches <paramref name="handlerTypeName"/>.</summary>
		public abstract Task HandleOne(IEvent @event, IServiceProvider serviceFactory, string handlerTypeName, CancellationToken cancellationToken);
	}

	public class EventHandlerWrapperImpl<TEvent> : EventHandlerWrapper
		where TEvent : IEvent
	{
		public override Task Handle(IEvent @event, IServiceProvider serviceFactory,
			Func<IEnumerable<EventHandlerExecutor>, IEvent, CancellationToken, Task> publish,
            CancellationToken cancellationToken)
		{
			var handlers = serviceFactory
				.GetServices<IEventHandler<TEvent>>()
				.Select(x => new EventHandlerExecutor(x, (theEvent, theToken) => x.Handle((TEvent)theEvent, theToken)));

			return publish(handlers, @event, cancellationToken);
		}

		public override IReadOnlyList<string> GetHandlerTypeNames(IServiceProvider serviceFactory)
		{
			return serviceFactory
				.GetServices<IEventHandler<TEvent>>()
				.Select(h => h.GetType().FullName)
				.Distinct()
				.ToList();
		}

		public override async Task HandleOne(IEvent @event, IServiceProvider serviceFactory, string handlerTypeName, CancellationToken cancellationToken)
		{
			var handler = serviceFactory
				.GetServices<IEventHandler<TEvent>>()
				.FirstOrDefault(h => h.GetType().FullName == handlerTypeName);

			if (handler == null)
			{
				throw new InvalidOperationException(
					$"No event handler '{handlerTypeName}' is registered for event '{typeof(TEvent).FullName}'. " +
					"The handler may have been removed or renamed after the job was enqueued.");
			}

			await handler.Handle((TEvent)@event, cancellationToken).ConfigureAwait(false);
		}
	}
}
