---
title: Current Architecture
type: architecture
status: active
updated: 2026-09-22
---

# Current architecture

This document describes the implemented high-level boundary and the direction
that new work must preserve. It is not the canonical feature-status report;
see [current state](../status/current-state.md) for what is playable versus
thin or fixture-only.

The concrete deterministic, cognition, and content-governance semantics are in
the linked [implementation contracts](../README.md#contracts-and-policy).

## Components

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

The private world runs the server and provider adapters on the same VPS. They
remain separate logical components so model failures, API credentials, and
expensive cognition cannot directly corrupt simulation state.

## Engine direction

Godot is the player-facing client/rendering engine because it provides a
strong 2D and pixel-art workflow, tile-based rendering, animation, and a path
toward a headless server and later multiplayer. A logical tile is a world cell,
not one physical screen pixel, so a 256×256 logical map can use 16×16 or
32×32-pixel artwork per cell.

Godot is not the simulation engine. The separate headless simulation service
remains authoritative, while Godot consumes its versioned protocol as a client.
The live host, reconnect behavior, and first Godot adapter are proven; the
rendering details remain prototype-grade until later play evidence justifies
production assets.

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
external input. A slow or malformed individual response must not corrupt the
world or apply half an action. A prolonged individual failure can fall back to
simple local behaviour; a provider-level outage pauses the game and notifies
the human.

The first normal clock is intentionally simple: 365 in-world days per year,
four seasons, about 2 minutes 40 seconds of daylight and 1 minute 20 seconds of
night per in-world day. Pausing stops the world. Sleep changes an inhabitant's
state and cognition eligibility; it does not require the whole simulation to
stop. The timing is a world configuration, not a permanent balance law, so it
can be changed after playtesting without changing the calendar model.

The simulation does not make a model call for every tick, tile movement, or
small need change. Cognition is event-driven and receives a compact observation
when an inhabitant needs to choose or revise an intention. All meaningful event
categories may trigger cognition, but repeated small events are coalesced into
one observation so a burst of hunger, messages, and route failures does not
become a burst of model calls.

Movement is logically tile-backed, while the client may animate movement
smoothly between tiles. The inhabitant's LLM chooses a destination and broad
travel intention; the simulation calculates and validates the route with
deterministic grid pathfinding. The first implementation should use A*-style
tile routing with reusable route caches. Roads lower movement cost and are
preferred when they are faster; route caches are invalidated when relevant
terrain, roads, or obstacles change. Terrain, roads, health, and transport
affect travel time. A blocked or dangerous route produces an event that can
cause the inhabitant to reconsider at the next action boundary.

## Observation and knowledge boundary

The human client receives the complete world view by default; there is no fog
of war. An inhabitant receives an epistemically bounded observation assembled
from its current senses, location, communication, durable memories, and known
map facts. The current tile is always known, including its material, occupants,
objects, and relevant effects. Local observations include nearby visible
surroundings, messages, changes to known locations, danger, urgent needs, the
inhabitant's own body and task state, time/season, nearby weather, and direct
interactions. Spatial knowledge can include remembered objects such as a bed or
resource node, landmarks, destinations, and lightweight route memories. The
authoritative world map remains separate from that personal knowledge so an
inhabitant can be wrong, forget, or lack information without corrupting reality.
Developer tooling may additionally expose and persist the full structured
decision record—observation fields, retrieved memories, event triggers, model
output, selected action, and fallback reason—without treating hidden
chain-of-thought as authoritative state.

## Persistence and replay

The persistence model is:

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
- provider/account limits configured by the human remain the external billing
  boundary
- contract-defined runtime controls: owner pause, queue limits and backpressure,
  provider-wide outage pause, emergency stop, and deterministic fallback
- model/provider configuration rather than hardcoded identity
- timeouts, retries, and malformed-output handling
- local mock agents and deterministic scripted scenarios
- explicit provider/account status and a safe stop path, while never pretending
  the game can control billing limits it does not own

The simulation should be able to keep running with agents asleep or using
deterministic fallback behaviour after an individual model failure. If the
provider itself is unavailable, the game pauses and asks the human to resolve
it.

For hosted providers, including Ollama Cloud, the provider account remains the
authoritative billing boundary. The project does not currently target local
model inference. The initial prototype does not promise fixed game-side call or
token ceilings; Phase 3 measures usage and queue pressure before deciding
whether any numeric ceiling is needed.

An individual failed cognition request uses bounded retry and deterministic
fallback. A confirmed provider-wide outage enters the full-world paused state at
the next atomic boundary, cancels/invalidates pending model work, and notifies
the human. It does not quietly keep the rest of the simulation advancing; see
the [deterministic kernel contract](deterministic-kernel-contract.md).

## VPS target

The initial target is a small VPS-hosted private world, not an MMO. A headless
2D simulation, persistence, a few inhabitants, and a small number of viewers
should be realistic. Hundreds of active LLM agents, high-frequency cognition,
and a giant public world are explicitly outside the first capacity target.
