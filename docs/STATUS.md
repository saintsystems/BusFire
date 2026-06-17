# BusFire — Status & Resume Notes

**Last updated:** 2026-06-16. Read this first when resuming; it's the handoff doc.

## TL;DR

BusFire is a Hangfire-backed command/event bus ("durable mediator") being extracted from
an internal `FireBus` fork into a standalone, MIT-licensed, publishable NuGet package
so another client (Billee) can reuse it. This repo is the **lift-and-shift baseline**: a faithful
rebrand that builds clean but still carries the original's known issues, which are tracked in
[`ROADMAP.md`](ROADMAP.md). The "why" behind the work is in [`DESIGN-REVIEW.md`](DESIGN-REVIEW.md).
Architecture/build/conventions are in [`../CLAUDE.md`](../CLAUDE.md).

## Done

- [x] Extracted 38 source files from the internal `FireBus` fork, excluding `obj`/`bin`/`.vs`/TFVC metadata.
- [x] Rebranded the internal `FireBus` fork → `BusFire` everywhere: namespace, identifiers
      (`AddBusFire`, `BusFireServiceConfiguration`, `BusFireGlobalConfiguration`, `UseBusFire`,
      `AddBusFireServer`, default conn-string name `"BusFire"`), and the two `FireBus*.cs` filenames.
- [x] Project scaffolding: `src/BusFire.csproj` (PackageId `BusFire`, MIT, SourceLink props, v`0.1.0`),
      `BusFire.sln`, `README.md`, `LICENSE`, `.gitignore`, `CLAUDE.md`, `docs/ROADMAP.md`, this file.
- [x] Removed the private NuGet feed and the embedded DevExpress key.
- [x] `dotnet build` clean: **0 errors** (10 cosmetic warnings — `CS8632` nullable annotations,
      `CS1998` async-without-await — carried over from the original).
- [x] `git init` on `main`; initial commit; working tree clean.

## Current state

- **Repo:** `D:\git\saintsystems\BusFire`, branch `main`, pushed to **`github.com/saintsystems/BusFire`** (public, MIT).
- **Published:** `BusFire` **0.1.0** is live on nuget.org (tag `v0.1.0`, via Trusted Publishing/OIDC — no stored secret).
- Build: `dotnet build BusFire.sln`. Pack: `dotnet pack src\BusFire.csproj -c Release`.

## Pending decisions / next actions

1. ~~**GitHub push (blocked on owner choice).**~~ **Done (2026-06-16):** created and pushed to the public
   `saintsystems/BusFire` repo; `csproj` `RepositoryUrl`/`PackageProjectUrl` already match.
2. **All four P0 roadmap items are done (2026-06-16)** — see `ROADMAP.md` P0 for detail:
   - Conditional `IShouldQueue` dispatch (inline by default, queue on opt-in; `Defer` always queues).
   - `TypeNameHandling.All` → `TypeNameHandling.None` + logical message-type registry (`MessageJsonConverter`, `[MessageName]`).
   - Per-handler event fan-out (one job per handler; isolated retries).
   - Dropped the serialized `CancellationToken` (Hangfire injects the live token).
3. **All P1 roadmap items are done (2026-06-16)** — see `ROADMAP.md` P1:
   - Storage decoupling: storage-agnostic `AddBusFire(cfg => ...)` + host-owned Hangfire via
     `config.UseBusFire(provider)`, plus an `AddBusFire(cfg, configureStorage)` convenience overload.
     The SQL-Server-specific path and `Hangfire.SqlServer` dependency were dropped (unblocks PostgreSQL).
   - Removed the static global config; restored `IQueueable : IShouldQueue` (read-only `Queue`/`Delay`).
   - Multi-target `netstandard2.0;net8.0`; added SourceLink + `.snupkg`.
   - Pinned the surface to fire-and-forget (removed `ICommand<TResponse>`).
   - **Deferred (additive):** a call-site dispatch override to make the message-based model a hybrid
     (see ROADMAP P1) — non-breaking, can land post-baseline.
4. **Tests done (2026-06-16):** `tests/BusFire.Tests` (xUnit, 61 tests, ~82% line coverage). Caught and
   fixed two latent bugs (exception-handler arg-count mismatch; missing `ServiceFactory` registration).
5. **CI done (2026-06-17):** `.github/workflows/ci.yml` (build + coverage-gated test + pack) and
   `release.yml` (publish to nuget.org on `v*` tag via **Trusted Publishing/OIDC — no stored secret**).
   Versioning = **MinVer** (tag-driven). To cut the first release, follow the **Publishing checklist** below.

## Publishing checklist (first nuget.org release)

Package ID is bare **`BusFire`** (see decision log). Publishing uses **nuget.org Trusted Publishing (OIDC)** —
no API key is stored as a GitHub secret.

1. ~~**Confirm `BusFire` is free.**~~ **Done (2026-06-17):** verified 404 at `nuget.org/packages/BusFire`.
2. ~~**Set up auth.**~~ **Done (2026-06-17):** created a **Trusted Publisher Policy** on nuget.org
   (account `saintsystems`) — Package Owner `saintsystems`, Repo Owner `saintsystems`, Repo `BusFire`,
   Workflow `release.yml`. `release.yml` authenticates via `NuGet/login@v1` (needs `id-token: write`); no
   `NUGET_API_KEY` secret. **Note:** a new policy is *pending* until first use within ~7 days — publish soon.
3. ~~**Sanity-check.**~~ **Done.**
4. ~~**Tag and push to release.**~~ **Done (2026-06-17):** tagged `v0.1.0` at `1b8263f`; `release.yml`
   tested, packed `BusFire.0.1.0`, logged in via OIDC, and pushed to nuget.org. Package is live (validation
   /indexing finishes within ~an hour of the push).
5. **TODO — request an ID prefix reservation** for `BusFire` (with the `BusFire.*`
   wildcard) via nuget.org → Manage Packages → Reserve ID Prefix (or contact support). Grants the
   verified-owner badge and protects the `BusFire.*` namespace from squatting. (Single-word prefix
   approval isn't guaranteed; if declined, the bare `BusFire` package is unaffected.)

## Decision log

- **Package ID = bare `BusFire`** (matches the root namespace and the brand; ID↔namespace match is the
  NuGet convention). `SaintSystems.BusFire` (under a reservable `SaintSystems.*` prefix) was the alternative
  — chosen against because it mismatches the `BusFire` namespace. Plan: ship `BusFire`, then request a
  `BusFire`/`BusFire.*` prefix reservation for the verified badge + namespace protection.

- **Name = `BusFire`** (bare, playful). `FireBus`/`Firebus` is **taken** on nuget.org (owner `variel`,
  same problem domain, dormant since 2021-05) so it can't be published; `SaintSystems.FireBus` was the
  namespaced fallback. `BusFire` confirmed FREE on nuget.org. Mild "bus on fire" connotation accepted.
- **Location:** `D:\git\saintsystems\BusFire` (sibling to `kwik`). **License:** MIT.
- **Approach:** lift-and-shift first (build green under new identity), then refactor per ROADMAP.
- **Identifier rename done now** (not deferred) because changing public API names post-publish is breaking.

## Reference: where the originals live (read-only, for comparison)

- Original fork being extracted: the internal `FireBus` source tree (local, read-only).
- Upstream ancestor `Kwik.Bus`: `D:\git\saintsystems\kwik\src\Bus` (+ contracts in `..\Contracts`)
