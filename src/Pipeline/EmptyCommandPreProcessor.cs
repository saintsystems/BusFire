using System.Threading;
using System.Threading.Tasks;

namespace BusFire.Pipeline
{
	public class EmptyCommandPreProcessor<TCommand> : ICommandPreProcessor<TCommand>
	{
		public Task Process(TCommand command, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}
}
