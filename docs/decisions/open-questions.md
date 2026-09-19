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

The behavioral contract and fixture categories are settled in the
[deterministic kernel contract](../planning/deterministic-kernel-contract.md).
Only implementation choices remain:

- Which implementation language and test toolchain produce the smallest
  reproducible headless kernel on the target host?
- What event, snapshot, and migration storage format is sufficient for the
  first replay fixtures without prematurely choosing the long-term database?
- What compact seeded map, terrain fixture set, and test actors expose movement,
  needs, ownership, and persistence failures quickly?

These are build-spec choices. Resolve them in the first implementation plan and
record the chosen direction in the [decision register](decision-register.md).

## Later roadmap-phase implementation choices

- Phase 2: whether Godot is the viewer, client only, or a discarded prototype
  tool; native versus web client.
- Phase 3: measured observation, memory, inhabitant-capacity, and provider-usage
  budgets under the [cognition and society contract](../planning/cognition-and-society-contract.md);
  the contract's admission, backpressure, pause, and emergency-stop behavior is
  already settled.
- Phase 5: executable-mod sandbox/runtime choice and measured resource budgets
  within the [content-governance contract](../planning/content-governance-contract.md).

No item above blocks the deterministic Phase 1 kernel.
