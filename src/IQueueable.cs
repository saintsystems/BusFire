using System;

namespace BusFire
{
    /// <summary>
    /// A message that opts into durable queueing <em>and</em> describes its own routing. Implementing this
    /// implies <see cref="IShouldQueue"/> (the message is queued), and additionally lets the message declare
    /// the Hangfire queue it runs on and an optional delay before it runs.
    /// </summary>
    /// <remarks>
    /// The getters may compute their value, which is the idiomatic .NET equivalent of Laravel's
    /// <c>viaQueue()</c> / <c>withDelay()</c> listener methods — e.g. <c>Queue =&gt; _highPriority ? "high" : null;</c>.
    /// A per-call <c>queue</c> argument, when supplied, takes precedence over <see cref="Queue"/>; an explicit
    /// <c>Defer(..., delay)</c> takes precedence over <see cref="Delay"/>.
    /// </remarks>
    public interface IQueueable : IShouldQueue
    {
        /// <summary>The Hangfire queue to route to, or <see langword="null"/> for the default queue.</summary>
        string? Queue { get; }

        /// <summary>The delay before the message is processed, or <see langword="null"/> for no delay.</summary>
        TimeSpan? Delay { get; }
    }
}
