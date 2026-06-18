# BusFire — Design Review

The analysis behind the [roadmap](ROADMAP.md): why the pattern is sound and the architectural decisions
behind it. This captures the reasoning so it isn't lost; the actionable items live in `ROADMAP.md`.

## Is the pattern sound?

Yes — BusFire is a **"durable mediator"**: a MediatR-style dispatch surface (`ICommand`/`IEvent`
+ handlers) backed by Hangfire/SQL so messages survive restarts and get retries, scheduling, and a
dashboard, without standing up a broker (RabbitMQ/Azure Service Bus). For a client already on SQL
Server who doesn't want broker infrastructure, that's a genuine sweet spot.

The tension: it **conflates in-process mediation with durable messaging**. The current baseline
*always* queues, so even a trivial command pays a SQL round-trip + serialization. The original
design (from Laravel's `ShouldQueue`) was better — run inline unless the message opts into queuing.
**Restoring that conditional dispatch is the headline fix** (`IShouldQueue` exists but is unused).

## Concerns, ranked by blast radius for a third party

1. **`TypeNameHandling.All` — security + versioning landmine.** Newtonsoft with `TypeNameHandling.All`
   is a known **RCE gadget vector** if any payload is even partially untrusted, and it embeds
   assembly-qualified type names into persisted jobs, so renaming/moving a type breaks in-flight jobs.
   Fix: stable logical type-name registry or a strict `ISerializationBinder` allowlist.
2. **At-least-once + multi-handler events = duplicate execution.** Hangfire retries the *whole job*;
   with the foreach-await publisher, if handler #3 of 5 throws, all five re-run. ⇒ handlers must be
   **idempotent**, and events should ideally fan out to one job per handler for failure isolation.
3. **Dual-write / no outbox.** `Enqueue` writes on Hangfire's own connection and commits immediately
   — it does not enlist in the caller's business DB transaction, so you can enqueue then roll back
   (or vice versa). Enlist in an ambient transaction, or document that an outbox is the user's job.
4. ~~**Storage + Hangfire setup are baked in.**~~ **Resolved (2026-06-16).** `AddBusFire(cfg => ...)` is now
   storage-agnostic (host owns Hangfire + storage and calls `config.UseBusFire(provider)`); an optional
   convenience overload takes a storage delegate. The SQL-Server-specific path and the `Hangfire.SqlServer`
   dependency were dropped (now references `Hangfire.Core` + `Hangfire.AspNetCore`). Unlocks PostgreSQL etc.
5. **Static mutable global config** (`BusFireGlobalConfiguration.Configuration`) breaks test isolation
   and multiple-instance scenarios.
6. **Serialized `CancellationToken`** is meaningless on the consumer side; rely on Hangfire's injected token.
7. **No tests, single TFM, `LangVersion` was `preview`** (now `latest`). Zero tests on a fork of MediatR
   internals is risky for a shared library; add tests and multi-target net8/net9.

## Build-vs-buy sanity check

Before investing heavily, weigh against mature options that do this pattern:
- **Durable mediator + transactional outbox libraries** — there are mature libraries that already pair a
  mediator surface with a transactional outbox, scheduling, and retry/saga support, solving the
  outbox/idempotency problems directly. If P0/P1 drift toward reimplementing outbox + retry policy +
  sagas, prefer one of those over building it here.
- **MassTransit / Rebus / NServiceBus** — full messaging (broker-oriented, SQL transports exist).
- **Hangfire directly** — if the value is just "durable background jobs," a thin convention layer may do.
- **MediatR** — note its 2024–2025 move to **commercial licensing**; an argument *for* owning the
  dispatch layer (BusFire already inlines MediatR's `ServiceRegistrar`), but also ongoing maintenance.

The differentiator that justifies BusFire: **"durable mediator on SQL, zero broker, conditional
inline/queue"** ergonomics. Keep that crisp and fix the above, and there's a real niche.

## Lineage

BusFire descends from an internal `FireBus` fork (itself derived from an earlier internal library), both
modeled on Laravel's bus/`ShouldQueue`. That fork cut the MediatR package dependency and external project
references, inlining a stripped-down MediatR to become standalone; BusFire is the cleaned, rebranded version.
The commented-out MediatR blocks and the `#define FIREBUS` toggle in `IBus.cs` remain to document that lineage.

## Message/handler separation vs the "Job" model (Laravel/Coravel)

**Decision: keep request/handler separation; do *not* adopt a combined Job type.** Laravel (Jobs) and
Coravel (`IInvocable`) merge data + behavior in one class — but only for the *command* side; both keep
**events + listeners separated** (1:many), which BusFire already mirrors with `IEvent`/`IEventHandler`.
So the only open question was the command side, and three BusFire-specific properties make separation the
right call:

1. **Serializer safety.** The P0 logical-type-name serializer (`TypeNameHandling.None`) is safe *because
   messages are pure data DTOs*. A combined Job serializes the whole instance — the classic footgun is
   injected services ending up on the wire. Separation keeps the payload data-only by construction.
2. **Per-handler event fan-out** can't be expressed by a 1-class combined job.
3. **Pipeline behaviors** wrap a *separate* handler cleanly.

The legitimate pull of the Job model is **cohesion/locality** for simple/recurring tasks. We get that
*without merging* via the **nested-container convention** (a `static class` holding a nested `Command`
record + `Handler`) — the established MediatR "vertical slice" shape. It needs **zero engine changes**:
assembly scanning already finds nested handlers (the test suite relies on exactly this). This supersedes an
earlier `ISelfHandling` idea, which merged data+behavior and reintroduced the serialization footgun.

## Recurring jobs & the scheduler↔dispatcher relationship

**Decision: recurring is a *trigger*, not a subsystem; the scheduler stays separate from the dispatcher.**
In Laravel the Scheduler **depends on** the Dispatcher (one-directional) — `$schedule->job()` delegates to
`dispatch()`, and a scheduled job uses the *identical* dispatch contract. "When it fires" is orthogonal to
"how it runs / who handles it." BusFire should compose them the same way: Hangfire's `RecurringJob` is the
cron engine; the recurring job's body is the existing `HangfireBridge.Send`/`Publish`, so recurring feeds
the same invariant pipeline as every other trigger. Put the fluent API on a separate `IBusFireScheduler`
(depends on the bus/bridge; the bus knows nothing about cron). Borrow **Coravel's fluent frequency DSL**
(Laravel's scheduler for .NET) for ergonomics, but **not its in-process/non-durable engine** — pairing
Coravel-style syntax with Hangfire durability is the sweet spot. Actionable design + scope in
[`ROADMAP.md`](ROADMAP.md#recurring-scheduled-jobs--design-note-proposed-not-yet-built).
