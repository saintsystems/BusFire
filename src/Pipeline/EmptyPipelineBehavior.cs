using System.Threading;
using System.Threading.Tasks;

namespace BusFire.Pipeline
{
	public class EmptyPipelineBehavior<TCommand> : IPipelineBehavior<TCommand>
		where TCommand : ICommand
	{
		public async Task Handle(TCommand command, CommandHandlerDelegate next, CancellationToken cancellationToken)
		{
			await next();
		}
	}
}
