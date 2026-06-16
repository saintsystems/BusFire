using System.Threading;
using System.Threading.Tasks;

namespace BusFire.Pipeline
{
	public class EmptyCommandPostProcessor<TCommand> : ICommandPostProcessor<TCommand>
		where TCommand : ICommand
	{
		public Task Process(TCommand command, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}
}
