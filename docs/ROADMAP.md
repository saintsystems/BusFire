# BusFire Roadmap

This file tracks the work to take BusFire from the lift-and-shift baseline (a faithful
rebrand of an internal `FireBus` fork) to a publishable, reusable NuGet package.

## Origin & design intent

BusFire descends from two earlier internal libraries — `Kwik.Bus` and an internal `FireBus` fork
— both modeled on **Laravel's bus/dispatcher and its `ShouldQueue` contract**. The defining
idea: a message dispatches **synchronously in-process by default**, and is pushed onto a
**durable queue only when it opts in** (Laravel: implement `ShouldQueue`; here: implement
`IShouldQueue`). The current baseline regressed from this — it *always* queues — and
restoring the conditional model is the headline goal.

## Baseline (done)

- [x] Extracted to its own git repo, MIT-licensed.
- [x] Rebranded the internal `FireBus` fork → `BusFire` (namespace, identifiers, package id).
- [x] Removed client branding, TFVC/SCC metadata, and the private NuGet feed (incl. the
      embedded DevExpress key).

## P0 — correctness & safety (do before any publish)

- [x] **Restore conditional dispatch (the `ShouldQueue` model).** Run inline via the
      mediator unless the message implements `IShouldQueue`; only then enqueue on Hangfire.
      *Done (2026-06-16):* `Bus` now injects `IBusInternal` and runs `Send`/`Publish` inline
      unless the message implements `IShouldQueue`. `Defer` always queues (a delayed message
      can't run inline "now"). The `IShouldQueue` marker is now referenced.
- [x] **Replace `TypeNameHandling.All`.** *Done (2026-06-16):* moved to `TypeNameHandling.None` plus a
      logical message-type registry (`IMessageTypeRegistry`/`MessageTypeRegistry`) + `MessageJsonConverter`.
      Jobs now persist a stable `__busfire_type` name (default `Type.FullName`, overridable via the new
      `[MessageName]` attribute) instead of assembly-qualified `$type` — closing the RCE vector and making
      persisted jobs rename-safe.
- [x] **Per-handler event jobs / idempotency story.** *Done (2026-06-16):* the queued event path now
      fans out — `HangfireBridge.Publish` enqueues one `RunEventHandler` job per registered handler
      (via `IBusInternal.GetEventHandlerTypeNames` / `PublishToHandler`), each in its own DI scope, so a
      failure retries only that handler. (Inline, non-queued publish still runs all handlers in-process.)
      Handlers must still be idempotent — see the README operational contract.
- [x] **Drop the serialized `CancellationToken`.** *Done (2026-06-16):* the bridge methods take the
      `CancellationToken` last so Hangfire substitutes a live job-cancellation token at run time; the
      producer passes `CancellationToken.None` on the queued path, so no caller token is persisted. The
      caller's token is still honored on the inline path.

## P1 — packaging & API for third parties

- [x] **Decouple from storage / Hangfire bootstrapping.** *Done (2026-06-16):* `AddBusFire(cfg => ...)` is
      storage-agnostic — registers handlers/bus/registry/failure filter but does **not** touch Hangfire; the
      host owns `AddHangfire` + storage (e.g. PostgreSQL) and calls `config.UseBusFire(provider)` to apply
      BusFire's serializer + filter. A convenience overload `AddBusFire(cfg, hangfire => hangfire.UseXxxStorage(...))`
      lets BusFire own the `AddHangfire` call while the host supplies only storage. The SQL-Server-specific
      overload and the `Hangfire.SqlServer` dependency were dropped (now `Hangfire.Core` + `Hangfire.AspNetCore`),
      and `BusOptions` slimmed to just `Queues`.
- [x] **Remove the static global config** (`BusFireGlobalConfiguration.Configuration`). *Done (2026-06-16):*
      `AddBusFire` now builds a fresh `BusFireServiceConfiguration` per call; the static class is gone.
- [x] **Restore `IQueueable`.** *Done (2026-06-16):* added `IQueueable : IShouldQueue` with **read-only**
      `Queue`/`Delay` getters (getters may compute, the .NET equivalent of Laravel `viaQueue()`/`withDelay()`).
      Implementing it implies queueing; the bus reads `Queue`/`Delay` off the message. Per-call `queue` is now
      nullable so precedence is call-arg › `IQueueable` › `"default"`; an explicit `Defer(delay)` overrides
      `IQueueable.Delay`. Read-only (not the original mutable `OnQueue`/`WithDelay`) to keep messages immutable
      and stay netstandard2.0-safe (no default interface methods). Runtime `shouldQueue()` deliberately skipped.
- [x] **Multi-target.** *Done (2026-06-16):* `TargetFrameworks=netstandard2.0;net8.0` (kept netstandard2.0
      for net48; one modern LTS asset covers net8/9/10 consumers — net9 is STS, net10 needs its SDK installed).
      `Microsoft.CSharp` is now referenced only for netstandard2.0 (in-box on net8). `pack` produces both
      `lib/netstandard2.0` and `lib/net8.0` assets.
- [x] **Add SourceLink.** *Done (2026-06-16):* added `Microsoft.SourceLink.GitHub` (`PrivateAssets=all`) +
      `Deterministic`; `dotnet pack` emits the `.snupkg` symbol package. (Set `ContinuousIntegrationBuild=true` in CI.)
- [x] **Decide the surface.** *Done (2026-06-16):* pinned to **void fire-and-forget** commands/events.
      Request/response (`ICommand<TResponse>`) was removed from the active surface — it can't be honored on
      the durable-queue path (no caller to return to). Streaming is out of scope. Can revisit for an
      inline-only request/response path later if a real need appears.
- [ ] **Call-site dispatch override (message-based → hybrid).** Dispatch mode is currently message-based
      (`IShouldQueue`/`IQueueable` declare inline-vs-queued per type). That's the deliberate Laravel-aligned
      default and gives a centralized "can't forget to queue" guarantee, but pure message-based can't express
      *contextual* decisions (same operation queued from a web request, inline from an already-async batch).
      Laravel itself is a hybrid (`ShouldQueue` default + `dispatchSync()`/`->onQueue()` overrides). Add a thin
      call-site override — force-inline and force-queue — keeping the message-based default. Non-breaking
      (additive), so it can land post-baseline. Going *fully* call-based (Wolverine-style, routing via central
      config) is explicitly out of scope — that's reimplementing a full messaging framework.

## P2 — quality & decisions

- [x] **Tests.** *Done (2026-06-16):* `tests/BusFire.Tests` (xUnit, 61 tests) covering conditional
      dispatch + `IQueueable` precedence, the in-process mediator and per-handler fan-out, the pipeline
      (behaviors, pre/post processors, exception handlers/actions), the message-type registry, serializer
      round-trips, registration/scanning, and the default publisher. **~82% line coverage** (Hangfire
      runtime shims — the `JobActivator` and failure `JobFilter` — are `[ExcludeFromCodeCoverage]` as
      integration-only). Run: `dotnet test tests/BusFire.Tests -p:CollectCoverage=true -p:Include='[BusFire]*'`.
      The tests caught and fixed two latent bugs: (1) `CommandExceptionProcessorBehavior` invoked the
      exception handler with 4 args against a 3-arg interface (`TargetParameterCountException` on every use);
      (2) `AddBusFire` never registered `ServiceFactory`, so `CommandExceptionActionProcessorBehavior` threw
      when any `ICommandExceptionAction` existed.
- [x] **CI** (GitHub Actions). *Done (2026-06-17):* `.github/workflows/ci.yml` (build + test with an 80%
      line-coverage gate + pack on push/PR to `main`) and `release.yml` (test + pack + push to nuget.org on a
      `v*` tag via **Trusted Publishing/OIDC** — no stored secret). Versioning is **MinVer** (tag-driven SemVer):
      `<Version>` removed from the csproj; tag `v0.1.0` → package `0.1.0`, untagged builds get a pre-release.
      MinVer sets `AssemblyVersion` to `{Major}.0.0.0` for net48 binding stability and the precise version on
      `FileVersion`/`InformationalVersion`.
- [ ] **Reserve the `SaintSystems.*`-style prefix or confirm `BusFire` ownership** on nuget.org
      before first publish.
- [ ] **Build-vs-buy sanity check.** If P0/P1 drift toward reimplementing an outbox + retry
      policy + sagas, evaluate a mature durable-mediator/outbox library before investing
      further — that exact pattern already exists, matured, off the shelf.

## Recurring (scheduled) jobs — design note (proposed, not yet built)

Add **recurring/cron dispatch** as a *fourth trigger* into the existing dispatch pipeline. Design decided
(see [`DESIGN-REVIEW.md`](DESIGN-REVIEW.md) for rationale); not yet implemented.

- **Hangfire owns the cron engine** (`RecurringJob`): persistence, dashboard, misfire handling. Do **not**
  rebuild scheduling — borrow ergonomics, not the engine.
- **Recurring = the 4th trigger** alongside `Send`/`Defer`/`IQueueable`, all feeding the *same* invariant
  pipeline (bridge → `BusInternal` → handlers, serializer, event fan-out, failure filter). Implementation is
  literally `RecurringJob.AddOrUpdate<HangfireBridge>(id, b => b.Send/Publish(...), cron, options)` — reusing
  the existing bridge, so recurring work flows through the same path as a one-shot queued message. Like
  `Defer`, recurring always runs server-side, so `IShouldQueue` is moot for it.
- **Separate `IBusFireScheduler`**, not on `IBus` — the scheduler *depends on* the bus/bridge; the dispatch
  surface stays ignorant of cron (one-directional dependency, mirroring Laravel Scheduler↔Dispatcher and
  Coravel `IScheduler`). Don't put `Cron`/schedule on messages or handlers.
- **Borrow Coravel's fluent DSL** (it's the Laravel scheduler for .NET): `Schedule(id, message).Daily()` /
  `.Hourly()` / `.EveryFiveMinutes()` / `.DailyAt(h,m)` / `.HourlyAt(m)` / `.Weekly().Monday()` / `.Monthly()`
  / `.Cron("…")`, plus `.PreventOverlapping()` (→ Hangfire `DisableConcurrentExecution`), `.Zoned(tz)` (→
  `RecurringJobOptions.TimeZone`), `.RunOnceAtStart()` (→ `TriggerJob`). Define schedules in code at startup
  (idempotent `AddOrUpdate`), Laravel "schedule is code" style.
- **Don't borrow** Coravel's in-process/non-durable engine, sub-minute helpers (`EverySecond…` — Hangfire
  recurring is minute-grained; document the limit), `Once()` (that's one-shot — already `Send`/`Defer`), or
  `OnError`/`OnWorker` (covered by BusFire's failure filter + `queue` routing).
- Schedule a **message instance** (data), not a Coravel-style invocable — keeps the wire data-only and reuses
  the handler/pipeline. The nested-container "Job" convention (see README) is the recommended authoring shape:
  `scheduler.Schedule("nightly", new RunNightlyReport.Command()).Daily();`
- **Phase 1:** `IBusFireScheduler.Schedule(id, message)` + the fluent frequency builder + `Remove(id)`, over
  the existing bridge, with tests. **Phase 2 (defer):** richer constraints (`When`, more frequencies).

## Known dead code carried over from the fork

- `#define FIREBUS` toggle in `IBus.cs` (FireBus-native vs MediatR interfaces).
- Large blocks of commented-out MediatR code throughout — kept for now to preserve lineage;
  remove during the P1 API pass.
