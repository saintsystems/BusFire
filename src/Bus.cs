using BusFire.Infrastructure;
using Hangfire;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BusFire
{
	public class Bus : IBus
	{
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IBusInternal _busInternal;

        /// <summary>
		/// Initializes a new instance of the <see cref="Bus"/> class.
		/// </summary>
		public Bus(IBackgroundJobClient backgroundJobClient, IBusInternal busInternal)
        {
            _backgroundJobClient = backgroundJobClient;
            _busInternal = busInternal;
        }

		/// <summary>
		/// Dispatches a command. Runs inline via the in-process mediator unless the command
		/// opts into durable queueing by implementing <see cref="IShouldQueue"/>, in which case
		/// it is enqueued as a Hangfire job.
		/// </summary>
		public Task Send(ICommand command, CancellationToken cancellationToken = default, string queue = "default")
		{
			if (command == null) throw new ArgumentNullException(nameof(command));

			if (command is IShouldQueue)
			{
				// The queued path relies on Hangfire's injected job-cancellation token on the consumer side;
				// the caller's token is honored only on the inline path below.
				_backgroundJobClient.Enqueue<HangfireBridge>(bridge => bridge.Send(command.GetType().FullName, command, queue, CancellationToken.None));
				return Task.CompletedTask;
			}

			return _busInternal.Send(command, cancellationToken);
		}

		/// <summary>
		/// Publishes an event. Runs inline via the in-process mediator unless the event
		/// opts into durable queueing by implementing <see cref="IShouldQueue"/>, in which case
		/// it is enqueued as a Hangfire job.
		/// </summary>
		public Task Publish(IEvent @event, CancellationToken cancellationToken = default, string queue = "default")
		{
			if (@event == null) throw new ArgumentNullException(nameof(@event));

			if (@event is IShouldQueue)
			{
				_backgroundJobClient.Enqueue<HangfireBridge>(bridge => bridge.Publish(@event.GetType().FullName, @event, queue, CancellationToken.None));
				return Task.CompletedTask;
			}

			return _busInternal.Publish(@event, cancellationToken);
		}

		/// <summary>
		/// Schedules a command to run after <paramref name="delay"/>. Deferred dispatch is
		/// inherently durable — a delayed message cannot run inline "now" — so it always
		/// enqueues on Hangfire regardless of <see cref="IShouldQueue"/>.
		/// </summary>
		public Task Defer(ICommand command, TimeSpan delay, CancellationToken cancellationToken = default, string queue = "default")
		{
			if (command == null) throw new ArgumentNullException(nameof(command));

			_backgroundJobClient.Schedule<HangfireBridge>(bridge => bridge.Send(command.GetType().FullName, command, queue, CancellationToken.None), delay);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Schedules an event to publish after <paramref name="delay"/>. Deferred dispatch is
		/// inherently durable — a delayed message cannot run inline "now" — so it always
		/// enqueues on Hangfire regardless of <see cref="IShouldQueue"/>.
		/// </summary>
		public Task Defer(IEvent @event, TimeSpan delay, CancellationToken cancellationToken = default, string queue = "default")
		{
			if (@event == null) throw new ArgumentNullException(nameof(@event));

			_backgroundJobClient.Schedule<HangfireBridge>(bridge => bridge.Publish(@event.GetType().FullName, @event, queue, CancellationToken.None), delay);
			return Task.CompletedTask;
		}
	}
}
