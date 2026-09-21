---
title: Phase 2 Implementation Ledger
type: implementation-plan
status: complete
updated: 2026-09-21
---

# Phase 2 Implementation Ledger

Phase 2 establishes a human-readable, owner-bound observation and interaction
boundary around the authoritative kernel. A client renders server projections
and submits requests; it never owns or mutates world state.

## Gate

A human must be able to inspect the world and explain what happened, while a
client remains unable to directly mutate authoritative state. Privileged
observation and control must be tied to a revocable paired device, not merely
to private-network membership. The protocol baseline is defined in the
[deterministic kernel contract](../planning/deterministic-kernel-contract.md).

## Capability ledger

| Contract area | Delivery issue | Evidence status | Code / test evidence |
| --- | --- | --- | --- |
| Versioned handshake, tick-indexed seeded snapshot, ordered event suffix, and static read-only browser inspection | [#95](https://github.com/compoodment/AgentWorld/issues/95) | verified | [c44fb37](https://github.com/compoodment/AgentWorld/commit/c44fb376803735cba1eebc458fc6778ff61387a6); ViewerObservationTests; ViewerHttpTests; [GitHub CI](https://github.com/compoodment/AgentWorld/actions/runs/35438342872); locked restore, format, and test command |
| Persistent private deployment of the read-only viewer | [#96](https://github.com/compoodment/AgentWorld/issues/96) | verified | systemd service source binds Kestrel to loopback only; private Tailscale Serve HTTPS proxy; deployed static page, handshake, and 405 write refusal checked over HTTPS; CI for the deployment source |
| Server-owned live scripted fixture and atomic reconnect baseline | [#97](https://github.com/compoodment/AgentWorld/issues/97) | verified | [a1e0026](https://github.com/compoodment/AgentWorld/commit/a1e0026996d2a1a38ff6b4f01ac9a596466a5468); LiveSeededWorldRuntimeTests; ViewerObservationTests; ViewerHttpTests; [GitHub CI](https://github.com/compoodment/AgentWorld/actions/runs/35440587981); deployed loopback and Tailnet reconnect checks |
| Thin Godot read-only projection client | [#98](https://github.com/compoodment/AgentWorld/issues/98) | verified | [25789a2](https://github.com/compoodment/AgentWorld/commit/25789a23fb17afd4d189de1d71eb2509ec515aa3) client and [a192c6b](https://github.com/compoodment/AgentWorld/commit/a192c6b410080ac0e7a911ea803487880023d5a4) portable verifier; GodotWorldObservationProtocolTests; 64 tests; `scripts/verify-godot-client.sh` checks the archived Godot engine SHA-256, builds C# scripts, and starts the scene headlessly; [GitHub CI](https://github.com/compoodment/AgentWorld/actions/runs/35442313574) |
| Paired-device authority, owner-only reconnect, and bounded replay-resistant signed requests | [#99](https://github.com/compoodment/AgentWorld/issues/99) | verified, Windows smoke complete | [`1636679`](https://github.com/compoodment/AgentWorld/commit/1636679ca8898a29688a75c30e3d8da5a84bbad1); `OwnerAuthorityStore`, `OwnerAuthorityStateFile`, `OwnerHttpProtocol`, pairing/client protocol tests, and `ViewerHttpTests`; locked restore, format, and 134 tests; [GitHub CI](https://github.com/compoodment/AgentWorld/actions/runs/35467414950); deployed loopback-only approval listener verified against the normal and Tailnet routes; real Windows pairing/reconnect completed 2026-09-21 |
| Durable Phase 2 runtime state, complete owner projections, and server-validated controls/paused authoring | [#99](https://github.com/compoodment/AgentWorld/issues/99) | verified, Windows smoke complete | [`1636679`](https://github.com/compoodment/AgentWorld/commit/1636679ca8898a29688a75c30e3d8da5a84bbad1); `PhaseTwoWorldRuntime`, `PhaseTwoWorldStateFile`, `PhaseTwoWorldObservationStore`, runtime/state-file tests, and owner HTTP tests; restart/reconnect coverage, full locked gate, and [GitHub CI](https://github.com/compoodment/AgentWorld/actions/runs/35467414950); real Windows client connected and displayed the paired world |
| Godot paired-owner inspector, request UI, and Windows 11 x64 portable-export path | [#99](https://github.com/compoodment/AgentWorld/issues/99) | verified, Windows smoke complete | [`1636679`](https://github.com/compoodment/AgentWorld/commit/1636679ca8898a29688a75c30e3d8da5a84bbad1); `Main.cs`, `UI/OwnerWorldApi.cs`, `Pairing/`, `export_presets.cfg`, and `scripts/verify-godot-windows-export.sh`; Godot C# build/headless scene start, manifest-verified Windows PE export, [GitHub CI](https://github.com/compoodment/AgentWorld/actions/runs/35467414950), and real paired Windows reconnect completed 2026-09-21 |

## Scope and deferrals

The browser can serve the completed deterministic camp-alpha harness by default,
or a server-owned instance beginning at genesis when the host enables the small
live fixture clock. In the active #99 delivery it remains a dependency-free
**protocol-discovery** page: it does not receive the map, actor, resources, or
event history. Those projections move to the signed paired-owner reconnect
baseline used by Godot.

Phase 2 separated durable runtime state from paired-device authority state;
validated pause/resume, instructions, and atomic paused authoring batches at
the server; and added the first Windows 11 x64 portable export path. It does
not claim a final game UI, a signed installer, or a production provider-control
surface. Godot remains the intended production-facing client; the browser
remains diagnostics only, so a debug viewer is not an accidental game client.

The post-gate client baseline is deliberately narrow: world-space inhabitant
selection, a contextual instruction card, distinct terrain/object/inhabitant
rendering layers, and a small procedural readability vocabulary. It is a
usability baseline for Phase 3, not a commitment to finish art or build the
Phase 5 asset pipeline early.

Asset attachment is additionally fail-closed: an owner request may name an
asset ID and digest, but only the immutable host-startup approved-asset catalog
can authorize that exact pair. The catalog contains no asset bytes and has no
owner API. A missing catalog approves nothing; an invalid existing document
stops the host before it can trust a partial allow-list. The same catalog
policy is supplied to both fresh runtime construction and saved-world replay,
so a save containing a formerly approved reference cannot restart if its host
approval is absent.

## Completion evidence: Windows owner smoke test

On 2026-09-21 the owner extracted the unsigned `agentworld-windows-11-x64`
artifact from CI, launched `AgentWorld.exe` while connected to the private
Tailnet, created the current-user device key, completed host-approved pairing,
and obtained an active paired reconnect. The client displayed the paired world.
This closes the Phase 2 owner smoke-test hold. SmartScreen still reports an
unknown publisher because the prototype export is intentionally unsigned; that
is a distribution/signing concern, not a Phase 2 authority failure.
