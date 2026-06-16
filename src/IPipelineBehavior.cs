using System.Threading;
using System.Threading.Tasks;

namespace BusFire
{
	/// <summary>
	/// Represents an async continuation for the next task to execute in the pipeline
	/// </summary>
	/// <typeparam name="TCommand">Command type</typeparam>
	/// <returns>Awaitable task returning a <typeparamref name="TCommand"/></returns>
	public delegate Task CommandHandlerDelegate();

	/// <summary>
	/// Pipeline behavior to surround the inner handler.
	/// Implementations add additional behavior and await the next delegate.
	/// </summary>
	/// <typeparam name="TCommand">Command type</typeparam>
	public interface IPipelineBehavior<in TCommand> where TCommand : ICommand
	{
        /// <summary>
        /// Pipeline handler. Perform any additional behavior and await the <paramref name="next"/> delegate as necessary
        /// </summary>
        /// <param name="command">Incoming request</param>
        /// <param name="next">Awaitable delegate for the next action in the pipeline. Eventually this delegate represents the handler.</param>
        /// <param name="cancellationToken"></param>
        Task Handle(TCommand command, CommandHandlerDelegate next, CancellationToken cancellationToken);
	}

}
