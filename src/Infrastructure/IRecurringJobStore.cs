using System.Collections.Generic;
using System.Linq;
using Hangfire;
using Hangfire.Storage;

namespace BusFire.Infrastructure
{
    /// <summary>
    /// Reads the ids of the recurring jobs currently persisted in Hangfire storage. Used by
    /// <see cref="IBusFireScheduler.ConfigureSchedules"/> to prune BusFire-owned schedules that are no longer
    /// declared. A seam so the reconcile logic is unit-testable without a live storage.
    /// </summary>
    public interface IRecurringJobStore
    {
        IReadOnlyCollection<string> GetRecurringJobIds();
    }

    /// <inheritdoc />
    public sealed class HangfireRecurringJobStore : IRecurringJobStore
    {
        private readonly JobStorage _storage;

        public HangfireRecurringJobStore(JobStorage storage) => _storage = storage;

        public IReadOnlyCollection<string> GetRecurringJobIds()
        {
            using (var connection = _storage.GetConnection())
            {
                return connection.GetRecurringJobs().Select(job => job.Id).ToList();
            }
        }
    }
}
