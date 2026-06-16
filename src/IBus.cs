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

	public interface ICommand
	{

	}

	public interface ICommand<out TResponse>
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
        Task Send(ICommand command, CancellationToken cancellationToken = default, string queue = "default");

        //Task Send(ICommand command, TimeSpan delay, string queue = "default");

        Task Defer(ICommand command, TimeSpan delay, CancellationToken cancellationToken = default, string queue = "default");
    }

    public interface IPublisher
    {
        Task Publish(IEvent @event, CancellationToken cancellationToken = default, string queue = "default");

        //Task Publish(IEvent @event, TimeSpan delay, string queue = "default");

        Task Defer(IEvent @event, TimeSpan delay, CancellationToken cancellationToken = default, string queue = "default");
    }

}
