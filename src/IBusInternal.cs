using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusFire
{
	public interface IBusInternal
	{
		Task Send(ICommand command, CancellationToken token);

		Task Publish(IEvent @event, CancellationToken token);

		/// <summary>The concrete type names of every handler registered for the event — one queued job is fanned out per name.</summary>
		IReadOnlyList<string> GetEventHandlerTypeNames(IEvent @event);

		/// <summary>Runs the single event handler identified by <paramref name="handlerTypeName"/>, isolated in its own job.</summary>
		Task PublishToHandler(IEvent @event, string handlerTypeName, CancellationToken token);
	}

}
