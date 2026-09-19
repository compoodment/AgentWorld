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

All kernel rows begin as `specified`. The toolchain foundation is verified, but
that does not mean any world behavior exists. The linked issue is the
live-work source; replace `none` with a merged PR and test or CI command only
when evidence exists.

| Contract area | Delivery issue | Evidence status | Code / test evidence |
| --- | --- | --- | --- |
| Runtime, test framework, dependency and CI entrypoint | [#86](https://github.com/compoodment/AgentWorld/issues/86) | verified | [C# toolchain](../planning/csharp-toolchain.md); `dotnet restore --locked-mode && dotnet format --verify-no-changes --no-restore && dotnet test --configuration Release --no-restore` |
| World identity, events, snapshots, migrations, and replay persistence | [#87](https://github.com/compoodment/AgentWorld/issues/87) | specified | none |
| Seed corpus, generated-map acceptance, scripted actor, and canonical digest harness | [#88](https://github.com/compoodment/AgentWorld/issues/88) | specified | none |
| Integral tick loop, clock, pause/resume, ordered ingress, and deterministic randomness | [#89](https://github.com/compoodment/AgentWorld/issues/89) | specified | none |
| Routing, reservations, movement contention, and cache invalidation | [#89](https://github.com/compoodment/AgentWorld/issues/89) | specified | none |
| Needs, resources, lots, reservations, ownership, commands, and messages | [#89](https://github.com/compoodment/AgentWorld/issues/89) | specified | none |
| Contract fixture matrix and end-to-end acceptance gate | [#89](https://github.com/compoodment/AgentWorld/issues/89) | specified | none |

Issue #89 is intentionally an acceptance umbrella, not permission to hide
unbounded work. Split a new narrowly scoped issue before implementation when a
row cannot be completed by one reviewable pull request.

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
