using System;
using System.Threading;
using System.Threading.Tasks;

namespace BusFire.Infrastructure
{
    internal class NullFailureHandler : IFailureHandler
    {
        public Task Handle(string jobId, Exception exception, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
