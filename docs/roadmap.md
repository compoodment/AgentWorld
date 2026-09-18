---
title: Design and Implementation Roadmap
type: roadmap
status: draft
updated: 2026-09-18
---

# Roadmap

This roadmap is intentionally gated. The project is still in the concept
phase; later phases are not commitments to build every feature.

## Phase 0 — Concept and foundation design (current)

- define the experience and design pillars
- decide which laws belong to the protected kernel
- define inhabitants, needs, family growth, and population limits
- define inventories, ownership, exchange, and the no-forged-state rule
- define the asset proposal and normalization pipeline
- define the content/mod proposal boundary
- classify first-world features and explicit deferrals in [feature scope](feature-scope.md)
- compare Godot, a separate simulation service, and storage options
- write failure modes and invariants before implementation
- keep unresolved decisions visible in [open questions](open-questions.md)

**Gate:** the first-world concept is coherent enough that implementation can
be judged against it, while remaining open to deliberate change.

## Phase 1 — Deterministic simulation kernel

- world time and logical coordinates
- terrain and resource primitives
- health, hunger, rest, shelter, and mortality rules
- inventories, ownership, access, production, consumption, and atomic transfer
- action validation and atomic state transitions
- one scripted inhabitant or test actor
- snapshots, event log, migrations, and replay fixtures

**Gate:** the world remains correct and reproducible without any LLM or visual
client.

## Phase 2 — World viewer and observation boundary

- choose the rendering/client engine
- render a seeded world and active entities
- inspect state and event history
- define the observation and action protocol
- run the server headlessly on a development machine or VPS

**Gate:** a human can understand what happened, and a client cannot directly
mutate authoritative state.

## Phase 3 — One LLM inhabitant

- add a configurable model/provider adapter
- event-driven cognition queue
- structured intentions and actions
- compact observations and durable memory prototype
- budgets, timeouts, fallback behaviour, and spend shutdown
- measure cost and decision quality with a mock-world harness

**Gate:** one inhabitant can survive and make meaningful choices without
making the world nondeterministic or unaffordable.

## Phase 4 — Society, economy, and limited family growth

- relationships and social memory
- emergent work and roles
- food storage, barter, local exchange, and economic decision-making
- households, farms, workshops, organizations, and simple ownership models
- housing, care, and population constraints
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
