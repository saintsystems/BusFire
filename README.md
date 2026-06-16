# BusFire

A simple [Hangfire](https://www.hangfire.io/)-backed command/event bus for .NET — a **durable mediator**. You dispatch `ICommand`s and `IEvent`s through a MediatR-style surface, and BusFire runs the handlers as durable Hangfire background jobs (persisted, retried, schedulable, observable via the Hangfire dashboard) without standing up a separate message broker.

The design is inspired by [Laravel's bus/dispatcher](https://laravel.com/docs/queues) and its `ShouldQueue` contract: messages dispatch **in-process by default**, and run **on a durable queue when they opt in**. (See [Status](#status) — the opt-in path is the next milestone; today every message is queued.)

> **Status: pre-1.0, not yet published to nuget.org.** The public API is still being reshaped — see [`docs/ROADMAP.md`](docs/ROADMAP.md). Don't take a dependency on it yet.

## How it works

```
caller → IBus.Send/Publish  →  enqueue Hangfire job  →  (SQL Server)  →  Hangfire server
                                                                              │
                                          HangfireBridge → BusInternal → your handler
```

1. `IBus.Send(command)` / `Publish(event)` enqueue a Hangfire job rather than invoking handlers directly.
2. Hangfire persists the job (SQL Server) and a Hangfire server picks it up.
3. `HangfireBridge` resolves the message's handler(s) via `BusInternal` and runs them, through the pipeline behaviors, in a fresh DI scope.

## Quick start

Define a command and its handler:

```csharp
public record SendWelcomeEmail(string Email) : ICommand;

public class SendWelcomeEmailHandler : ICommandHandler<SendWelcomeEmail>
{
    public Task Handle(SendWelcomeEmail command, CancellationToken ct) { /* ... */ }
}
```

Register on the **producer** (anything that dispatches):

```csharp
services.AddBusFire(
    new BusOptions { ConnectionStringOrName = "BusFire" },
    cfg => cfg.RegisterServicesFromAssemblyContaining<SendWelcomeEmailHandler>());
```

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
- **Pipeline behaviors** — `ICommandPreProcessor`, `ICommandPostProcessor`, `ICommandExceptionHandler`, `ICommandExceptionAction`, `IPipelineBehavior`.
- **`IFailureHandler`** — invoked when a job exhausts retries and lands in the failed state (wired via `NotifyOnFailureAttribute`).
- **`BusFireServiceConfiguration`** — the `cfg` builder: register handler assemblies, swap the `IEventPublisher`, set the `IFailureHandler`, choose lifetimes and exception strategy.

## Operational contract

BusFire delivers **at least once** and retries the whole job on failure, so:

- **Handlers must be idempotent.**
- For events with multiple handlers, a failure in one handler currently re-runs *all* of them on retry (see roadmap for per-handler isolation).
- `Send`/`Publish` enqueue on Hangfire's own storage connection — they do **not** enlist in your business DB transaction. Use an outbox or transaction enlistment if you need exactly-once-relative-to-your-data semantics.

## License

[MIT](LICENSE). Depends on Hangfire (LGPLv3) — fine as a dependency.
