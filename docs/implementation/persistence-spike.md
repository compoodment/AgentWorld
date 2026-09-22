---
title: Phase 1 Persistence Spike Evidence
type: feasibility-evidence
status: complete
updated: 2026-09-22
---

# Phase 1 Persistence Spike Evidence

This is the evidence record for [#87](https://github.com/compoodment/AgentWorld/issues/87).
It validates a small persistence boundary in the C# kernel. It is not a
long-term database decision or a production save format promise.

## Proposed spike format

The spike represents a save as two canonical UTF-8 byte streams:

1. a JSON **snapshot** with an explicit property order and the full
   `world_identity` required by the deterministic-kernel contract; and
2. a newline-delimited, append-only JSON **event log**, also with explicit
   property order and strictly increasing event IDs/ticks.

State and ordered-event digests are lowercase SHA-256 hex hashes over those
canonical bytes. The original tiny fixture state contains only a counter; it
exists to exercise ordering, replay, versioning, and recovery rather than
pretend the survival world has been implemented. Its v2 snapshot schema adds
an optional canonical state-payload field so the [#88 harness](seeded-harness.md)
can use the same envelope rather than invent a second save format. V1 snapshots
remain decodable by the spike.

## Evidence

`PersistenceSpikeTests` verifies:

- genesis replay and snapshot-plus-event-suffix replay produce the same
  canonical state digest;
- snapshot and event-log decode/re-encode returns byte-identical canonical
  bytes, and an inspector can read the save without running it;
- compatibility preflight rejects every required contract, simulation, clock,
  generator, map-manifest, content, and asset lock mismatch without mutation;
- a migration validation failure leaves the live source byte-identical and
  inspectable; and
- an interruption immediately after checkpointing resumes to the exact same
  bytes, state digest, and event digest as an uninterrupted migration.

Run the evidence with:

```bash
dotnet restore --locked-mode
dotnet test --configuration Release --no-restore
```

## Trade-offs and limits

**Useful now:** the bytes are readable, platform-neutral, deterministic, and
easy to inspect in tests. Explicit writers avoid dictionary/property-order
accidents that would poison replay digests.

**Not decided:** no database, production file layout, compression, retention,
concurrency model, encryption, or UI save browser has been selected. The
interruption test models the activation boundary in memory; it does not prove
real filesystem atomicity or power-loss durability.

Before adopting any persistence implementation beyond this spike, a production
candidate must prove actual durable checkpoint/replace semantics, corruption
handling, crash recovery at real I/O boundaries, migration chains, and the full
Phase 1 fixture corpus. Its serializer must preserve the same contract fields
and replay-digest guarantees or come with an explicit migration.
