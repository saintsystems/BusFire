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

- **Repo:** `D:\git\saintsystems\BusFire`, branch `main`, local only.
- **NOT pushed** to GitHub and **NOT published** to nuget.org.
- Build: `dotnet build BusFire.sln`. Pack: `dotnet pack src\BusFire.csproj -c Release`.

## Pending decisions / next actions

1. **GitHub push (blocked on owner choice).** `csproj` assumes `github.com/saintsystems/BusFire`.
   Once the owner is confirmed (`saintsystems` org vs personal), create + push:
   `gh repo create <owner>/BusFire --public --source . --remote origin --push`
   (and fix the `RepositoryUrl`/`PackageProjectUrl` in `src/BusFire.csproj` if the owner differs).
2. **P0 roadmap items** — most impactful before a client depends on it:
   - ~~Restore Laravel-style `IShouldQueue` conditional dispatch (inline by default, queue on opt-in).~~
     **Done (2026-06-16):** `Bus` runs `Send`/`Publish` inline via `IBusInternal` unless the message
     implements `IShouldQueue`; `Defer` always queues. See `ROADMAP.md` P0.
   - ~~Replace `TypeNameHandling.All` (RCE + versioning risk).~~ **Done (2026-06-16):** `TypeNameHandling.None`
     + logical message-type registry (`MessageJsonConverter`, `[MessageName]`). See `ROADMAP.md` P0.
3. **Smoke-test harness + CI.** The original `TestHarness` referenced `Kwik.Bus`, not FireBus, so it
   did not transfer — a fresh minimal harness is needed. Add GitHub Actions for build/test/pack.

## Decision log

- **Name = `BusFire`** (bare, playful). `FireBus`/`Firebus` is **taken** on nuget.org (owner `variel`,
  same problem domain, dormant since 2021-05) so it can't be published; `SaintSystems.FireBus` was the
  namespaced fallback. `BusFire` confirmed FREE on nuget.org. Mild "bus on fire" connotation accepted.
- **Location:** `D:\git\saintsystems\BusFire` (sibling to `kwik`). **License:** MIT.
- **Approach:** lift-and-shift first (build green under new identity), then refactor per ROADMAP.
- **Identifier rename done now** (not deferred) because changing public API names post-publish is breaking.

## Reference: where the originals live (read-only, for comparison)

- Original fork being extracted: the internal `FireBus` source tree (local, read-only).
- Upstream ancestor `Kwik.Bus`: `D:\git\saintsystems\kwik\src\Bus` (+ contracts in `..\Contracts`)
