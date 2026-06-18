# BusFire

[![NuGet](https://img.shields.io/nuget/v/BusFire.svg)](https://www.nuget.org/packages/BusFire)
[![NuGet downloads](https://img.shields.io/nuget/dt/BusFire.svg)](https://www.nuget.org/packages/BusFire)
[![CI](https://github.com/saintsystems/BusFire/actions/workflows/ci.yml/badge.svg)](https://github.com/saintsystems/BusFire/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A simple [Hangfire](https://www.hangfire.io/)-backed command/event bus for .NET — a **durable mediator**. You dispatch `ICommand`s and `IEvent`s through a MediatR-style surface; handlers run in-process by default, or — when a message opts in — as durable Hangfire background jobs (persisted, retried, schedulable, observable via the Hangfire dashboard) without standing up a separate message broker.

The design is inspired by [Laravel's bus/dispatcher](https://laravel.com/docs/queues) and its `ShouldQueue` contract: messages dispatch **in-process by default**, and run **on a durable queue when they opt in** by implementing `IShouldQueue`.

> **Pre-1.0:** the public API may still change between minor versions — see [`docs/ROADMAP.md`](docs/ROADMAP.md).

## How it works

```
                          ┌─ message is NOT IShouldQueue ─→ run inline now (BusInternal → handler)
caller → IBus.Send/Publish ┤
                          └─ message IS IShouldQueue ─→ enqueue Hangfire job → (SQL Server) → Hangfire server
                                                                                                   │
                                                               HangfireBridge → BusInternal → your handler
```

1. `IBus.Send(command)` / `Publish(event)` run the handler(s) **in-process** by default, through the pipeline behaviors, in a fresh DI scope.
2. If the message implements `IShouldQueue`, BusFire instead **enqueues a Hangfire job** so it runs durably on a Hangfire server. `Defer(...)` always queues (a delayed message can't run inline now).
3. On the queued path, Hangfire persists the job (SQL Server), a Hangfire server picks it up, and `HangfireBridge` resolves and runs the handler(s) via `BusInternal`.

Queued messages are persisted with a stable logical type name (no assembly-qualified `$type`), so jobs survive type/assembly renames; override a message's logical name with `[MessageName("...")]` if you refactor its namespace.

## Quick start

Define a command and its handler:

```csharp
// Runs inline by default. Add `, IShouldQueue` to make it run as a durable Hangfire job instead:
public record SendWelcomeEmail(string Email) : ICommand, IShouldQueue;

public class SendWelcomeEmailHandler : ICommandHandler<SendWelcomeEmail>
{
    public Task Handle(SendWelcomeEmail command, CancellationToken ct) { /* ... */ }
}
```

Register on the **producer** (anything that dispatches). BusFire is storage-agnostic — **you own Hangfire and its storage**, and call `config.UseBusFire(provider)` to wire BusFire's serializer + failure filter:

```csharp
services.AddBusFire(cfg => cfg.RegisterServicesFromAssemblyContaining<SendWelcomeEmailHandler>());

services.AddHangfire((provider, config) =>
{
    config.UsePostgreSqlStorage(connectionString); // or SQL Server, Redis, in-memory, ...
    config.UseBusFire(provider);                   // required: BusFire serializer + failure filter
});
```

> **Let BusFire own the Hangfire bootstrap:** if the host doesn't already configure Hangfire, use the convenience overload — BusFire makes the `AddHangfire` call and applies its serializer + filter for you; you just supply the storage (any storage, no SQL lock-in):
> ```csharp
> services.AddBusFire(
>     cfg => cfg.RegisterServicesFromAssemblyContaining<SendWelcomeEmailHandler>(),
>     hangfire => hangfire.UsePostgreSqlStorage(connectionString));
> ```
> Don't also call `AddHangfire` yourself when using this overload.

Register on the **consumer** (the app that should process jobs):

```csharp
services.AddBusFireServer(); // adds the Hangfire server that drains the queues
```

Dispatch:

```csharp
await bus.Send(new SendWelcomeEmail("a@b.com"));
await bus.Defer(new SendWelcomeEmail("a@b.com"), TimeSpan.FromMinutes(5)); // delayed
await bus.Publish(new UserRegistered(userId));                            // fan-out to IEventHandler<UserRegistered>
```

A pure producer calls `AddBusFire` only; the worker that runs handlers also calls `AddBusFireServer`.

## Concepts

- **`ICommand` / `ICommandHandler<T>`** — one handler per command.
- **`IEvent` / `IEventHandler<T>`** — many handlers per event.
- **`IShouldQueue`** — marker a message implements to opt into durable queued dispatch; without it, dispatch runs in-process.
- **`IQueueable : IShouldQueue`** — opt into queueing *and* declare routing: read-only `Queue` and `Delay` getters (which may be computed). Precedence is per-call argument › `IQueueable` › default. Example:
  ```csharp
  public record SendInvoice(int Id) : ICommand, IQueueable
  {
      public string? Queue => "billing";
      public TimeSpan? Delay => TimeSpan.FromMinutes(5);
  }
  ```
- **`[MessageName("...")]`** — pins a message's stable logical name on the wire so namespace/assembly renames don't break in-flight jobs.
- **Pipeline behaviors** — `ICommandPreProcessor`, `ICommandPostProcessor`, `ICommandExceptionHandler`, `ICommandExceptionAction`, `IPipelineBehavior`.
- **`IFailureHandler`** — invoked when a job exhausts retries and lands in the failed state (wired via `NotifyOnFailureAttribute`).
- **`BusFireServiceConfiguration`** — the `cfg` builder: register handler assemblies, swap the `IEventPublisher`, set the `IFailureHandler`, choose lifetimes and exception strategy.

## Organizing a message with its handler (the "Job" convention)

BusFire keeps the message (data) and its handler (behavior) as **separate types** — that's what keeps the
serialized payload pure data and lets events fan out to many handlers. But you don't have to scatter them:
co-locate both as **nested types in one container class**, the idiomatic "vertical slice" style. You get
Job-like cohesion (open one file, see the data *and* the behavior) without giving up the separation:

```csharp
public static class SendWelcomeEmail              // the "Job" container
{
    [MessageName("send-welcome-email")]           // clean, rename-safe wire name
    public sealed record Command(string Email) : ICommand, IShouldQueue;

    public sealed class Handler : ICommandHandler<Command>
    {
        private readonly IEmailService _email;
        public Handler(IEmailService email) => _email = email;
        public Task Handle(Command command, CancellationToken ct) => _email.SendWelcomeAsync(command.Email, ct);
    }
}

await bus.Send(new SendWelcomeEmail.Command("a@b.com"));   // reads like a job
```

Assembly scanning discovers nested handlers automatically — no extra registration. Keep the container and
nested types `public`, and prefer `[MessageName("…")]` on the message (a nested type's `FullName` uses `+`
and changes if you rename the container, so pin a stable logical name).

## Recurring (scheduled) dispatch

Dispatch a message on a cron schedule via **`IBusFireScheduler`** — a fourth trigger alongside
`Send`/`Defer`/`IQueueable`. Hangfire's recurring-job scheduler provides the durable cron engine; when it
fires, the message flows through the same handler pipeline as any other dispatch. Define schedules in code
at startup (idempotent — a stable `id` upserts), with a Coravel/Laravel-style fluent surface:

```csharp
var scheduler = provider.GetRequiredService<IBusFireScheduler>();

scheduler.Schedule("nightly-rollup", new RunNightlyRollup.Command()).DailyAt(2, 30).Zoned(TimeZoneInfo.Local);
scheduler.Schedule("heartbeat",      new Heartbeat()).EveryFiveMinutes();
scheduler.Schedule("weekly-report",  new SendWeeklyReport(), queue: "reports").Weekly().Monday();
scheduler.Schedule("six-hourly",     new Sync()).Cron("0 */6 * * *");   // raw cron escape hatch

scheduler.Remove("heartbeat");   // unschedule by id
```

**Keep the schedule in sync with your code** by declaring all schedules in one block at startup.
`ConfigureSchedules` upserts everything you declare and then **prunes any BusFire-owned recurring job that
isn't declared** — so renaming or deleting a schedule doesn't leave an orphan firing forever in storage
(the gap a durable scheduler has that a stateless one like Laravel/Coravel doesn't):

```csharp
scheduler.ConfigureSchedules(s =>
{
    s.Schedule("nightly-rollup", new RunNightlyRollup.Command()).DailyAt(2, 30);
    s.Schedule("heartbeat",      new Heartbeat()).EveryFiveMinutes();
});
// any other busfire-owned recurring job not declared above is removed
```

BusFire recurring-job ids are namespaced with a `busfire:` prefix in storage/the dashboard (so pruning only
ever touches BusFire's own jobs, never recurring jobs you registered directly with Hangfire). You still pass
plain ids to `Schedule`/`Remove`.

Frequencies: `EveryMinute`/`EveryFiveMinutes`/`EveryTenMinutes`/`EveryFifteenMinutes`/`EveryThirtyMinutes`,
`Hourly`/`HourlyAt(m)`, `Daily`/`DailyAt(h,m)`, `Weekly`, `Monthly`, `Cron(...)`; refine with
`Monday()…Sunday()`/`Weekday()`/`Weekend()` and `Zoned(tz)`. Schedules are **minute-granularity** (Hangfire
recurring jobs aren't sub-minute). For a plain recurring CLR call with no handler, use Hangfire's
`RecurringJob` directly — `IBusFireScheduler` is for when the recurring trigger should run a BusFire handler.

## Operational contract

For **queued** messages (`IShouldQueue` / `Defer`), BusFire delivers **at least once** and retries the whole job on failure, so:

- **Handlers must be idempotent.**
- Events fan out to **one job per handler**, so a failure retries only that handler — not all of them. (A handler that itself isn't idempotent can still re-run on its own retry.)
- `Send`/`Publish` enqueue on Hangfire's own storage connection — they do **not** enlist in your business DB transaction. Use an outbox or transaction enlistment if you need exactly-once-relative-to-your-data semantics.

## License

[MIT](LICENSE). Depends on Hangfire (LGPLv3) — fine as a dependency.
