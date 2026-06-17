using System;

namespace BusFire
{
    /// <summary>
    /// Schedules BusFire messages to dispatch on a recurring (cron) schedule. This is the *fourth trigger*
    /// into the dispatch pipeline (alongside <c>Send</c>, <c>Defer</c>, and <see cref="IQueueable"/>): the
    /// recurring trigger is Hangfire's recurring-job scheduler, and when it fires the message flows through
    /// the same bridge → handlers → fan-out → failure pipeline as any other dispatch.
    /// </summary>
    /// <remarks>
    /// Define schedules in code at startup; registration is idempotent (a stable <c>id</c> upserts).
    /// The fluent surface is modeled on Coravel/Laravel, but the durable engine is Hangfire.
    /// </remarks>
    public interface IBusFireScheduler
    {
        /// <summary>Schedule a command to dispatch recurringly under the stable recurring-job <paramref name="id"/>.</summary>
        IRecurringFrequency Schedule(string id, ICommand command, string? queue = null);

        /// <summary>Schedule an event to publish recurringly under the stable recurring-job <paramref name="id"/>.</summary>
        IRecurringFrequency Schedule(string id, IEvent @event, string? queue = null);

        /// <summary>Remove a recurring schedule by id (no-op if it doesn't exist).</summary>
        void Remove(string id);
    }

    /// <summary>
    /// Frequency stage of a recurring schedule — pick exactly one. Returns an <see cref="IRecurringSchedule"/>
    /// that can be further constrained (day-of-week, time zone). Minute-granularity only (Hangfire recurring
    /// jobs are not sub-minute).
    /// </summary>
    public interface IRecurringFrequency
    {
        IRecurringSchedule EveryMinute();
        IRecurringSchedule EveryFiveMinutes();
        IRecurringSchedule EveryTenMinutes();
        IRecurringSchedule EveryFifteenMinutes();
        IRecurringSchedule EveryThirtyMinutes();
        IRecurringSchedule Hourly();
        /// <summary>Every hour, at <paramref name="minute"/> (0–59) past the hour.</summary>
        IRecurringSchedule HourlyAt(int minute);
        /// <summary>Every day at midnight (server/UTC unless <see cref="IRecurringSchedule.Zoned"/> is applied).</summary>
        IRecurringSchedule Daily();
        /// <summary>Every day at <paramref name="hour"/> (0–23) and <paramref name="minute"/> (0–59).</summary>
        IRecurringSchedule DailyAt(int hour, int minute = 0);
        /// <summary>Once a week (Sunday at midnight by default; refine with a day-of-week method).</summary>
        IRecurringSchedule Weekly();
        /// <summary>Once a month, on the 1st at midnight.</summary>
        IRecurringSchedule Monthly();
        /// <summary>Raw cron expression escape hatch (5-field cron).</summary>
        IRecurringSchedule Cron(string cronExpression);
    }

    /// <summary>
    /// A registered recurring schedule that can be further constrained. Each call re-applies (idempotent
    /// upsert), so the recurring job always reflects the accumulated state.
    /// </summary>
    public interface IRecurringSchedule
    {
        IRecurringSchedule Monday();
        IRecurringSchedule Tuesday();
        IRecurringSchedule Wednesday();
        IRecurringSchedule Thursday();
        IRecurringSchedule Friday();
        IRecurringSchedule Saturday();
        IRecurringSchedule Sunday();
        /// <summary>Monday–Friday.</summary>
        IRecurringSchedule Weekday();
        /// <summary>Saturday and Sunday.</summary>
        IRecurringSchedule Weekend();
        /// <summary>Interpret the schedule's time in the given time zone (default is UTC).</summary>
        IRecurringSchedule Zoned(TimeZoneInfo timeZone);
    }
}
