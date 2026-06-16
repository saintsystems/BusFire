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
- [ ] **Per-handler event jobs / idempotency story.** Today a single failing event handler
      re-runs all handlers on retry. Fan out one job per handler, or document the
      idempotency requirement prominently (it's in the README; needs enforcement options).
- [ ] **Drop the serialized `CancellationToken`.** It's meaningless on the consumer side;
      rely on Hangfire's injected job-cancellation token.

## P1 — packaging & API for third parties

- [ ] **Decouple from storage / Hangfire bootstrapping.** `AddBusFire` currently calls
      `AddHangfire(...UseSqlServerStorage...)` itself, which collides with hosts that already
      configure Hangfire. Invert it: let the consumer own Hangfire + storage; BusFire only
      registers handlers, the bridge, and serializer settings. Unlocks non-SQL-Server storage.
- [ ] **Remove the static global config** (`BusFireGlobalConfiguration.Configuration`).
      Process-wide mutable state breaks test isolation and multiple-instance scenarios.
- [ ] **Restore `IQueueable`** (self-describing queue name + delay, with `OnQueue`/`WithDelay`),
      ported from Kwik.Bus, as an alternative to per-call `queue`/delay arguments.
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
