using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusFire.Pipeline
{
	/// <summary>
	/// Behavior for executing all <see cref="ICommandPostProcessor{TCommand}"/> instances after handling the command
	/// </summary>
	/// <typeparam name="TCommand">Command type</typeparam>
	public class CommandPostProcessorBehavior<TCommand> : IPipelineBehavior<TCommand>
		where TCommand : ICommand
	{
		private readonly IEnumerable<ICommandPostProcessor<TCommand>> _postProcessors;

		public CommandPostProcessorBehavior(IEnumerable<ICommandPostProcessor<TCommand>> postProcessors)
			=> _postProcessors = postProcessors;

		public async Task Handle(TCommand command, CommandHandlerDelegate next, CancellationToken cancellationToken)
		{
			await next().ConfigureAwait(false);

			foreach (var processor in _postProcessors)
			{
				await processor.Process(command, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
