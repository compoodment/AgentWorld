---
title: Build and Test the Current Prototype
type: development-reference
status: active
updated: 2026-09-26
---

# Build and Test the Current Prototype

The current prototype uses **C# 14 on .NET 10 LTS** for the authoritative, headless
simulation kernel. The exact SDK baseline is `10.0.401`, selected by
[`global.json`](../global.json); patch updates within that feature band are
permitted. .NET 10 is an LTS release supported through November 2028.

This choice concerns the world brain, not the player-facing renderer. Godot is
the intended player-facing client and may share C# with the core, but it must
never become a dependency of the authoritative simulation project. The first
Godot observer uses Godot 4.7.2's .NET project SDK and targets `net8.0`, the
desktop script target supported by that engine. That target is intentionally
local to the client; the authoritative projects remain on .NET 10.

The first end-user export target is **Windows 11 x64**. The repository now has
an unsigned portable-export path and a CI artifact configuration for that
target; the Godot editor remains a development-only tool. This is not an
installer choice, code-signing provider, or public release. The separate
Windows smoke test and paired reconnect were completed on 2026-09-21.

The export check proves that a reproducible bundle is produced, not that a
person can use it on Windows; it does not substitute for the completed Windows
11 x64 smoke test or for final product playtesting. The tester needs the
portable bundle and Tailnet reachability, not the Godot editor.

## Project boundary

```text
src/AgentWorld.Simulation/        pure authoritative simulation library
src/AgentWorld.Viewer/            separate ASP.NET Core observation host
src/AgentWorld.GodotClient/       separate Godot paired-owner projection/request client
tests/AgentWorld.Simulation.Tests/ deterministic, replay, and viewer-contract tests
```

`AgentWorld.Simulation` must remain runnable and testable without Godot, a
window manager, an LLM provider, or a network connection after dependencies are
restored. The headless HTTP host references the simulation, never the reverse,
and projects its own protocol DTOs. The Godot client does not reference the
simulation; it uses the headless host's versioned paired-owner HTTP contract.
It signs requests with a device key, but every observation, pause/resume,
instruction, and authoring result still comes from server validation and commit.
No client mutates world state directly.

The Windows client uses a non-exportable current-user CNG P-256 device key for
normal pairing. Its saved registration contains only non-secret metadata; the
private key is never bundled into an export or written into a world save. The
full bootstrap, revocation, and transport policy lives in
[current private-host device pairing](pairing.md).

## Test and dependency policy

- **xUnit** is the current test framework.
- `Microsoft.NET.Test.Sdk` runs tests through `dotnet test`.
- NuGet lock files are committed for every project with external packages.
  Routine verification uses `--locked-mode`, so dependency changes are
  deliberate reviewable diffs rather than surprise downloads.
- Shared compiler, nullability, warning, analyzer, deterministic-build, and
  prerelease-version settings live in
  [`Directory.Build.props`](../Directory.Build.props). Its `VersionPrefix`
  and `VersionSuffix` are the single source for future .NET package/runtime
  metadata; do not add a hand-maintained version constant.

## Reproducibility proof

On a fresh clone with the selected SDK:

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet test --configuration Release --no-restore
bash scripts/verify-godot-client.sh
bash scripts/verify-godot-windows-export.sh
```

`verify-godot-client.sh` downloads the exact Godot 4.7.2 .NET engine archive,
checks its SHA-256, builds the C# scripts, and starts the scene headlessly.
`verify-godot-windows-export.sh` separately verifies the pinned editor and
export-template archives, emits an unsigned Windows x64 PE bundle, and writes a
SHA-256 manifest. It proves export reproducibility on the build host; it does
not substitute for running the bundle on Windows 11. GitHub Actions is
configured to run these checks and upload the Windows bundle as an artifact for
pushes and pull requests using the pinned .NET 10 SDK/runtime host.
