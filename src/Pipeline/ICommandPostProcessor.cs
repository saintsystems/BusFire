using System.Threading;
using System.Threading.Tasks;

namespace BusFire.Pipeline
{
    /// <summary>
    /// Defines a command post-processor for a command
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    public interface ICommandPostProcessor<in TCommand> where TCommand : ICommand
	{
        /// <summary>
        /// Process method executes after the Handle method on your handler
        /// </summary>
        /// <param name="command">Command instance</param>
        /// <param name="cancellationToken"></param>
        /// <returns>An awaitable task</returns>
        Task Process(TCommand command, CancellationToken cancellationToken);
	}
}
