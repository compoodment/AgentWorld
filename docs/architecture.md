---
title: Architecture Direction
type: design
status: proposal
updated: 2026-09-18
---

# Architecture Direction

This is a proposed foundation, not an implementation specification. Its job is
to identify boundaries and invariants before code makes them expensive to
change.

## Proposed components

```text
Godot client / viewer
          ↕ protocol
authoritative world server
  ├── deterministic simulation core
  ├── persistence and event log
  ├── action and mod validators
  └── observation API
          ↕ queued decisions
LLM inhabitant worker
```

The first private world may run the server and worker on the same VPS. They
should remain separate logical components so model failures, API credentials,
and expensive cognition cannot directly corrupt simulation state.

## Engine direction

Godot is currently the leading client/rendering candidate because it provides a
strong 2D and pixel-art workflow, tile-based rendering, animation, and a path
toward a headless server and later multiplayer. A logical tile is a world cell,
not one physical screen pixel, so a 256×256 logical map can use 16×16 or
32×32-pixel artwork per cell.

Godot is not yet locked as the simulation engine. Before committing, we should
test whether the simulation core, persistence, headless operation, and
networking boundaries remain clean. A separate simulation service with Godot as
a client is also possible.

## Server authority

The server owns:

- time and tick ordering
- world state and random seeds
- inhabitant state
- resource and action validation
- installed mods and policy
- persistence and recovery
- observation data sent to clients

Clients render observations and submit requests. They do not decide whether an
action happened. This is useful for integrity even before multiplayer exists.

## Simulation loop

The loop should be deterministic where practical:

```text
advance clock
  → update kernel needs and environment
  → advance routine actions
  → resolve collisions and resource effects
  → deliver due events
  → enqueue cognition requests
  → apply validated decisions
  → emit events and checkpoint
```

LLM responses should enter through a queue and be validated like any other
external input. A slow or failed response must not pause the whole world or
apply half an action.

## Persistence and replay

The current direction is:

- periodic complete snapshots
- an append-only event log between snapshots
- explicit schema and migration versions
- deterministic world seed and simulation version
- mod manifest included in saves
- crash-safe checkpointing
- a replay or reproduction mode for tests and bug reports

SQLite is a plausible first storage engine, but it is not yet a requirement.
The important invariant is that a world can be inspected and recovered without
depending on a live LLM provider.

## World scale

The first visual prototype may use a modest map, but the conceptual world
should not be permanently constrained to a tiny grid. The likely design is:

- logical world divided into chunks
- only nearby or active regions rendered at full detail
- local simulation for important active areas
- lower-frequency or abstract simulation for distant regions
- explicit expansion rules rather than an unbounded coordinate accident

The final map model depends on what kinds of ecology, travel, construction, and
dimensions the concept settles on.

## Cost and failure boundaries

LLM usage is the expensive and least deterministic part. The runtime needs:

- compact observations and summaries
- event-triggered calls instead of tick-triggered calls
- per-world and per-agent budgets
- model/provider configuration rather than hardcoded identity
- timeouts, retries, and malformed-output handling
- local mock agents and deterministic scripted scenarios
- a hard emergency stop before credits are exhausted

The simulation should be able to keep running with agents asleep, paused, or
using local fallback behaviour when the model provider is unavailable.

## VPS target

The initial target is a small VPS-hosted private world, not an MMO. A headless
2D simulation, persistence, a few inhabitants, and a small number of viewers
should be realistic. Hundreds of active LLM agents, high-frequency cognition,
and a giant public world are explicitly outside the first capacity target.
