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

Private-world ticks execute against an isolated proposed state. Provider I/O
does not hold the authoritative world gate; snapshots and owner controls see
the last committed tick. Commit checks the unchanged world/event generation.
Cancellation discards the proposal, and a concurrent owner change supersedes
it. A separate tick semaphore prevents competing tick commits.

## Engine direction

Godot is the player-facing client/rendering engine because it provides a
strong 2D and pixel-art workflow, tile-based rendering, animation, and a path
toward a headless private host. Multiplayer is excluded. A logical tile is a world cell,
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
action happened. This preserves authority in the single-player hosted world.

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

Private-world schema 5 persists bounded public settlement projects (goal,
phase, work, blocker and job), not hidden model reasoning. The built-in
settlement supplement may add stone/fiber/seed nodes to free passable cells;
restore verifies the original generator output after removing only those
registered additions. Existing paused checkpoints are not migrated on load.
See the [schema policy](versioning-and-releases.md#private-world-save-schema-5).

The integrated host atomically replaces its checkpoint after committed ticks.
World, society, inventory, scheduler and individual cognition event histories
compact from more than 2,048 entries to the latest 1,024 entries on save. Global
event IDs and explicit retention floors survive compaction and restart.

Older events are written first into immutable SHA-256-named segments beside the
save, in `<save>.history/`. Each segment links its predecessor; the checkpoint
stores only the head digest. A segment is flushed before the checkpoint that
references it is replaced. A crash before checkpoint replacement may leave an
unreferenced segment; it cannot make the last committed checkpoint depend on
an unwritten segment. Restart verifies the complete referenced hash chain.
Archives are private (0700 directory, 0600 files); never delete referenced
segments. Backups and restores must copy the save and its history together.

Reconnect returns a recent contiguous suffix. If the requested cursor predates
`eventHistoryFloor`, `resetRequired` explicitly requests a fresh snapshot and
recent log; the Godot client validates the floor, clears its held log, and
resumes at the new cursor. Client log retention is also bounded to 2,048 events.
Older clients without this reset support must be upgraded before reconnecting
to a compacted world. Offline archives are operator evidence, not an unbounded
payload sent to the client.

The save retains current world state, deterministic seeds, schema versions,
content locks, asset reservations and economic state. Compaction bounds event
history cost, not the size of genuinely growing world entities. No live model
provider is required to load or inspect a checkpoint.

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
