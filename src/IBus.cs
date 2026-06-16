#define BusFire

//using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BusFire
{
#if BusFire
	#region :: BusFire ::
	
	public interface IEvent
	{

	}

	public interface IEventHandler<in TEvent> where TEvent : IEvent
	{
        /// <summary>
        /// Handles an event
        /// </summary>
        /// <param name="event">The event</param>
        /// <param name="cancellationToken"></param>
        Task Handle(TEvent @event, CancellationToken cancellationToken);
	}

	// BusFire's public surface is fire-and-forget: commands and events return no value. Request/response
	// (ICommand<TResponse>) is intentionally not offered — it can't be honored on the durable-queue path,
	// where there is no caller to return a value to. See docs/ROADMAP.md (P1: "Decide the surface").
	public interface ICommand
	{

	}

	public interface ICommandHandler<in TCommand> where TCommand : ICommand
	{
		/// <summary>Handles a command</summary>
		/// <param name="command">The command</param>
		Task Handle(TCommand command, CancellationToken cancellationToken);
	}

	#endregion

#else

	#region :: MediatR ::

	public interface IEvent : INotification
	{

	}

	public interface IEventHandler<in TEvent> : INotificationHandler<TEvent> where TEvent : IEvent
	{

	}

	public interface ICommand : IRequest
	{

	}

	public interface ICommand<out TResponse> : IRequest<TResponse>
	{

	}

	public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit> where TCommand : ICommand<Unit>
	{
	}

	public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>
	{

	}

	#endregion

#endif

    public interface IBus : ISender, IPublisher
    {
    }

    public interface ISender
    {
        // queue == null means "not specified": fall back to the message's IQueueable.Queue, else "default".
        Task Send(ICommand command, CancellationToken cancellationToken = default, string? queue = null);

        Task Defer(ICommand command, TimeSpan delay, CancellationToken cancellationToken = default, string? queue = null);
    }

    public interface IPublisher
    {
        Task Publish(IEvent @event, CancellationToken cancellationToken = default, string? queue = null);

        Task Defer(IEvent @event, TimeSpan delay, CancellationToken cancellationToken = default, string? queue = null);
    }

}
