---
title: Phase 1 C# Toolchain
type: implementation-policy
status: active
updated: 2026-09-19
---

# Phase 1 C# Toolchain

Phase 1 uses **C# 14 on .NET 10 LTS** for the authoritative, headless
simulation kernel. The exact SDK baseline is `10.0.401`, selected by
[`global.json`](../../global.json); patch updates within that feature band are
permitted. .NET 10 is an LTS release supported through November 2028.

This choice concerns the world brain, not the player-facing renderer. Godot is
still a later viewer candidate. If it remains the best fit, it can share C#
with the core, but it must never become a dependency of the authoritative
simulation project.

## Project boundary

```text
src/AgentWorld.Simulation/        pure authoritative simulation library
tests/AgentWorld.Simulation.Tests/ deterministic and replay test suite
future viewer/                    separate project; no authority over state
```

`AgentWorld.Simulation` must remain runnable and testable without Godot, a
window manager, an LLM provider, or a network connection after dependencies are
restored. Any future client asks the simulation to validate and commit an
action; it does not mutate world state directly.

## Test and dependency policy

- **xUnit** is the Phase 1 test framework.
- `Microsoft.NET.Test.Sdk` runs tests through `dotnet test`.
- NuGet lock files are committed for every project with external packages.
  Routine verification uses `--locked-mode`, so dependency changes are
  deliberate reviewable diffs rather than surprise downloads.
- Shared compiler, nullability, warning, analyzer, deterministic-build, and
  prerelease-version settings live in
  [`Directory.Build.props`](../../Directory.Build.props). Its `VersionPrefix`
  and `VersionSuffix` are the single source for future .NET package/runtime
  metadata; do not add a hand-maintained version constant.

## Reproducibility proof

On a fresh clone with the selected SDK:

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet test --configuration Release --no-restore
```

GitHub Actions runs those same commands for pushes and pull requests. The
scaffold proves the language and project boundary only. It does **not** claim
that the simulation kernel, persistence, map, actor, or Phase 1 acceptance
fixture exists yet.
