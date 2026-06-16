using Hangfire;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace BusFire.Infrastructure
{
	public class HangfireBridge
    {
        private readonly IBusInternal _bus;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public HangfireBridge(IBusInternal bus, IBackgroundJobClient backgroundJobClient)
        {
	        _bus = bus;
	        _backgroundJobClient = backgroundJobClient;
        }

        // The trailing CancellationToken is substituted by Hangfire with a live job-cancellation token at
        // run time; the value passed at enqueue time is a placeholder, so no caller token is persisted.

        [DisplayName("{0}")]
        [Queue("{2}")]
        public async Task Send(string jobName, ICommand command, string queue, CancellationToken cancellationToken)
        {
	        await _bus.Send(command, cancellationToken);
        }

        /// <summary>
        /// Fan-out dispatcher: enqueues one job per event handler so a failure in one handler retries only
        /// that handler instead of re-running every handler for the event.
        /// </summary>
        [DisplayName("{0}")]
        [Queue("{2}")]
        public Task Publish(string eventName, IEvent @event, string queue, CancellationToken cancellationToken)
        {
	        foreach (var handlerTypeName in _bus.GetEventHandlerTypeNames(@event))
	        {
		        _backgroundJobClient.Enqueue<HangfireBridge>(
			        bridge => bridge.RunEventHandler(eventName, @event, handlerTypeName, queue, CancellationToken.None));
	        }

	        return Task.CompletedTask;
        }

        [DisplayName("{0} -> {2}")]
        [Queue("{3}")]
        public async Task RunEventHandler(string eventName, IEvent @event, string handlerTypeName, string queue, CancellationToken cancellationToken)
        {
	        await _bus.PublishToHandler(@event, handlerTypeName, cancellationToken);
        }
    }
}
