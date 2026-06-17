# BusFire — Design Review

The analysis behind the [roadmap](ROADMAP.md): why the pattern is sound, what to fix before
publishing, and how BusFire's lineage (`Kwik.Bus` → internal `FireBus` fork → `BusFire`) shaped it.
This captures the reasoning so it isn't lost; the actionable items live in `ROADMAP.md`.

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

## Lineage: `Kwik.Bus` → internal `FireBus` fork → `BusFire`

`Kwik.Bus` (at `D:\git\saintsystems\kwik\src\Bus`) is the ancestor. An internal `FireBus` fork forked it and
**cut the MediatR dependency and external project refs** (`Contracts`, `Support`, `Notifications`),
inlining a stripped MediatR — that's what made it a standalone library. BusFire is the cleaned rebrand.

### What FireBus (and thus BusFire today) *dropped* vs Kwik.Bus — candidates to restore

1. **Inline (non-queued) dispatch** — Kwik ran inline via the mediator unless `IShouldQueue`; BusFire
   always queues. The `IShouldQueue` marker is still present but unreferenced. (P0)
2. **`IQueueable`** — self-describing queue name + delay on the message, with `OnQueue()`/`WithDelay()`.
   BusFire takes `queue`/delay as method args instead. (P1)
3. **Generic signatures** — Kwik: `Send<TCommand>` / `Publish<TEvent>`; BusFire: non-generic.
4. **Request/response + streaming** — via MediatR (`IRequest<TResponse>`, `CreateStream`/`IStreamRequest`).
   BusFire declares `ICommand<out TResponse>` but has no handler interface or dispatch path for it.
5. **Separate contracts assembly** — Kwik split the public surface into `Contracts`; BusFire bundles it.

### What FireBus/BusFire *added* vs Kwik.Bus — keep

1. **Failure handling** — `NotifyOnFailureAttribute` (Hangfire job filter) + `IFailureHandler` /
   `NullFailureHandler`, wired into registration. No equivalent in Kwik.
2. **Working pipeline** — pre/post processors, exception handlers/actions, and the conditional
   registration logic. Kwik referenced MediatR's pipeline types but registered none.
3. **Self-contained** — no MediatR / no `Contracts`/`Support` refs; netstandard2.0.
4. **A config that actually runs** — Kwik's `FireBusServiceConfiguration` invocation was commented out.

### Caveat about Kwik.Bus

Kwik is **mid-refactor**: `KwikMediator.cs` (a custom `IMediator`) is present but **not wired**
(`AddMediatR` is used; `MediatorImplementationType = typeof(KwikMediator)` is commented out) and
`BusInternal.cs` is `<Compile Remove>`'d. So Kwik was drifting toward a custom mediator that never
landed; FireBus took the alternative route of dropping MediatR outright. Treat Kwik as reference only.

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
