using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusFire
{
    public interface IEventPublisher
    {
        Task Publish(IEnumerable<EventHandlerExecutor> handlerExecutors, IEvent @event,
            CancellationToken cancellationToken);
    }

    public interface IFailureHandler
    {
        Task Handle(string jobId, Exception exception, CancellationToken cancellationToken);
    }
}
