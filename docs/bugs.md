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

## Rules

- Record only reproduced defects or demonstrated product gaps.
- Put speculative features on the [roadmap](planning/roadmap.md), not here.
- Never store credentials, private world contents or raw provider payloads in a
  bug report.
- When a fix lands, update the affected canonical status/roadmap document in
  the same change and preserve detailed evidence in tests or the relevant
  implementation ledger.
