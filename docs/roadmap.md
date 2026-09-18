---
title: Design and Implementation Roadmap
type: roadmap
status: draft
updated: 2026-09-19
---

# Roadmap

This roadmap is intentionally gated. The project is still in the concept
phase; later phases are not commitments to build every feature.

## Phase 0 — Concept and foundation design (current)

- define the experience and design pillars
- decide which laws belong to the protected kernel
- define inhabitants, needs, family growth, and population dynamics
- define inventories, ownership, exchange, and the no-forged-state rule
- define the asset proposal and normalization pipeline
- define the content/mod proposal boundary
- define observer-director control, instructions, world directives, and paused
  authoring mode
- define the calendar, day/night timing, seasons, tile properties, ecosystem
  objects, and the human/inhabitant knowledge boundary
- classify first-world features and explicit deferrals in [feature scope](feature-scope.md)
- compare Godot, a separate simulation service, and storage options
- write failure modes and invariants before implementation
- keep unresolved decisions visible in [open questions](open-questions.md)

**Gate:** the first-world concept is coherent enough that implementation can
be judged against it, while remaining open to deliberate change.

## Phase 1 — Deterministic simulation kernel

- world time and logical coordinates
- 365-day calendar, simple seasons, day/night, and pause state
- terrain and resource primitives
- seeded temperate terrain and ecosystem object fixtures
- tile-backed movement, route validation, and travel effects
- health, hunger, rest, shelter, fatigue slowdown, exhaustion damage, and
  mortality rules
- inventories, ownership, access, production, consumption, and atomic transfer
- action validation and atomic state transitions
- one scripted inhabitant or test actor
- human instruction and event-history fixtures
- snapshots, event log, migrations, and replay fixtures

**Gate:** the world remains correct and reproducible without any LLM or visual
client.

## Phase 2 — World viewer and observation boundary

- choose the rendering/client engine
- render a seeded world and active entities
- inspect the complete world, selected inhabitants, summarized decision factors,
  spatial knowledge, and event history
- show current-tile knowledge, local perception, destinations, and active routes
- expose suggestive/must-do instructions and the full paused authoring surface:
  terrain, water, resources, plants, buildings, inhabitants, weather/seasons,
  and checked human assets
- define the observation and action protocol
- run the server headlessly on a development machine or VPS

**Gate:** a human can understand what happened, and a client cannot directly
mutate authoritative state.

## Phase 3 — One LLM inhabitant

- add a configurable model/provider adapter
- event-driven cognition queue
- structured intentions and actions
- destination-level travel intentions with server-side route execution
- compact observations and durable memory prototype
- bounded observations including spatial memory
- coalesced event triggers and urgency-based fallback behaviour
- provider/model selection and later changes, individual-failure local
  fallback, provider-outage pause and notification, and provider status
- defer game-side cognition ceilings until measurement shows they are needed
- measure cost and decision quality with a mock-world harness

**Gate:** one inhabitant can survive and make meaningful choices without
making the world nondeterministic or unaffordable.

## Phase 4 — Society, economy, and family growth

- relationships and social memory
- emergent work and roles
- food storage, barter, local exchange, and economic decision-making
- households, farms, workshops, organizations, and simple ownership models
- housing, care, and population dynamics without a fixed numeric cap
- birth, childhood, inheritance, and death design
- multiple inhabitants with independent cognition schedules

**Gate:** a small group can form a legible settlement without population or API
costs running away.

## Phase 5 — Agent-created content and assets

- content declaration format
- asset proposal, normalization, provenance, and preview pipeline
- proposal validation and capability manifests
- isolated test-world execution
- versioning, compatibility, approval policy, and rollback
- first inhabitant-created buildings, recipes, or games

**Gate:** an inhabitant can materially change its world while the kernel and
host remain protected.

## Phase 6 — Richer worlds

- ecology, weather, factions, law, currency, and culture as justified by play
- larger chunked maps and distant-region simulation
- more expressive world rules
- carefully evaluated sandboxed behaviour, if still desirable

**Gate:** new systems solve demonstrated design needs rather than expanding the
feature list for its own sake.

## Phase 7 — Multiplayer and public worlds

- authenticated viewers and participants
- shared-world permissions
- player-added inhabitants and mod review
- server capacity and abuse controls
- public hosting, world discovery, and moderation

**Gate:** multiplayer is added to a stable single-world experience, not used to
hide an unstable simulation foundation.
