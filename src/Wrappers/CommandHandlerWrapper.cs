using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace BusFire.Wrappers
{
	public abstract class CommandHandlerBase : HandlerBase
	{
		public abstract Task Handle(object command, IServiceProvider serviceProvider, CancellationToken cancellationToken);
	}

	public abstract class CommandHandlerWrapper : CommandHandlerBase
	{
		public abstract Task Handle(ICommand command, IServiceProvider serviceProvider,
            CancellationToken cancellationToken);
	}

	public class CommandHandlerWrapperImpl<TCommand> : CommandHandlerWrapper
		where TCommand : ICommand
	{
		public override async Task Handle(object request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken) =>
			await Handle((ICommand)request, serviceProvider, cancellationToken).ConfigureAwait(false);

		public override Task Handle(ICommand command, IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
		{
			Task Handler() => serviceProvider.CreateScope().ServiceProvider.GetRequiredService<ICommandHandler<TCommand>>()
                .Handle((TCommand)command, cancellationToken);

            //return Handler();
            return serviceProvider
                .GetServices<IPipelineBehavior<TCommand>>()
				.Reverse()
				.Aggregate((CommandHandlerDelegate)Handler, (next, pipeline) => () => pipeline.Handle((TCommand)command, next, cancellationToken))();
		}
	}
}
