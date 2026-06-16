using System.Threading;
using System.Threading.Tasks;

namespace BusFire
{
	public interface IBusInternal
	{
		Task Send(ICommand command, CancellationToken token);

		Task Publish(IEvent @event, CancellationToken token);

	}

}
