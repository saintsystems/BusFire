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
	}
}
