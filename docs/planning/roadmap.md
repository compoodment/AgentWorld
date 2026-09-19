---
title: Design and Implementation Roadmap
type: roadmap
status: active
updated: 2026-09-19
---

# Roadmap

This roadmap is intentionally gated. The first-world concept gate is complete;
later phases are not commitments to build every feature.

## Phase 0 — Concept and foundation design (complete)

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
- keep unresolved implementation choices visible in
  [open questions](../decisions/open-questions.md)

**Gate met:** the first-world concept is coherent enough that implementation
can be judged against it. Further changes require deliberate decisions or
prototype evidence.

## Phase 1 — Deterministic simulation kernel (complete)

The exact Phase 1 semantics and replay fixtures are in the
[deterministic kernel contract](deterministic-kernel-contract.md).
Its live delivery work is tracked by the
[P1 milestone](https://github.com/compoodment/AgentWorld/milestone/1), while
the [Phase 1 implementation ledger](../implementation/phase-1.md) records
capability evidence. Neither replaces this roadmap's gate.

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

## Phase 2 — World viewer and observation boundary (current)

The protocol and client-correction baseline are defined in the
[deterministic kernel contract](deterministic-kernel-contract.md).

- use Godot as the intended player-facing rendering/client engine; keep the
  browser as a protocol-discovery and diagnostic surface
- bind full-world observation and control to a revocable paired owner device;
  Tailnet/private-network reachability is transport only, not application
  authority
- prove a Godot paired-owner inspector against the versioned handshake and
  atomic reconnect baseline before committing to production rendering or
  authoring workflows
- render a seeded world and active entities
- inspect the complete world, selected inhabitants, summarized decision factors,
  spatial knowledge, and event history
- show current-tile knowledge, local perception, destinations, and active routes
- persist the live runtime and owner-authority state separately, with validated
  restart/reconnect behavior
- expose server-validated suggestive/must-do instructions and the full paused
  authoring request surface:
  terrain, water, resources, plants, buildings, inhabitants, weather/seasons,
  and checked human assets
- define and test the signed observation/action protocol, including replay
  resistance and revocation
- establish an unsigned Windows 11 x64 portable export path for later owner
  testing; do not mistake that path for a released or final UI
- run the server headlessly on a development machine or VPS

**Gate:** a human can understand what happened through a paired client, and no
client can directly mutate authoritative state. The browser must remain useful
for diagnosis without becoming an unauthenticated owner console. Phase 2 stays
current while [#99](https://github.com/compoodment/AgentWorld/issues/99) receives
its final verification, deployment, and evidence ledger.

## Phase 3 — One LLM inhabitant

The queue, provider, authority, and telemetry semantics are defined in the
[cognition and society contract](cognition-and-society-contract.md).

- add a configurable model/provider adapter
- event-driven cognition queue
- structured intentions and actions
- destination-level travel intentions with server-side route execution
- compact observations and durable memory prototype
- bounded observations including spatial memory
- coalesced event triggers and urgency-based fallback behaviour
- provider/model selection and later changes, individual-failure local
  fallback, provider-outage pause and notification, and provider status
- measure provider usage, queue pressure, and decision quality against the
  contract's admission and stop controls with a mock-world harness; choose a
  numeric call/token ceiling only if prototype evidence requires one

**Gate:** one inhabitant can survive and make meaningful choices without making
the world nondeterministic, silently exceeding declared queue/admission limits,
or hiding provider usage and stop/fallback events.

## Phase 4 — Society, economy, and family growth

The relationship, consent, family, estate, and access semantics are defined in
the [cognition and society contract](cognition-and-society-contract.md).

- relationships and social memory
- emergent work and roles
- food storage, barter, local exchange, and economic decision-making
- households, farms, workshops, organizations, and simple ownership models
- housing, care, and population dynamics without a fixed numeric cap
- birth, childhood, inheritance, and death design
- multiple inhabitants with independent cognition schedules

**Gate:** a small group can form a legible settlement without unbounded
cognition queueing or silent provider-cost growth; provider/account billing
remains the external boundary and all runtime stop/fallback behavior remains
observable and replayable.

## Phase 5 — Agent-created content and assets

The activation, package, asset, quarantine, and constitutional semantics are
defined in the [content-governance contract](content-governance-contract.md).

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
