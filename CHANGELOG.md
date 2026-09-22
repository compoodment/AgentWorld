# Changelog

All notable player-facing, world-simulation, save-compatibility, deployment,
and security changes are documented here. AgentWorld has not published a
release yet.

## Unreleased

### Added

- Added a deterministic .NET 10 headless simulation with an integer world
  clock, atomic ticks, durable pause/resume epochs, bounded recovery, and a
  pinned PCG32 random stream.
- Added seeded world generation with canonical terrain and resource manifests,
  stable cardinal routing, deterministic multi-inhabitant movement, contention
  resolution, legal direct swaps, and route-cache invalidation.
- Added survival simulation for hunger, energy, harvesting, eating, sleeping,
  renewable resources, and resource regeneration.
- Added canonical snapshots and event logs, schema migrations, save/reload,
  physical replay, compatibility checks, and digest proofs for deterministic
  world recovery.
- Added lot-based inventories with quantity, freshness, spoilage, reservations,
  ownership, capacity checks, atomic transfers, and exact barter settlement.
- Added durable world commands and messages with idempotency, stale-result
  rejection, and crash-safe processing boundaries.
- Added a private hosted world runtime with reconnectable observation, a
  browser diagnostic viewer, and a Godot client for normal play.
- Added owner-device pairing, signed owner requests, anti-replay protection,
  device management, and owner-only controls for pausing, resuming, and giving
  inhabitants instructions.
- Added deterministic cognition with bounded legal choices, validated
  responses, retries, fallbacks, usage accounting, provider-outage pausing,
  and restart-safe pending decisions. Supported adapters include the local
  deterministic provider, Jev, OpenAI-compatible providers, and Ollama Cloud.
- Added independently scheduled inhabitants with persistent identities, roles,
  needs, skills, intentions, inventories, and per-inhabitant cognition
  configuration.
- Added typed, consent-aware relationships, households, caregivers, social
  interactions, and atomic person-to-person trade.
- Added family and mortality simulation including birth, aging, death,
  tombstones, estates, inheritance, and household continuity.
- Added a four-inhabitant private world that saves and restores movement,
  needs, inventories, relationships, cognition, production, and world events.
- Added ecology and weather state, factions and laws, currencies, cultural
  state, and deterministic chunk manifests to the private-world simulation.
- Added governed data-only content packages with canonical IDs, semantic
  versions, dependency locks, validation, owner approval, staging, activation,
  rollback, and quarantine.
- Added typed content definitions for materials, buildings, recipes, and
  production, with deterministic preview and validation before activation.
- Added deterministic building placement, production jobs, ingredient and
  asset reservations, workstation checks, and inventory completion.
- Added asset governance with format and quota validation, rights and source
  metadata, provenance manifests, canonical package digests, deterministic
  cache accounting, previews, and portable artifact envelopes.
- Added public inhabitant intention and relationship summaries without
  exposing private model reasoning.
- Added inhabitant-chosen `build` actions. Inhabitants can independently choose
  valid structures or recipes; every generated world supplies reachable fertile
  land, and crop builds such as carrots complete into household inventory.
- Added a portable Windows 11 x64 game build and an automated verification
  pipeline that tests the simulation, starts the Godot client, exports Windows,
  and uploads the resulting artifact.

### Changed

- Replaced the inspector-style Godot shell with a world-first, responsive 16:9
  play surface. The world now fills the screen beneath a compact clock and
  weather HUD instead of sharing space with permanent developer panels.
- Moved the inhabitants roster and recent events into temporary popovers, and
  moved settings, display controls, and developer tools into the pause menu.
- Changed inhabitant inspection to a closeable card anchored near the selected
  person. No empty selection panel is shown before a person is selected.
- Replaced raw ticks with a player-readable `Day N · HH:MM` clock and filtered
  routine movement, cognition, and tick noise out of the recent-events view so
  it can focus on meaningful world events.
- Removed protocol versions, fixture IDs, provider state, authoring drafts, and
  other implementation language from ordinary play; diagnostics remain
  available under Developer tools.
- Made the integrated private world the intended playable runtime and isolated
  its save and pairing authority from the preserved one-person fixture used by
  diagnostics.
- Expanded the map from a fixture grid into layered terrain, resources,
  buildings, and selectable inhabitants with contextual inspection.

### Fixed

- Fixed re-pairing after a world-authority change by exposing a `Pair again`
  action in Settings; players no longer need to find and delete client files to
  replace an obsolete saved registration.
- Fixed restart and replay edge cases across inventories, reservations,
  owner-control idempotency, cognition scheduling, stale provider responses,
  and durable command recovery.
- Fixed client selection and map-layer interactions so clearing a selection,
  selecting an inhabitant from either the map or roster, and reconnecting all
  produce the same observation state.
- Fixed the game viewport so it scales to the display without making the whole
  play screen scrollable or reserving a permanent right-hand sidebar.

### Security

- Stored paired-owner signing keys as non-exportable Windows CNG keys and
  required explicit pairing approval before owner capabilities are granted.
- Kept provider credentials environment-only and excluded secrets and private
  reasoning from saves, replay digests, and owner observations.
- Kept content packages data-only and fail-closed: executable mods, invalid
  dependencies, unapproved assets, and over-quota packages cannot activate.
