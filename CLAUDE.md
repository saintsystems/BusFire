# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Resuming work here

Start with [`docs/STATUS.md`](docs/STATUS.md) — current state, decision log, and next actions (it's
the session-handoff doc). Then [`docs/ROADMAP.md`](docs/ROADMAP.md) for prioritized work and
[`docs/DESIGN-REVIEW.md`](docs/DESIGN-REVIEW.md) for the architectural rationale and the
`Kwik.Bus`↔`FireBus` comparison. This repo is a lift-and-shift baseline; several known issues are
intentionally still present and tracked in those docs — don't treat them as new discoveries.

## What this is

`BusFire` is a NuGet library (`netstandard2.0`, PackageId `BusFire`, currently `0.1.0`, **not yet published**): a MediatR-style command/event dispatcher whose transport is Hangfire. Commands/events are serialized and enqueued as Hangfire background jobs (persisted to SQL Server), then dispatched to handlers by a Hangfire server — a "durable mediator."

It was extracted from an internal `FireBus` fork (itself a fork of `Kwik.Bus`), which inlines a stripped-down MediatR plus Hangfire as the transport. Large blocks of commented-out MediatR code and a `#define FIREBUS` toggle (`IBus.cs`) remain — they document lineage; leave them unless the roadmap calls for removal.

**Before doing architectural work, read [`docs/ROADMAP.md`](docs/ROADMAP.md).** It records the design intent (Laravel `ShouldQueue` conditional dispatch) and the prioritized refactors from the extraction review. This repo is a lift-and-shift baseline; some known issues (e.g. the static global config) are intentionally still present and tracked there — don't treat them as discoveries.

## Build & test

```powershell
dotnet build BusFire.sln              # build the library
dotnet pack src\BusFire.csproj -c Release   # produce the NuGet package
```

The package **version is derived from git tags by MinVer** (tag-driven SemVer) — there's no `<Version>`
in the csproj. Tag `v0.1.0` → package `0.1.0`; untagged builds get a pre-release (e.g. `0.0.0-alpha.0.N`).
CI/release run from `.github/workflows/` (`ci.yml`, `release.yml`); a `v*` tag publishes to nuget.org via
**Trusted Publishing (OIDC)** — no stored secret. To cut the first release, follow the **Publishing checklist** in
[`docs/STATUS.md`](docs/STATUS.md#publishing-checklist-first-nugetorg-release) (claim the `BusFire` ID, add
the secret, tag, then reserve the ID prefix).

Tests live in `tests/BusFire.Tests` (xUnit). Run with coverage:

```powershell
dotnet test tests\BusFire.Tests -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura -p:Include="[BusFire]*"
```

~82% line coverage. The Hangfire runtime shims (`BusFireActivator`/`NotifyOnFailureAttribute`) are
`[ExcludeFromCodeCoverage]` (integration-only). CI is still a P2 item.

## Architecture: two-stage dispatch

Read `Bus.cs`, `Infrastructure/HangfireBridge.cs`, and `BusInternal.cs` together:

1. **Producer — `IBus`/`Bus`:** `Send`/`Publish` run handlers **inline** via `IBusInternal` unless the message implements `IShouldQueue`, in which case they enqueue a Hangfire job targeting `HangfireBridge`. `IQueueable : IShouldQueue` adds self-declared `Queue`/`Delay` (read-only getters); queue precedence is per-call arg › `IQueueable.Queue` › `"default"`, and a non-null `IQueueable.Delay` schedules instead of enqueues. `Defer` always enqueues (`Schedule`) — a delayed message can't run inline "now".
2. **Transport:** Hangfire — **storage is the host's choice** (PostgreSQL, SQL Server, Redis…); the host owns `AddHangfire` and calls `config.UseBusFire(provider)`, or uses the `AddBusFire(cfg, configureStorage)` overload that takes a storage delegate. Serialization uses Newtonsoft with `TypeNameHandling.None` plus a logical message-type registry: `MessageJsonConverter` writes a stable `__busfire_type` name (default `Type.FullName`, overridable with `[MessageName]`) resolved via `IMessageTypeRegistry`/`MessageTypeRegistry`, built from the scanned assemblies in `AddBusFire`. No assembly-qualified `$type` is persisted (closes the RCE vector; rename-safe). The bridge routes by the queue argument: `[Queue("{2}")]` on `Send`/`Publish`, `[Queue("{3}")]` on `RunEventHandler`.
3. **Consumer — `HangfireBridge` → `IBusInternal`/`BusInternal`:** resolves handlers (reflection-built wrappers cached in static `ConcurrentDictionary`s), runs pipeline behaviors, executes in a fresh DI scope.

## Registration (entry points)

`Infrastructure/ServiceCollectionExtensions.cs`:

- **`AddBusFire(cfg => cfg.RegisterServicesFromAssemblies(...))`** (storage-agnostic, primary) — on every app that produces or consumes. Scans assemblies for handlers/behaviors (`ServiceRegistrar.cs`), builds the `IMessageTypeRegistry`, registers `IBus`/`ISender`/`IPublisher` and the failure filter. Does **not** touch Hangfire — the host owns `AddHangfire`/storage and must call `config.UseBusFire(provider)` inside it (applies serializer settings + filter). Throws if no assemblies supplied.
- **`AddBusFire(cfg => ..., hangfire => hangfire.UseXxxStorage(...))`** — convenience overload: calls the storage-agnostic overload, then owns the `AddHangfire` call (`configureStorage(config)` + `UseBusFire(provider)`). Host supplies storage only; no SQL hardcoding. Don't also call `AddHangfire` separately.
- **`AddBusFireServer()`** — only on apps that should process jobs (`AddHangfireServer`); queues come from `BusOptions.Queues`.

`BusFireServiceConfiguration` (`Infrastructure/BusFireServiceConfiguration.cs`) is the `cfg` builder: handler assemblies, `IEventPublisher`, `IFailureHandler`, lifetime, behaviors, exception strategy. A fresh instance is created per `AddBusFire` call (the old `BusFireGlobalConfiguration` static was removed).

## Handlers & pipeline

Contracts live in `IBus.cs`, guarded by `#define FIREBUS` (default = BusFire's own marker interfaces; `#else` = MediatR-based). `ICommandHandler<TCommand>` (one per command), `IEventHandler<TEvent>` (many; published via `IEventPublisher`, default `ForEachAwaitPublisher`). Behaviors wrap command handling in `Wrappers/CommandHandlerWrapper.cs` by reverse-aggregating `IPipelineBehavior<TCommand>`. `*Behavior` classes register only when an implementation exists (`RegisterBehaviorIfImplementationsExist`).

## Failure handling

`NotifyOnFailureAttribute` (a global Hangfire `JobFilterAttribute`) forwards a job's id + exception to the configured `IFailureHandler` when it enters the failed state. Default is the no-op `NullFailureHandler`; override via `BusFireServiceConfiguration.FailureHandler`.

## Conventions

- Keep the public API under the `BusFire` brand — no `FireBus`/`Kwik` names in new code.
- **Message ≠ handler stays separate** (data DTO vs behavior) — that's what keeps the wire payload data-only and enables event fan-out + pipeline behaviors. The recommended way to organize them is the **nested-container "Job" convention**: a `public static class SendWelcomeEmail` holding a nested `record Command : ICommand` and `class Handler : ICommandHandler<Command>`. Co-location without merging; assembly scanning finds nested handlers (the tests rely on exactly this). Don't merge data+behavior into one type — it would put deps on the wire. See the README "Job convention" section.
- Source control is git; the upstream is intended to be `github.com/saintsystems/BusFire` (confirm before pushing/publishing).
