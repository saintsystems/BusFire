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
- [ ] **Multi-target** `netstandard2.0;net8.0;net9.0`; drop `LangVersion=preview` (already set
      to `latest`) and confirm reproducible builds.
- [ ] **Add SourceLink** (`Microsoft.SourceLink.GitHub`) and validate the symbol package.
- [ ] **Decide the surface:** void fire-and-forget only, or also request/response
      (`ICommand<TResponse>`, declared but undispatchable today) and streaming? Pin it.

## P2 — quality & decisions

- [ ] **Tests.** There are currently none. Cover registration/scanning, the pipeline,
      conditional dispatch, serialization round-trips, and failure handling.
- [ ] **CI** (GitHub Actions): build, test, pack; publish to nuget.org on tag.
- [ ] **Reserve the `SaintSystems.*`-style prefix or confirm `BusFire` ownership** on nuget.org
      before first publish.
- [ ] **Build-vs-buy sanity check.** If P0/P1 drift toward reimplementing an outbox + retry
      policy + sagas, evaluate a mature durable-mediator/outbox library before investing
      further — that exact pattern already exists, matured, off the shelf.

## Known dead code carried over from the fork

- `#define FIREBUS` toggle in `IBus.cs` (FireBus-native vs MediatR interfaces).
- Large blocks of commented-out MediatR code throughout — kept for now to preserve lineage;
  remove during the P1 API pass.
