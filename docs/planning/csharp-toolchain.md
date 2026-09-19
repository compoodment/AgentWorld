---
title: C# and Godot Toolchain
type: implementation-policy
status: active
updated: 2026-09-19
---

# C# and Godot Toolchain

Phase 1 uses **C# 14 on .NET 10 LTS** for the authoritative, headless
simulation kernel. The exact SDK baseline is `10.0.401`, selected by
[`global.json`](../../global.json); patch updates within that feature band are
permitted. .NET 10 is an LTS release supported through November 2028.

This choice concerns the world brain, not the player-facing renderer. Godot is
the intended player-facing client and may share C# with the core, but it must
never become a dependency of the authoritative simulation project. The first
Godot observer uses Godot 4.7.2's .NET project SDK and targets `net8.0`, the
desktop runtime line loaded by that Godot engine. That target is intentionally
local to the client; the authoritative projects remain on .NET 10.

## Project boundary

```text
src/AgentWorld.Simulation/        pure authoritative simulation library
src/AgentWorld.Viewer/            separate ASP.NET Core observation host
src/AgentWorld.GodotClient/       separate Godot read-only projection client
tests/AgentWorld.Simulation.Tests/ deterministic, replay, and viewer-contract tests
```

`AgentWorld.Simulation` must remain runnable and testable without Godot, a
window manager, an LLM provider, or a network connection after dependencies are
restored. The browser viewer references the simulation, never the reverse, and
projects its own read-only DTOs. The Godot client does not reference the
simulation; it uses the browser host's versioned HTTP projection contract. Any
future client asks the server to validate and commit an action; it does not
mutate world state directly.

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
bash scripts/verify-godot-client.sh
```

The final command downloads the exact Godot 4.7.2 .NET engine archive, checks
its SHA-256, builds the C# scripts, and starts the scene headlessly. GitHub
Actions runs the same checks for pushes and pull requests and provisions the
.NET 8 runtime alongside the pinned .NET 10 SDK.
