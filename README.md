# AgentWorld

**AgentWorld** is a design-first open-source project about a persistent virtual
world inhabited by LLM-driven agents.

The inhabitants are not quest-givers waiting for a player. They live under
hardcoded survival laws, form relationships and families, gather resources,
own and exchange things, build settlements, and may eventually create new
systems for the world itself.
The long-term idea is a world that can become more complex because its
inhabitants choose to make it so.

> **Status: Phase 2 observation baseline.**
> The repository includes a small read-only browser debugger and a thin Godot
> observer for the deterministic seeded harness. Neither client can directly
> mutate authoritative state. The Godot observer is a proven protocol/rendering
> slice, not the final game UI or a packaged desktop release.

This repository deliberately captured the concept before implementation. The
first-world rules are now settled enough to build and test, and the C# solution
foundation establishes that boundary without claiming a finished kernel. New
design changes should come from prototype evidence or a deliberate decision,
not an endless questionnaire.

## The core idea

```text
world simulation
        ↕
authoritative world state
        ↕
LLM inhabitant runtime
        ↕
validated creation and modding system
```

A first world should begin with a small group of multiple, unrelated founders.
They may name themselves, have distinct personalities and aspirations, and
develop social roles through what they actually do. They must satisfy needs
such as hunger, health, rest, and shelter. Population grows through limited
family formation, not arbitrary agent spawning.

The world itself continues in a long-running process. It does not depend on a
cron job or a human issuing every action. Routine movement and survival can be
handled by deterministic simulation; an LLM is consulted at meaningful
decision points such as projects, discoveries, relationships, crises, and
creative proposals.

The human is an observer-director: the full map is visible without fog of war,
while inhabitants only know what they perceive, remember, or learn. The player
can inspect an inhabitant and send either a suggestive instruction or a must-do
instruction, as well as persistent world directives and direct broadcasts.
The world uses a configurable 365-day calendar with simple seasons and a
day/night cycle; it continues spending provider resources while unpaused.

## Design boundaries

- **Protected kernel:** time, space, health, needs, mortality, persistence,
  authority, validation, and sandbox boundaries cannot be removed by an
  inhabitant.
- **Emergent society:** jobs, customs, factions, economies, buildings, games,
  and institutions should arise from inhabitants and their circumstances.
- **Agent-created world:** inhabitants may propose new content and systems,
  but proposals are validated, simulated, versioned, and rolled back safely.
- **Design first:** the concept and invariants come before a large Godot
  project or multiplayer surface.
- **Multiplayer later:** the first target is one private persistent world, but
  the simulation should be authoritative and network-friendly from the start.

## Documentation

Start with the [documentation map](docs/README.md). The shortest path into the
project is:

1. [Vision](docs/concept/vision.md)
2. [Feature scope](docs/planning/feature-scope.md)
3. [Roadmap](docs/planning/roadmap.md)
4. [Decision register](docs/decisions/decision-register.md)

The implementation-level boundaries for the next phases live in the
[deterministic kernel contract](docs/planning/deterministic-kernel-contract.md),
[cognition and society contract](docs/planning/cognition-and-society-contract.md),
and [content-governance contract](docs/planning/content-governance-contract.md).

The [Phase 1 implementation ledger](docs/implementation/phase-1.md) records
which deterministic-kernel capabilities have executable evidence. GitHub owns
active work; the [document-authority guide](docs/governance/document-authority.md)
defines the boundary between design, delivery tracking, and proof.

Phase 1's headless core uses [C#/.NET 10](docs/planning/csharp-toolchain.md).
The browser viewer is deliberately a separate, non-authoritative diagnostic
project. The Godot client is separately non-authoritative and consumes the
same versioned observation boundary; it is the intended player-facing client.

## Browser observation baseline

Run the seeded-world debugger locally with:

    dotnet run --project src/AgentWorld.Viewer/AgentWorld.Viewer.csproj

It serves a dependency-free static browser UI and three read-only protocol
endpoints:

- `GET /api/v1/handshake`
- `GET /api/v1/world`
- `GET /api/v1/events?afterEventId=…`
- `GET /api/v1/reconnect?afterEventId=…`

By default the initial sample is the completed deterministic `camp-alpha`
harness. Set `AgentWorld__Runtime__AdvanceScript=true` when starting the host
to begin at genesis and advance its small scripted path at the configured
server clock. The viewer projects snapshots and events into its own DTOs; it
cannot mutate the simulation, issue actions, or advance ticks.

The decision register is the current authority; the
[design log](docs/decisions/design-log.md) is the chronological record of why
those decisions were made. The retired Batch 5 worksheet has been removed:
every one of its policy proposals was accepted or superseded.

## Godot observation baseline

`src/AgentWorld.GodotClient` is a deliberately plain 2D inspector. It reads
the handshake and atomic reconnect baseline, renders the map, actor, resources,
tick, and ordered event suffix, and holds the last good projection if a refresh
fails. It contains no action-submission route, client prediction, pause button,
or simulation reference.

The project defaults to a loopback world host. Developers can override that
endpoint with `--world-url=<https-url>` after the Godot command separator. A
future platform-specific package will make that configuration normal-user
friendly; users do not need the Godot editor for that eventual build.

Verify the pinned Godot engine and scene without installing it globally:

    bash scripts/verify-godot-client.sh

## What AgentWorld is not yet

It is not currently:

- a finished game
- a packaged Godot desktop client or final game UI
- an OpenClaw plugin
- an MMO or public server
- a free-form code execution environment for agents
- a claim that every interesting system has already been designed

Those may become future work. None should be smuggled into the foundation
without a clear design and testable boundary.

## Contributing

The most useful contributions during this phase are design critiques,
alternative models, small feasibility experiments, and precise questions. See
[CONTRIBUTING.md](CONTRIBUTING.md).

## Project name

The name **AgentWorld** is used for this project. The concept and design in
this repository stand on their own.

## License

AgentWorld is released under the [MIT License](LICENSE).
