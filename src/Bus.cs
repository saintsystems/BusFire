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
		/// Dispatches a command. Runs inline via the in-process mediator unless the command opts into durable
		/// queueing by implementing <see cref="IShouldQueue"/> (or <see cref="IQueueable"/>), in which case it
		/// is enqueued as a Hangfire job — scheduled with a delay if the command is <see cref="IQueueable"/>
		/// with a <see cref="IQueueable.Delay"/>.
		/// </summary>
		public Task Send(ICommand command, CancellationToken cancellationToken = default, string? queue = null)
		{
			if (command == null) throw new ArgumentNullException(nameof(command));

			if (command is IShouldQueue)
			{
				// The queued path relies on Hangfire's injected job-cancellation token on the consumer side;
				// the caller's token is honored only on the inline path below.
				EnqueueCommand(command, ResolveQueue(command, queue), MessageDelay(command));
				return Task.CompletedTask;
			}

			return _busInternal.Send(command, cancellationToken);
		}

		/// <summary>
		/// Publishes an event. Runs inline via the in-process mediator unless the event opts into durable
		/// queueing by implementing <see cref="IShouldQueue"/> (or <see cref="IQueueable"/>), in which case it
		/// is enqueued — scheduled with a delay if the event is <see cref="IQueueable"/> with a delay.
		/// </summary>
		public Task Publish(IEvent @event, CancellationToken cancellationToken = default, string? queue = null)
		{
			if (@event == null) throw new ArgumentNullException(nameof(@event));

			if (@event is IShouldQueue)
			{
				EnqueueEvent(@event, ResolveQueue(@event, queue), MessageDelay(@event));
				return Task.CompletedTask;
			}

			return _busInternal.Publish(@event, cancellationToken);
		}

		/// <summary>
		/// Schedules a command to run after <paramref name="delay"/>. The explicit delay always wins; deferred
		/// dispatch is inherently durable, so it always enqueues regardless of <see cref="IShouldQueue"/>.
		/// </summary>
		public Task Defer(ICommand command, TimeSpan delay, CancellationToken cancellationToken = default, string? queue = null)
		{
			if (command == null) throw new ArgumentNullException(nameof(command));

			EnqueueCommand(command, ResolveQueue(command, queue), delay);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Schedules an event to publish after <paramref name="delay"/>. The explicit delay always wins;
		/// deferred dispatch is inherently durable, so it always enqueues regardless of <see cref="IShouldQueue"/>.
		/// </summary>
		public Task Defer(IEvent @event, TimeSpan delay, CancellationToken cancellationToken = default, string? queue = null)
		{
			if (@event == null) throw new ArgumentNullException(nameof(@event));

			EnqueueEvent(@event, ResolveQueue(@event, queue), delay);
			return Task.CompletedTask;
		}

		private void EnqueueCommand(ICommand command, string queue, TimeSpan? delay)
		{
			var jobName = command.GetType().FullName;
			if (delay.HasValue)
			{
				_backgroundJobClient.Schedule<HangfireBridge>(bridge => bridge.Send(jobName, command, queue, CancellationToken.None), delay.Value);
			}
			else
			{
				_backgroundJobClient.Enqueue<HangfireBridge>(bridge => bridge.Send(jobName, command, queue, CancellationToken.None));
			}
		}

		private void EnqueueEvent(IEvent @event, string queue, TimeSpan? delay)
		{
			var eventName = @event.GetType().FullName;
			if (delay.HasValue)
			{
				_backgroundJobClient.Schedule<HangfireBridge>(bridge => bridge.Publish(eventName, @event, queue, CancellationToken.None), delay.Value);
			}
			else
			{
				_backgroundJobClient.Enqueue<HangfireBridge>(bridge => bridge.Publish(eventName, @event, queue, CancellationToken.None));
			}
		}

		// Precedence: explicit per-call queue, then the message's self-declared IQueueable.Queue, then "default".
		private static string ResolveQueue(object message, string? queue)
			=> queue ?? (message as IQueueable)?.Queue ?? "default";

		private static TimeSpan? MessageDelay(object message)
			=> (message as IQueueable)?.Delay;
	}
}
