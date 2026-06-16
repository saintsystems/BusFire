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
4. **Storage + Hangfire setup are baked in.** `AddBusFire` calls `AddHangfire(...UseSqlServerStorage...)`
   itself, colliding with hosts that already configure Hangfire. Invert: consumer owns Hangfire +
   storage; BusFire registers handlers + bridge + serializer. Also unlocks non-SQL-Server storage.
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
