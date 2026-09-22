# AgentWorld documentation

This directory is organized by purpose rather than by the order in which the
project happened to ask questions.

## Start here

1. [Vision](concept/vision.md) — the experience and design pillars.
2. [Feature scope](planning/feature-scope.md) — protected foundation,
   first-world priorities, and explicit deferrals.
3. [Decision register](decisions/decision-register.md) — current accepted
   first-world decisions.
4. [Deterministic kernel contract](planning/deterministic-kernel-contract.md)
   — the Phase 1 executable-design boundary.
5. [Phase 1 implementation ledger](implementation/phase-1.md) — deterministic
   kernel capability evidence.
6. [Phase 2 implementation ledger](implementation/phase-2.md) — completed
   owner-observation, interaction-boundary, and Windows-export evidence.
7. [Roadmap](planning/roadmap.md) — gated implementation sequence.
8. [Phase 3 implementation plan](implementation/phase-3.md) — completed
   one-inhabitant cognition loop and provider boundary.
9. [Phase 4 implementation ledger](implementation/phase-4.md) — completed
   society, multi-inhabitant scheduling, family, exchange, mortality, and
   lifecycle evidence.

## Concept

- [World model](concept/world-model.md)
- [Inhabitants](concept/inhabitants.md)
- [Economy](concept/economy.md)
- [Creation and modding](concept/creation-and-modding.md)
- [Assets and art](concept/assets-and-art.md)

## Planning

- [Architecture direction](planning/architecture.md)
- [C# and Godot toolchain](planning/csharp-toolchain.md)
- [Phase 2 owner device pairing](planning/device-pairing.md)
- [Feature scope](planning/feature-scope.md)
- [Roadmap](planning/roadmap.md)
- [Versioning and releases](planning/versioning-and-releases.md)
- [Deterministic kernel and recovery contract](planning/deterministic-kernel-contract.md)
- [Cognition, authority, and society contract](planning/cognition-and-society-contract.md)
- [Content, mod, and constitutional governance contract](planning/content-governance-contract.md)

## Decisions

- [Decision register](decisions/decision-register.md) — current authority for
  accepted product decisions.
- [Open questions](decisions/open-questions.md) — only unresolved work that
  needs a decision or prototype evidence.
- [Design log](decisions/design-log.md) — chronological rationale and history.

## Delivery and governance

- [Document authority and delivery evidence](governance/document-authority.md)
  — which source answers which question, and how implementation proof is
  recorded.
- [Phase 1 implementation ledger](implementation/phase-1.md) — capability
  evidence for the deterministic vertical slice. GitHub owns its active-work
  state.
- [Phase 2 implementation ledger](implementation/phase-2.md) — completed
  evidence for paired owner observation, server-validated interaction, and the
  Windows 11 x64 export path.
- [Phase 4 implementation ledger](implementation/phase-4.md) — completed
  society implementation scope, gate, and accepted mortality model.
- [Persistence spike evidence](implementation/persistence-spike.md) — the
  bounded save/replay/migration feasibility result, not a database commitment.
- [Seeded harness evidence](implementation/seeded-harness.md) — the first
  executable map, actor, action, and save/replay acceptance slice.
- [Staged kernel evidence](implementation/staged-kernel.md) — atomic tick,
  clock, pause/resume, recovery, and deterministic-randomness fixtures.
- [Deterministic movement evidence](implementation/deterministic-movement.md)
  — movement claims, swaps, blocking, and route-cache invalidation fixtures.

## Maintenance rule

Keep current decisions in the decision register, rationale in the design log,
and only genuinely unresolved work in open questions. Retire worksheets once
their proposals are accepted or superseded; Git history remains the archive.
Use GitHub milestones and issues for live work, and update an implementation
ledger only when code or test evidence changes a capability's status.
