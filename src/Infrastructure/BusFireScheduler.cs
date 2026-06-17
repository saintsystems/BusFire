using System;
using System.Threading;
using Hangfire;
using Hangfire.Common;

namespace BusFire.Infrastructure
{
    /// <summary>
    /// <see cref="IBusFireScheduler"/> backed by Hangfire's recurring-job scheduler. Each schedule registers
    /// a recurring job whose body is the existing <see cref="HangfireBridge"/> dispatch — so recurring work
    /// flows through the same serializer/handlers/fan-out/failure pipeline as one-shot dispatch.
    /// </summary>
    public sealed class BusFireScheduler : IBusFireScheduler
    {
        private readonly IRecurringJobManager _recurringJobs;

        public BusFireScheduler(IRecurringJobManager recurringJobs)
        {
            _recurringJobs = recurringJobs ?? throw new ArgumentNullException(nameof(recurringJobs));
        }

        public IRecurringFrequency Schedule(string id, ICommand command, string? queue = null)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var q = queue ?? "default";
            var job = Job.FromExpression<HangfireBridge>(b => b.Send(command.GetType().FullName, command, q, CancellationToken.None));
            return new Builder(_recurringJobs, RequireId(id), job);
        }

        public IRecurringFrequency Schedule(string id, IEvent @event, string? queue = null)
        {
            if (@event == null) throw new ArgumentNullException(nameof(@event));
            var q = queue ?? "default";
            var job = Job.FromExpression<HangfireBridge>(b => b.Publish(@event.GetType().FullName, @event, q, CancellationToken.None));
            return new Builder(_recurringJobs, RequireId(id), job);
        }

        public void Remove(string id) => _recurringJobs.RemoveIfExists(RequireId(id));

        private static string RequireId(string id)
            => string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("A recurring schedule id is required.", nameof(id)) : id;

        // Builds the cron from fields so a day-of-week method can refine a frequency, and re-upserts the
        // recurring job on each call (idempotent) so it always reflects the accumulated state.
        private sealed class Builder : IRecurringFrequency, IRecurringSchedule
        {
            private readonly IRecurringJobManager _recurringJobs;
            private readonly string _id;
            private readonly Job _job;

            private string _minute = "*";
            private string _hour = "*";
            private string _dayOfMonth = "*";
            private string _month = "*";
            private string _dayOfWeek = "*";
            private string? _rawCron;
            private TimeZoneInfo? _timeZone;

            public Builder(IRecurringJobManager recurringJobs, string id, Job job)
            {
                _recurringJobs = recurringJobs;
                _id = id;
                _job = job;
            }

            // --- frequency ---
            public IRecurringSchedule EveryMinute() => Apply();
            public IRecurringSchedule EveryFiveMinutes() => SetMinute("*/5");
            public IRecurringSchedule EveryTenMinutes() => SetMinute("*/10");
            public IRecurringSchedule EveryFifteenMinutes() => SetMinute("*/15");
            public IRecurringSchedule EveryThirtyMinutes() => SetMinute("*/30");
            public IRecurringSchedule Hourly() => SetMinute("0");
            public IRecurringSchedule HourlyAt(int minute) { _minute = Range(minute, 0, 59, nameof(minute)).ToString(); return Apply(); }
            public IRecurringSchedule Daily() { _minute = "0"; _hour = "0"; return Apply(); }
            public IRecurringSchedule DailyAt(int hour, int minute = 0)
            {
                _hour = Range(hour, 0, 23, nameof(hour)).ToString();
                _minute = Range(minute, 0, 59, nameof(minute)).ToString();
                return Apply();
            }
            public IRecurringSchedule Weekly() { _minute = "0"; _hour = "0"; _dayOfWeek = "0"; return Apply(); }
            public IRecurringSchedule Monthly() { _minute = "0"; _hour = "0"; _dayOfMonth = "1"; return Apply(); }
            public IRecurringSchedule Cron(string cronExpression)
            {
                _rawCron = string.IsNullOrWhiteSpace(cronExpression)
                    ? throw new ArgumentException("Cron expression is required.", nameof(cronExpression))
                    : cronExpression;
                return Apply();
            }

            // --- day-of-week constraints ---
            public IRecurringSchedule Monday() => SetDayOfWeek("1");
            public IRecurringSchedule Tuesday() => SetDayOfWeek("2");
            public IRecurringSchedule Wednesday() => SetDayOfWeek("3");
            public IRecurringSchedule Thursday() => SetDayOfWeek("4");
            public IRecurringSchedule Friday() => SetDayOfWeek("5");
            public IRecurringSchedule Saturday() => SetDayOfWeek("6");
            public IRecurringSchedule Sunday() => SetDayOfWeek("0");
            public IRecurringSchedule Weekday() => SetDayOfWeek("1-5");
            public IRecurringSchedule Weekend() => SetDayOfWeek("0,6");

            // --- options ---
            public IRecurringSchedule Zoned(TimeZoneInfo timeZone)
            {
                _timeZone = timeZone ?? throw new ArgumentNullException(nameof(timeZone));
                return Apply();
            }

            private IRecurringSchedule SetMinute(string minute) { _minute = minute; return Apply(); }
            private IRecurringSchedule SetDayOfWeek(string dayOfWeek) { _dayOfWeek = dayOfWeek; return Apply(); }

            private IRecurringSchedule Apply()
            {
                var cron = _rawCron ?? $"{_minute} {_hour} {_dayOfMonth} {_month} {_dayOfWeek}";
                var options = _timeZone == null ? new RecurringJobOptions() : new RecurringJobOptions { TimeZone = _timeZone };
                _recurringJobs.AddOrUpdate(_id, _job, cron, options);
                return this;
            }

            private static int Range(int value, int min, int max, string name)
                => value < min || value > max ? throw new ArgumentOutOfRangeException(name, value, $"Must be between {min} and {max}.") : value;
        }
    }
}
