# Contributing to BusFire

## Workflow

- Branch off `main` for each change — name it by type, e.g. `feat/recurring-overlap`, `fix/queue-precedence`, `docs/readme-badges`.
- Open a **pull request** to `main`. CI (`ci.yml`: build both target frameworks + tests + an 80% line-coverage gate) must pass.
- The **PR title must follow [Conventional Commits](https://www.conventionalcommits.org/)** — it drives the auto-generated release notes (the repo auto-labels each PR from its title, and `.github/release.yml` groups the notes by label).

## Conventional commit types

| Type | Use for | Release-notes section |
|------|---------|-----------------------|
| `feat:` | a new feature | 🚀 Features |
| `fix:` | a bug fix | 🐛 Fixes |
| `docs:` | docs only | 📝 Documentation |
| `refactor:` `test:` `chore:` `ci:` `build:` `perf:` `style:` | everything else | 🧹 Maintenance |

Breaking change: add `!` after the type (`feat!:`) or a `BREAKING CHANGE:` footer.

## Releasing

Releases are **tag-driven** — the package version comes from the git tag via MinVer (there is no `<Version>` in the csproj).

1. Make sure `main` is green.
2. `git tag vX.Y.Z && git push origin vX.Y.Z`.
3. `release.yml` runs the tests, packs, publishes to nuget.org via **Trusted Publishing** (OIDC — no stored API key), and creates a **GitHub Release** with auto-generated, categorized notes.

Choose `X.Y.Z` by SemVer: `feat` → minor, `fix`/`docs`/`chore` → patch, breaking → major. (Pre-1.0, a breaking change may ship in a minor.)
