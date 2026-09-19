---
title: Staged Kernel Evidence
type: implementation-evidence
status: verified-fixture
updated: 2026-09-19
---

# Staged Kernel Evidence

This is the evidence record for [#90](https://github.com/compoodment/AgentWorld/issues/90).
It establishes the atomic transaction and clock/control boundary that later
first-world systems must use. It does not claim that every future game mechanic
has already been wired into that boundary.

## What is implemented

`StagedTickKernel` has the complete fixed Phase 1 order:

```text
ingress -> clock/passive -> needs -> lifecycle -> movement -> work
-> communication -> cognition -> decisions/control -> commit
```

It runs a tick against a private staged state, assigns durable event IDs only
to a completed transaction, and exposes either the new whole checkpoint or the
unchanged prior checkpoint. The fixture's work phase currently adjusts only a
counter; [#91](https://github.com/compoodment/AgentWorld/issues/91) through
[#94](https://github.com/compoodment/AgentWorld/issues/94) attach the actual
movement, survival, inventory, and command/message behavior to this boundary.

The saved clock is integer-only: one tick is one in-world minute, there are
1,440 ticks per day and 365 days per year. Six real-time ticks per second is a
scheduler target, not state; wall-clock time is never read by the kernel.

`Pcg32XshRrV1` is the shared portable random stream implementation. It records
the algorithm identifier `pcg32-xsh-rr-v1` and derives named streams from the
world seed with HMAC-SHA-256. The existing map fixture now uses that shared
implementation rather than its own copy.

## Pause and recovery proof

The fixture suite injects a pause request after each of the ten phases. In all
cases the current tick completes exactly once, the world becomes paused before
another tick starts, and `resume` creates a durable run-epoch boundary without
changing world time. Resume while already running is idempotent.

It also injects an interruption after every phase. An interrupted result exposes
only the prior checkpoint—no partial state or phase event becomes visible.
Restarting from that checkpoint with the recorded input reaches the same
canonical state digest and ordered event digest as an uninterrupted tick.

The PCG fixture pins the first five draws for seed `fixture-seed` and stream
`weather` to:

```text
2746376332, 2393862774, 1216660101, 2255332717, 1720709021
```

## Reproduce

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet test --configuration Release --no-restore
```

The complete suite currently has 38 tests. The staged-kernel tests account for
the total phase-order/clock fixture, ten pause placements, ten interruption
boundaries, and the pinned random stream.

## Limits

This is an in-memory recovery proof, not a claim of real filesystem or
power-loss durability; the persistence spike's corresponding limits remain.
It also deliberately does not prove multi-actor movement, needs/resource
semantics, inventory/ownership, messages, live provider work, or a visual
client. Those are separate P1 blockers and must retain the same atomic/digest
properties when attached.
