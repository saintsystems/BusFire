using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusFire.EventPublishers
{
    /// <summary>
    /// Awaits each event handler in a single foreach loop:
    /// <code>
    /// foreach (var handler in handlers) {
    ///     await handler(notification, cancellationToken);
    /// }
    /// </code>
    /// </summary>
    public class ForEachAwaitPublisher : IEventPublisher
    {
        public async Task Publish(IEnumerable<EventHandlerExecutor> handlerExecutors, IEvent @event, CancellationToken cancellationToken)
        {
            foreach (var handler in handlerExecutors)
            {
                await handler.HandlerCallback(@event, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
