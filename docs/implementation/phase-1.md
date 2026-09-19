---
title: Phase 1 Implementation Ledger
type: implementation-plan
status: active
updated: 2026-09-19
---

# Phase 1 Implementation Ledger

Phase 1 delivers a headless deterministic simulation kernel. This ledger
separates the settled design from implementation proof. It is not a task board:
GitHub owns active work and discussion; code, tests, and CI own runtime truth.

## Gate

The milestone acceptance proof is:

```text
seed -> move -> harvest -> eat and sleep -> save -> reload and replay
     -> identical canonical state and ordered-event digest
```

It must run without an LLM or visual client. The complete semantic source is
the [deterministic kernel contract](../planning/deterministic-kernel-contract.md).

## Capability ledger

The linked issue is the live-work source; code, named fixtures, and the
reproducible verification command provide the evidence below.

| Contract area | Delivery issue | Evidence status | Code / test evidence |
| --- | --- | --- | --- |
| Runtime, test framework, dependency and CI entrypoint | [#86](https://github.com/compoodment/AgentWorld/issues/86) | verified | [C# toolchain](../planning/csharp-toolchain.md); `dotnet restore --locked-mode && dotnet format --verify-no-changes --no-restore && dotnet test --configuration Release --no-restore` |
| World identity, events, snapshots, migrations, and replay persistence | [#87](https://github.com/compoodment/AgentWorld/issues/87) | verified spike | [Persistence spike evidence](persistence-spike.md); `dotnet test --configuration Release --no-restore` |
| Seed corpus, generated-map acceptance, scripted actor, and canonical digest harness | [#88](https://github.com/compoodment/AgentWorld/issues/88) | verified harness | [Seeded harness evidence](seeded-harness.md); `dotnet test --configuration Release --no-restore` |
| Integral tick loop, clock, pause/resume, ordered ingress, and deterministic randomness | [#90](https://github.com/compoodment/AgentWorld/issues/90) | verified fixture | [Staged kernel evidence](staged-kernel.md); `dotnet test --configuration Release --no-restore` |
| Routing, reservations, movement contention, and cache invalidation | [#91](https://github.com/compoodment/AgentWorld/issues/91) | verified fixture | [Deterministic movement evidence](deterministic-movement.md); `dotnet test --configuration Release --no-restore` |
| Integer needs, exhaustion/recovery, harvest-to-zero, and scheduled resource regeneration | [#92](https://github.com/compoodment/AgentWorld/issues/92) | verified fixture | `SurvivalFixtureTests`; `dotnet test --configuration Release --no-restore` |
| Lot inventory, spoilage/restore, reservations, ownership, and exact-revision direct barter | [#93](https://github.com/compoodment/AgentWorld/issues/93) | verified fixture | `InventoryFixtureTests`; `dotnet test --configuration Release --no-restore` |
| Durable commands/messages, duplicate rejection, ordered delivery, and stale provider-result rejection | [#94](https://github.com/compoodment/AgentWorld/issues/94) | verified fixture | `ControlFixtureTests`; `dotnet test --configuration Release --no-restore` |
| Contract fixture matrix and end-to-end acceptance gate | [#89](https://github.com/compoodment/AgentWorld/issues/89) | verified | all test fixtures; `dotnet restore --locked-mode && dotnet format --verify-no-changes --no-restore && dotnet test --configuration Release --no-restore` |

Issue #89 remains the recorded acceptance umbrella: it closes only after every
fixture row is verified together from the documented clean setup.

## Scope and non-goals

Phase 1 does not add a visual client, live model/provider calls, multiplayer,
or a long-term database commitment. The deterministic mock path and persistence
spike exist so later systems have something hard and replayable to attach to.

## Updating this ledger

- `implemented` needs a merged PR link and a named test or command.
- `verified` additionally needs reproducible passing fixture or CI evidence.
- A capability affected by a contract change must cite the decision and
  migration/replay impact before its status moves forward.
- Do not copy changing GitHub issue status, estimates, or ownership here.
