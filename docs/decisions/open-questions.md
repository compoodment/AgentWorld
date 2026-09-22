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
- Phase 4: complete. The implementation and evidence are tracked in the
  [Phase 4 ledger](../implementation/phase-4.md). Natural mortality uses the
  accepted no-hard-maximum curve: elderhood is a social marker, risk rises
  gradually, and the kernel owns terminal death and estate settlement.
- Phase 5: executable-mod sandbox/runtime choice and measured resource budgets
  within the [content-governance contract](../planning/content-governance-contract.md).

The next unresolved work is Phase 5 content governance and agent-created
content; Phase 4 policy is no longer blocking.
