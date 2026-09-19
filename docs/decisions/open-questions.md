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

- Which implementation language and test toolchain produce the smallest
  reproducible headless kernel on the target host?
- What event, snapshot, and migration storage format is sufficient for the
  first replay fixtures without prematurely choosing the long-term database?
- What compact seeded map, terrain fixture set, and test actors expose movement,
  needs, ownership, and persistence failures quickly?

These are build-spec choices. Resolve them in the Phase 1 prototype contract
and acceptance tests, then record the chosen direction in the
[decision register](decision-register.md).

## Deferred until their roadmap phase

- Phase 2: whether Godot is the viewer, client only, or a discarded prototype
  tool; native versus web client.
- Phase 3: the provider adapter schema, observation budget, memory compression,
  and measured inhabitant capacity/cost.
- Phase 5: mod migration across simulation versions, resource-proof/testing
  strategy, automatic low-risk-content adoption policy, and the executable-mod
  sandbox runtime.

No item above blocks the deterministic Phase 1 kernel.
