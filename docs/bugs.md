---
title: Known Bugs and Product Gaps
type: defect-register
status: active
updated: 2026-09-22
---

# Known bugs and product gaps

This is the canonical durable list of confirmed current problems. GitHub issues
may track active implementation work, but closing an issue does not remove an
entry here until the fix is merged and verified through the normal product
path.

| ID | Priority | Area | Problem | Acceptance condition |
| --- | --- | --- | --- | --- |
| AW-B001 | High | Content/gameplay | A fresh/default private world has no active starter package, leaving zero building and recipe definitions and no meaningful planning-provider work. | A new and existing private world receive a versioned starter content pack with useful materials, tools, shelter, storage, fire/workshop and crop/food recipes; planning candidates occur in normal play. |
| AW-B002 | High | Cognition/cost | Routine cognition can repeatedly pay for `safe_idle` when no meaningful observation changed. | Idle decisions are reused/coalesced until relevant state changes or a bounded reevaluation deadline; telemetry proves materially fewer no-op calls. |
| AW-B003 | Medium | Settings UI | “Inhabitant cognition” is visually dense and too wordy for a normal settings screen. | The panel uses compact role/provider/model/key rows, progressive help and clear saved/error state without losing the two-role distinction. |
| AW-B004 | Medium | Menu layout | With no inhabitant selected, the pause/menu content drops toward the bottom of the screen. | Menu position and layout remain stable regardless of world selection state and supported window size. |
| AW-B005 | Medium | Player observability | Provider decisions, fallback and usage are visible in safe server logs but not clearly in the game. | A compact in-game activity view shows provider role/model, accepted/fallback outcome and bounded usage/latency without exposing prompts, raw responses or secrets. |

## First repair increment

- **AW-B001 — implemented:** normal host ticks now stage a validated,
  versioned starter package for both fresh and old saves. Four buildings and
  three recipes lead to construction, production and household food pickup.
  A dependency-linked supplement adds stone/fiber/seeds and hearth/weaving/grain
  content; persistent projects acquire inputs and other inhabitants share
  requested materials. The ordinary 1,000-tick settlement regression covers
  three-input gathering, sharing, completion, eating and public gratitude.
- **AW-B002 — implemented:** persisted idle observation keys prevent repeated
  calls for unchanged choices; a 300-tick deadline bounds reuse. A regression
  test proves four calls remain four through 61 ticks and a reload.
- **AW-B003 — implemented:** compact controls, progressive help, and explicit
  world/inhabitant target selection retain both cognition roles.
- **AW-B004 — implemented:** container-owned centering replaces cached-height
  placement. Godot checks selection and settings toggles at three window sizes.
- **AW-B005 — implemented:** selected-inhabitant cards show accepted provider,
  action and fallback; tooltips show model, role and available usage/latency.
  Existing server logs remain the detailed operator diagnosis path.

These are implementation evidence, not a claim that Living Settlement's whole
acceptance gate is complete. Live migration remains deferred while the owner
keeps the existing world manually paused.

## Runtime audit issues

The following confirmed GitHub reports are tracked here as well; their issue
pages retain reproductions and regression requirements.

| Issues | Status in implementation | Defect |
| --- | --- | --- |
| [#107](https://github.com/compoodment/AgentWorld/issues/107) | Fixed; activation and rollback regressions | Dependency quarantine/activation order; active dependents must be rolled back first |
| [#108](https://github.com/compoodment/AgentWorld/issues/108) | Implemented; archive/restart and stale-cursor regression coverage | Unbounded checkpoint event history and rewrite cost |
| [#109](https://github.com/compoodment/AgentWorld/issues/109), [#116](https://github.com/compoodment/AgentWorld/issues/116) | Fixed; stalled-provider, pause, cancellation and concurrent-tick tests | Partial ticks and provider-held authoritative locks |
| [#110](https://github.com/compoodment/AgentWorld/issues/110), [#111](https://github.com/compoodment/AgentWorld/issues/111), [#112](https://github.com/compoodment/AgentWorld/issues/112) | Fixed; boundary and clock-advance regressions | Expired barter acceptance and reservation expiry |
| [#113](https://github.com/compoodment/AgentWorld/issues/113), [#114](https://github.com/compoodment/AgentWorld/issues/114), [#115](https://github.com/compoodment/AgentWorld/issues/115) | Fixed; atomic rejection and restore regressions | Asset ordering, conflicting shared charges and overflow |
| [#117](https://github.com/compoodment/AgentWorld/issues/117), [#118](https://github.com/compoodment/AgentWorld/issues/118) | Fixed; projection and restore regressions | Missing crop jobs and empty-population observation crash |
| [#119](https://github.com/compoodment/AgentWorld/issues/119) | Fixed; declared/chunked/misreported size tests | Unbounded provider response buffering |
| [#120](https://github.com/compoodment/AgentWorld/issues/120) | Fixed; 600 signed polls with no authority writes | Idle authority write amplification |

## Register maintenance

- Record only reproduced defects or demonstrated product gaps.
- Put speculative features on the [roadmap](planning/roadmap.md), not here.
- Never store credentials, private world contents or raw provider payloads in a
  bug report.
- When a fix lands, update the affected canonical status/roadmap document in
  the same change and preserve detailed evidence in tests or the relevant
  implementation ledger.
