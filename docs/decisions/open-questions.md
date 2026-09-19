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
The C#/.NET 10 toolchain and xUnit test path are settled in the
[Phase 1 C# toolchain](../planning/csharp-toolchain.md). The remaining
implementation choices are:

- What event, snapshot, and migration storage format is sufficient for the
  first replay fixtures without prematurely choosing the long-term database?
  Collect prototype evidence in
  [#87](https://github.com/compoodment/AgentWorld/issues/87).
- What compact seeded map, terrain fixture set, and test actors expose movement,
  needs, ownership, and persistence failures quickly? Build the initial harness
  through [#88](https://github.com/compoodment/AgentWorld/issues/88).

These are build-spec choices. Resolve them in the first implementation plan and
record the chosen direction in the [decision register](decision-register.md).
GitHub owns their live workflow state; the
[Phase 1 ledger](../implementation/phase-1.md) records only implementation
evidence.

## Later roadmap-phase implementation choices

- Phase 2: Godot is the intended player-facing client, while the browser stays
  a deliberately read-only diagnostic surface. Prove the live headless host,
  reconnect baseline, and first Godot protocol client through the Phase 2
  milestone before selecting production rendering details, packaging, or any
  authoring workflow.
- Phase 3: measured observation, memory, inhabitant-capacity, and provider-usage
  budgets under the [cognition and society contract](../planning/cognition-and-society-contract.md);
  the contract's admission, backpressure, pause, and emergency-stop behavior is
  already settled.
- Phase 5: executable-mod sandbox/runtime choice and measured resource budgets
  within the [content-governance contract](../planning/content-governance-contract.md).

No item above blocks the deterministic Phase 1 kernel.
