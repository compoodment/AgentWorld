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
  ├── inventory and transaction ledger
  ├── persistence and event log
  ├── action, asset, and mod validators
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
- the in-world calendar, day/night cycle, seasons, and pause state
- world state and random seeds
- inhabitant state
- resource and action validation
- inventory ownership and atomic transaction settlement
- installed mods and policy
- persistence and recovery
- observation data sent to clients
- human directives, broadcasts, and paused authoring-mode edits

Clients render observations and submit requests. They do not decide whether an
action happened. This is useful for integrity even before multiplayer exists.

## Simulation loop

The loop should be deterministic where practical:

```text
advance fixed world clock
  → update kernel needs and environment
  → advance routine actions
  → resolve collisions, production, consumption, and transactions
  → deliver due events
  → enqueue cognition requests
  → apply validated decisions
  → emit events and checkpoint
```

LLM responses should enter through a queue and be validated like any other
external input. A slow or failed response must not pause the whole world or
apply half an action.

The first normal clock is intentionally simple: 365 in-world days per year,
four seasons, about 2 minutes 40 seconds of daylight and 1 minute 20 seconds of
night per in-world day. Pausing stops the world. Sleep changes an inhabitant's
state and cognition eligibility; it does not require the whole simulation to
stop. The timing is a world configuration, not a permanent balance law, so it
can be changed after playtesting without changing the calendar model.

The simulation does not make a model call for every tick, tile movement, or
small need change. Cognition is event-driven and receives a compact observation
when an inhabitant needs to choose or revise an intention. The exact triggers,
perception contract, and movement model remain open design work.

## Observation and knowledge boundary

The human client receives the complete world view by default; there is no fog
of war. An inhabitant receives an epistemically bounded observation assembled
from its current senses, location, communication, durable memories, and known
map facts. Spatial knowledge can include visited tiles, remembered objects such
as a bed or resource node, landmarks, and learned routes. The authoritative
world map remains separate from that personal knowledge so an inhabitant can be
wrong, forget, or lack information without corrupting reality.

## Persistence and replay

The current direction is:

- periodic complete snapshots
- an append-only event log between snapshots
- explicit schema and migration versions
- deterministic world seed and simulation version
- mod manifest included in saves
- asset manifest and economic ledger included in saves
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
- provider/account limits configured by the human, plus optional game-level
  per-world and per-agent cognition limits
- model/provider configuration rather than hardcoded identity
- timeouts, retries, and malformed-output handling
- local mock agents and deterministic scripted scenarios
- a best-effort game-side stop before configured estimates are exhausted, while
  never pretending the game can control billing limits it does not own

The simulation should be able to keep running with agents asleep, paused, or
using local fallback behaviour when the model provider is unavailable.

For hosted providers, the provider account remains the authoritative billing
boundary. For local providers such as Ollama, the game can cap calls, tokens,
concurrency, or other local resource use even though there may be no API bill.
The exact budget and fallback contract is still a Batch 2 design question.

## VPS target

The initial target is a small VPS-hosted private world, not an MMO. A headless
2D simulation, persistence, a few inhabitants, and a small number of viewers
should be realistic. Hundreds of active LLM agents, high-frequency cognition,
and a giant public world are explicitly outside the first capacity target.
