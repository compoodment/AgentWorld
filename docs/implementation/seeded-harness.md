---
title: Seeded Harness Evidence
type: implementation-evidence
status: complete
updated: 2026-09-22
---

# Seeded Harness Evidence

This is the evidence record for [#88](https://github.com/compoodment/AgentWorld/issues/88).
It implements the smallest executable slice of the deterministic-kernel
contract: a selected temperate camp map, one actor, four cardinal routing,
integral action ticks, and a script that can survive a save/reload boundary.

## Fixture boundary

The harness has no UI, LLM/provider calls, multiplayer, economy, or database.
It uses a bounded 6 × 5 logical grid and one founder (`actor-scout`). Its map
contains a legal shelter, bedroll, storage, cooking point, reachable renewable
berry patch, and reachable construction tree. Two deterministic border
obstacles exercise explicit impassable terrain without cutting a required
camp-start route.

The selected map is identified by its seed, attempt number, generator/version
locks, and canonical map-manifest digest. The fixed corpus is deliberately
small but pins the byte-level output:

| Seed | Canonical manifest SHA-256 |
| --- | --- |
| `camp-alpha` | `ca6d5bfc9e6dc4d98dd77a9c2a02f847f3ecdcb12dfa5291476dcec70e653cf2` |
| `camp-beta` | `c5d49306cf76044707b4e3971c69041aa67278a9d4b407bda515a97c301817ce` |
| `camp-gamma` | `196ebc00ad937acc7283d262ab9f3f4cc26294991ed7f49c031050e7124c3896` |

Worldgen derives only its attempt-local stream from the saved seed using
`pcg32-xsh-rr-v1` with HMAC-SHA-256 state/increment derivation. The fixture
records its selected attempt; it fails with diagnostics after 32 failed
attempts instead of accepting an invalid map.

## Executed path

Each action consumes exactly one integral world tick. The tiny loop applies
integer need drain first, then one movement or work action, then creates a
strictly increasing durable event. The scripted path is:

```text
genesis -> cardinal moves to berries -> harvest -> consume
        -> cardinal moves to bedroll -> sleep
```

Movement uses cardinal neighbors in `north, east, south, west` order, integer
cost 100, Manhattan A*, and the contract's complete priority key
`(f, h, g, y, x, predecessor_y, predecessor_x, route_node_id)`. The equal-cost
fixture pins the route `(0,0) -> (1,0) -> (2,0) -> (2,1)`.

## Save, reload, and replay

The harness does **not** introduce a competing save representation. It uses the
[#87 persistence envelope](persistence-spike.md): canonical snapshot JSON plus
the ordered append-only event log. The harness's canonical state payload sits
inside the snapshot and includes the exact map-manifest bytes, actor state, and
resource availability. Load verifies all of the following before accepting it:

1. the snapshot's counter/event position agrees with a generic event-log replay;
2. the embedded manifest exactly reproduces the saved world identity; and
3. physically replaying the ordered action events from genesis produces the
   same canonical state payload.

The acceptance test saves after the harvest/consume portion, reloads it, runs
the remaining moves and sleep, and compares both final canonical state and
ordered-event digests against a clean full run.

## Reproduce

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet test --configuration Release --no-restore
```

The suite currently includes the foundation and persistence tests plus seven
seeded-harness assertions: fixed corpus output, route tie-breaking, scripted
actions, identical whole-run digests, and save/reload/physical-replay equality.

## Deliberate limits

This document is evidence for the seeded end-to-end slice only. The companion
fixtures now cover clock/pause/recovery and deterministic routing ([#90](https://github.com/compoodment/AgentWorld/issues/90),
[#91](https://github.com/compoodment/AgentWorld/issues/91)), survival/resource
lifecycle ([#92](https://github.com/compoodment/AgentWorld/issues/92)),
inventory/barter ([#93](https://github.com/compoodment/AgentWorld/issues/93)),
and durable ingress/provider-result safety ([#94](https://github.com/compoodment/AgentWorld/issues/94)).
The [Phase 1 implementation ledger](phase-1.md) is the complete current
acceptance matrix.
