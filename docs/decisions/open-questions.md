---
title: Open Questions
type: decision-backlog
status: active
updated: 2026-09-22
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

- Phase 2: complete. Godot is the intended player-facing client, while the
  browser remains a deliberately read-only discovery/diagnostic surface. Final
  rendering, installer/signing, and later authoring UX can wait for a post-
  Phase-2 prototype that needs them.
- Phase 3: complete. The provider boundary, admission, backpressure, pause,
  fallback, and emergency-stop behavior are implemented and covered by the
  [Phase 3 plan](../implementation/phase-3.md). Further budget tuning is
  prototype evidence, not a prerequisite for Phase 4 preparation.
- Phase 4: the policy baseline is accepted and implementation is tracked in the
  [Phase 4 plan](../implementation/phase-4.md). The remaining product decision
  before the family-lifecycle slice is the first-world natural lifespan model:
  hazard-only death initially, an age-based mortality curve without a hard
  maximum, or an age-based curve with a declared maximum age. The existing
  terminal-death, estate, consent, age-band, and replay rules are not open.
- Phase 5: executable-mod sandbox/runtime choice and measured resource budgets
  within the [content-governance contract](../planning/content-governance-contract.md).

The lifespan choice blocks only the natural-aging/family-lifecycle slice; it
does not block Phase 4 scheduler, relationship, household, or exchange work.
