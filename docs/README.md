---
title: AgentWorld Documentation Map
type: documentation-index
status: active
updated: 2026-09-22
---

# AgentWorld documentation

AgentWorld uses **one canonical source per question**. Documents may link to a
canonical source, but should not maintain competing summaries of current state
or future work.

## Canonical sources

| Question | Canonical source |
| --- | --- |
| What works in the game today? | [Current state](status/current-state.md) |
| What are we building next, and what is its gate? | [Roadmap](planning/roadmap.md) |
| Which confirmed problems are still open? | [Known bugs](bugs.md) |
| What is the intended experience? | [Vision](concept/vision.md) |
| Which product policy is accepted? | [Decision register](decisions/decision-register.md) |
| Why was a decision made? | [Design log](decisions/design-log.md) |
| What is the current system boundary? | [Architecture](planning/architecture.md) |
| What exact semantics must code preserve? | [Implementation contracts](#contracts-and-policy) |
| What evidence implemented a historical slice? | [Implementation evidence](#implementation-evidence) |
| What still needs a product decision? | [Open questions](decisions/open-questions.md) |

For detailed authority and maintenance rules, see
[Document authority](governance/document-authority.md).

## Read this first

1. [Current state](status/current-state.md) — honest playable/integrated/fixture
   status.
2. [Roadmap](planning/roadmap.md) — the outcome-driven sequence, beginning
   with **Living Settlement**.
3. [Known bugs](bugs.md) — confirmed UI, cognition and content-path problems.
4. [Vision](concept/vision.md) — stable long-horizon intent.
5. [Architecture](planning/architecture.md) — the implemented client/host/
   simulation boundary.

## Document classes

### Current product truth

- [Current state](status/current-state.md)
- [Known bugs](bugs.md)
- [Roadmap](planning/roadmap.md)

These are the only documents that should summarize the whole current product
or whole forward plan. Update them when behavior or priorities change.

### Concepts

- [Vision](concept/vision.md)
- [World model](concept/world-model.md)
- [Inhabitants](concept/inhabitants.md)
- [Economy](concept/economy.md)
- [Creation and modding](concept/creation-and-modding.md)
- [Assets and art](concept/assets-and-art.md)

Concept documents describe stable intent. They are not evidence that a feature
is playable.

### Contracts and policy

- [Architecture](planning/architecture.md)
- [Feature scope](planning/feature-scope.md)
- [Deterministic kernel contract](planning/deterministic-kernel-contract.md)
- [Cognition and society contract](planning/cognition-and-society-contract.md)
- [Content governance contract](planning/content-governance-contract.md)
- [Owner device pairing](planning/device-pairing.md)
- [C# and Godot toolchain](planning/csharp-toolchain.md)
- [Versioning and releases](planning/versioning-and-releases.md)

Contracts define invariants and accepted semantics. They do not claim that
every described system is integrated into the default private world.

### Decisions

- [Decision register](decisions/decision-register.md) — current accepted
  product policy.
- [Open questions](decisions/open-questions.md) — unresolved decisions only.
- [Design log](decisions/design-log.md) — chronological rationale; historical
  statements may describe retired prototypes.

### Implementation evidence

- [Phase 1 ledger](implementation/phase-1.md)
- [Phase 2 ledger](implementation/phase-2.md)
- [Phase 3 ledger](implementation/phase-3.md)
- [Phase 4 ledger](implementation/phase-4.md)
- [Phase 5 ledger](implementation/phase-5.md)
- [Private alpha integration plan](implementation/playtest-vertical-slice.md)
- [Persistence spike](implementation/persistence-spike.md)
- [Seeded harness](implementation/seeded-harness.md)
- [Staged kernel](implementation/staged-kernel.md)
- [Deterministic movement](implementation/deterministic-movement.md)

Ledgers and evidence documents record what a bounded contract or fixture proved
at a point in time. A phase marked complete does **not** mean every noun in that
phase is a rich player-visible loop. Consult [current state](status/current-state.md)
for that distinction.

## Maintenance rule

When a change affects behavior, UI, operations, compatibility or product
scope, update the relevant canonical document in the same change. Historical
evidence should normally remain unchanged; add a superseding link instead of
rewriting what an old proof established. GitHub issues own active execution,
while these documents own durable product truth.
