using Hangfire;
using System.ComponentModel;
using System.Threading.Tasks;
//using MediatR;
using System.Threading;

namespace BusFire.Infrastructure
{
	public class HangfireBridge
    {
        //private readonly IMediator _mediator;

        //   public HangfireBridge(IMediator mediator)
        //   {
        //    _mediator = mediator;
        //   }

        //   [DisplayName("{0}")]
        //   [Queue("{2}")]
        //   public async Task Send(string jobName, ICommand command, string queue)
        //   {
        //    await _mediator.Send(command);
        //   }

        //   [DisplayName("{0}")]
        //   [Queue("{2}")]
        //   public async Task Publish(string eventName, IEvent @event, string queue)
        //   {
        //    await _mediator.Publish(@event);
        //   }

        private readonly IBusInternal _bus;

        public HangfireBridge(IBusInternal bus)
        {
	        _bus = bus;
        }

        [DisplayName("{0}")]
        [Queue("{3}")]
        public async Task Send(string jobName, ICommand command, CancellationToken cancellationToken, string queue)
        {
	        await _bus.Send(command, cancellationToken);
        }

        [DisplayName("{0}")]
        [Queue("{3}")]
        public async Task Publish(string eventName, IEvent @event, CancellationToken cancellationToken, string queue)
        {
	        await _bus.Publish(@event, cancellationToken);
        }
    }
}
