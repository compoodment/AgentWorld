# Changelog

All notable player-facing releases will be documented here. AgentWorld has not
published a release yet.

## Unreleased

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
