---
title: Open Questions
type: decision-backlog
status: active
updated: 2026-09-19
---

# Open Questions

The first-world product policy is frozen. This file is deliberately short:
items belong here only when they block an upcoming phase or need prototype
evidence. Do not create new interview batches for speculative future systems.

## Phase 1 — deterministic simulation kernel

Phase 1 is complete. Its fixture-backed storage/replay and seeded-world choices
are recorded in the [Phase 1 ledger](../implementation/phase-1.md); they do
not remain open product questions.

## Later roadmap-phase implementation choices

- Phase 2: Godot is the intended player-facing client, while the browser stays
  a deliberately read-only diagnostic surface. The live headless host,
  reconnect baseline, and first Godot protocol client are now proven. Select
  production rendering details, a target platform for packaging, and any
  authoring workflow only when the next prototype needs them.
- Phase 3: measured observation, memory, inhabitant-capacity, and provider-usage
  budgets under the [cognition and society contract](../planning/cognition-and-society-contract.md);
  the contract's admission, backpressure, pause, and emergency-stop behavior is
  already settled.
- Phase 5: executable-mod sandbox/runtime choice and measured resource budgets
  within the [content-governance contract](../planning/content-governance-contract.md).

No item above blocks the deterministic Phase 1 kernel.
