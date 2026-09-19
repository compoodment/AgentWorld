---
title: Phase 2 Implementation Ledger
type: implementation-plan
status: active
updated: 2026-09-19
---

# Phase 2 Implementation Ledger

Phase 2 establishes a human-readable observation boundary around the
authoritative kernel. The client renders projections and requests future
actions; it never owns or mutates world state.

## Gate

A human must be able to inspect the world and explain what happened, while a
client remains unable to directly mutate authoritative state. The protocol
baseline is defined in the
[deterministic kernel contract](../planning/deterministic-kernel-contract.md).

## Capability ledger

| Contract area | Delivery issue | Evidence status | Code / test evidence |
| --- | --- | --- | --- |
| Versioned handshake, tick-indexed seeded snapshot, ordered event suffix, and static read-only browser inspection | [#95](https://github.com/compoodment/AgentWorld/issues/95) | verified | [c44fb37](https://github.com/compoodment/AgentWorld/commit/c44fb376803735cba1eebc458fc6778ff61387a6); ViewerObservationTests; ViewerHttpTests; [GitHub CI](https://github.com/compoodment/AgentWorld/actions/runs/35438342872); locked restore, format, and test command |
| Persistent private deployment of the read-only viewer | [#96](https://github.com/compoodment/AgentWorld/issues/96) | verified | systemd service source binds Kestrel to loopback only; private Tailscale Serve HTTPS proxy; deployed static page, handshake, and 405 write refusal checked over HTTPS; CI for the deployment source |
| Server-owned live scripted fixture and atomic reconnect baseline | [#97](https://github.com/compoodment/AgentWorld/issues/97) | implemented | `LiveSeededWorldRuntime`; `LiveWorldRuntimeService`; ViewerObservationTests; ViewerHttpTests; LiveSeededWorldRuntimeTests; local locked restore, format, and test command |

## Scope and deferrals

The browser can serve the completed deterministic camp-alpha harness by default,
or a server-owned instance beginning at genesis when the host enables the small
live fixture clock. It exposes a protocol handshake, map, actor, resources,
replayable event history, and atomic reconnect baseline through GET endpoints
and a dependency-free static UI.

It does not yet provide authenticated actions, interpolation, paused authoring,
provider controls, persistence lifecycle, or a Godot client. Godot is the
intended production-facing client; the browser remains diagnostics only, so a
debug viewer is not an accidental game client.
