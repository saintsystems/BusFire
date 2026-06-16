using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusFire.Pipeline
{
	/// <summary>
	/// Behavior for executing all <see cref="ICommandPreProcessor{TCommand}"/> instances before handling a command
	/// </summary>
	/// <typeparam name="TCommand"></typeparam>
	public class CommandPreProcessorBehavior<TCommand> : IPipelineBehavior<TCommand>
		where TCommand : ICommand
	{
		private readonly IEnumerable<ICommandPreProcessor<TCommand>> _preProcessors;

		public CommandPreProcessorBehavior(IEnumerable<ICommandPreProcessor<TCommand>> preProcessors)
			=> _preProcessors = preProcessors;

		public async Task Handle(TCommand request, CommandHandlerDelegate next, CancellationToken cancellationToken)
		{
			foreach (var processor in _preProcessors)
			{
				await processor.Process(request, cancellationToken).ConfigureAwait(false);
			}

			await next().ConfigureAwait(false);
		}
	}
}
