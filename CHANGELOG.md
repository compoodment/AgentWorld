# Changelog

All notable player-facing releases will be documented here. AgentWorld has not
published a release yet.

## Unreleased

- Replaced the inspector-style Godot shell with a world-first, responsive
  16:9 play surface, fixed clock HUD, overlay rosters/events, an inhabitant-
  anchored detail card, and a pause menu containing settings and developer
  tools; raw protocol, tick, fixture, and draft language is no longer part of
  ordinary play.
- Pinned the host service to the private-world runtime with separate private
  save and pairing files, preserving the older fixture state instead of
  accidentally treating it as the playable world.
- Added the .NET 10 headless simulation foundation and reproducible test entry
  point for the Phase 1 deterministic kernel.
- Added a bounded canonical snapshot/event-log, migration, and replay
  feasibility spike; no production database or save-format promise is made.
- Added the first deterministic seeded-map/actor harness, including canonical
  map manifests, stable cardinal routing, a survival script, and a real
  save/reload/physical-replay digest proof.
- Added a staged atomic tick fixture with integer clock arithmetic, durable
  pause/resume epochs, crash-boundary recovery proof, and a shared pinned PCG32
  random-stream implementation.
- Added deterministic multi-actor movement resolution with stable destination
  claims, legal direct swaps, rejected longer cycles, and non-authoritative
  route-cache invalidation keys.
