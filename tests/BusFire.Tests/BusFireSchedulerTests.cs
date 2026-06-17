using BusFire;
using BusFire.Infrastructure;
using Hangfire;
using Hangfire.Common;
using Xunit;

namespace BusFire.Tests;

public class BusFireSchedulerTests
{
    private sealed class RecordingRecurringJobManager : IRecurringJobManager
    {
        public List<(string Id, Job Job, string Cron, RecurringJobOptions Options)> Upserts { get; } = new();
        public List<string> Removed { get; } = new();
        public List<string> Triggered { get; } = new();

        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options)
            => Upserts.Add((recurringJobId, job, cronExpression, options));
        public void RemoveIfExists(string recurringJobId) => Removed.Add(recurringJobId);
        public void Trigger(string recurringJobId) => Triggered.Add(recurringJobId);
    }

    private static (BusFireScheduler scheduler, RecordingRecurringJobManager manager) Create()
    {
        var manager = new RecordingRecurringJobManager();
        return (new BusFireScheduler(manager), manager);
    }

    [Theory]
    [InlineData("every-minute", "* * * * *")]
    [InlineData("five", "*/5 * * * *")]
    public void Frequency_maps_to_cron(string _, string expectedCron)
    {
        var (scheduler, manager) = Create();
        if (expectedCron.StartsWith("*/5")) scheduler.Schedule("id", new Ping("x")).EveryFiveMinutes();
        else scheduler.Schedule("id", new Ping("x")).EveryMinute();

        Assert.Equal(expectedCron, manager.Upserts[^1].Cron);
    }

    [Fact]
    public void Hourly_daily_weekly_monthly_map_to_cron()
    {
        var (scheduler, manager) = Create();

        scheduler.Schedule("h", new Ping("x")).Hourly();
        Assert.Equal("0 * * * *", manager.Upserts[^1].Cron);

        scheduler.Schedule("ha", new Ping("x")).HourlyAt(15);
        Assert.Equal("15 * * * *", manager.Upserts[^1].Cron);

        scheduler.Schedule("d", new Ping("x")).Daily();
        Assert.Equal("0 0 * * *", manager.Upserts[^1].Cron);

        scheduler.Schedule("da", new Ping("x")).DailyAt(2, 30);
        Assert.Equal("30 2 * * *", manager.Upserts[^1].Cron);

        scheduler.Schedule("w", new Ping("x")).Weekly();
        Assert.Equal("0 0 * * 0", manager.Upserts[^1].Cron);

        scheduler.Schedule("m", new Ping("x")).Monthly();
        Assert.Equal("0 0 1 * *", manager.Upserts[^1].Cron);
    }

    [Fact]
    public void Day_of_week_refines_the_frequency()
    {
        var (scheduler, manager) = Create();

        scheduler.Schedule("wk", new Ping("x")).Weekly().Monday();
        Assert.Equal("0 0 * * 1", manager.Upserts[^1].Cron);

        scheduler.Schedule("wd", new Ping("x")).Daily().Weekday();
        Assert.Equal("0 0 * * 1-5", manager.Upserts[^1].Cron);

        scheduler.Schedule("we", new Ping("x")).Daily().Weekend();
        Assert.Equal("0 0 * * 0,6", manager.Upserts[^1].Cron);
    }

    [Fact]
    public void Cron_escape_hatch_is_passed_through()
    {
        var (scheduler, manager) = Create();
        scheduler.Schedule("c", new Ping("x")).Cron("0 */6 * * *");
        Assert.Equal("0 */6 * * *", manager.Upserts[^1].Cron);
    }

    [Fact]
    public void Schedules_a_command_through_the_bridge_Send()
    {
        var (scheduler, manager) = Create();
        scheduler.Schedule("cmd", new Ping("x")).Daily();

        var upsert = manager.Upserts[^1];
        Assert.Equal("cmd", upsert.Id);
        Assert.Equal(nameof(HangfireBridge.Send), upsert.Job.Method.Name);
        Assert.Equal(typeof(Ping).FullName, upsert.Job.Args[0]);
        Assert.Equal("default", upsert.Job.Args[2]);
    }

    [Fact]
    public void Schedules_an_event_through_the_bridge_Publish_with_queue()
    {
        var (scheduler, manager) = Create();
        scheduler.Schedule("evt", new Pinged("e"), queue: "reports").Weekly();

        var upsert = manager.Upserts[^1];
        Assert.Equal(nameof(HangfireBridge.Publish), upsert.Job.Method.Name);
        Assert.Equal("reports", upsert.Job.Args[2]);
    }

    [Fact]
    public void Zoned_sets_the_recurring_job_time_zone()
    {
        var (scheduler, manager) = Create();
        var tz = TimeZoneInfo.Utc;

        scheduler.Schedule("z", new Ping("x")).Daily().Zoned(tz);

        Assert.Equal(tz, manager.Upserts[^1].Options.TimeZone);
    }

    [Fact]
    public void Remove_removes_the_recurring_job()
    {
        var (scheduler, manager) = Create();
        scheduler.Remove("gone");
        Assert.Contains("gone", manager.Removed);
    }

    [Fact]
    public void Invalid_inputs_throw()
    {
        var (scheduler, _) = Create();
        Assert.Throws<ArgumentException>(() => scheduler.Schedule("", new Ping("x")));
        Assert.Throws<ArgumentNullException>(() => scheduler.Schedule("id", (ICommand)null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule("id", new Ping("x")).HourlyAt(60));
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule("id", new Ping("x")).DailyAt(24, 0));
    }
}
