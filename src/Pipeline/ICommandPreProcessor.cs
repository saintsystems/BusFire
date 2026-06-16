using System.Threading;
using System.Threading.Tasks;

namespace BusFire.Pipeline
{
	/// <summary>
	/// Defined a command pre-processor for a handler
	/// </summary>
	/// <typeparam name="TRequest">Request type</typeparam>
	public interface ICommandPreProcessor<in TCommand> where TCommand : notnull
	{
        /// <summary>
        /// Process method executes before calling the Handle method on your handler
        /// </summary>
        /// <param name="command">Incoming command</param>
        /// <param name="cancellationToken"></param>
        /// <returns>An awaitable task</returns>
        Task Process(TCommand command, CancellationToken cancellationToken);
	}
}
