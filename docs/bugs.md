---
title: Known Bugs and Product Gaps
type: defect-register
status: active
updated: 2026-09-23
---

# Known bugs and product gaps

This is the canonical durable list of confirmed current problems. GitHub issues
may track active implementation work, but closing an issue does not remove an
entry here until the fix is merged and verified through the normal product
path.

## Original reports and resolution evidence

- **AW-B020 — fixed:** rollback deleted placed buildings and production history
  belonging to the package, despite committed costs and outputs. Referenced
  packages now reject removal before any mutation; recorded settlement projects
  also block removal. A compatible conversion/migration workflow remains future
  work, not a hidden destructive fallback.
- **AW-B021 — fixed:** removing an unrelated unused package changed every
  remaining running production/crop job to cancelled. Unused withdrawal now
  preserves those jobs and reservations; regressions prove normal completion
  and restart afterwards.

- **AW-B018 — fixed:** household-owned production reservations outlived a dead
  worker, allowing unfinished crafting and crops to complete. Running jobs now
  cancel before completion and release remaining inputs. Six regressions cover
  both job types, completion-boundary death, already-finished work, safe logs
  and restart preservation.
- **AW-B019 — fixed:** map object labels ignored mouse hover and were recreated
  on every observation refresh. Resource/object labels now accept hover and
  retain identity across updates. An engine check verifies actual mouse entry,
  updated stock/help and removal of absent markers.

The reports below describe the original defects; their resolution is recorded
in the following repair evidence, not implied to remain open.

| ID | Priority | Area | Original problem | Acceptance condition |
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

## Survival integration regressions

- **AW-B006 — fixed:** production could reserve fresh ingredients that spoiled
  before completion, causing the tick to throw repeatedly. Completion now
  cancels that job, releases remaining reservations and emits
  `production_input_unusable`; the regression verifies subsequent ticks advance.
- Food availability, consumption and production input selection all exclude
  spoiled/ruined lots, so an older unusable lot cannot shadow usable stock.
- **AW-B007 — fixed:** construction could repeatedly select a site occupied by
  an idle inhabitant. Candidate sites now exclude other inhabitants and require
  a reachable route. An urgent-cold bootstrap regression proves protective
  construction and heating remain possible instead of permanent path retries.

## Runtime audit issues

- **AW-B017 — fixed:** descendant IDs contain
  colons, making directly interpolated building IDs invalid. Placement rejected
  after completed work. Noncanonical society IDs now produce stable hashed
  building IDs; existing canonical founder IDs retain their original mapping.
- **AW-B012 — fixed:** continuing project work
  could suppress a mentor's decision to answer a teaching request until expiry.
  Pending requests now interrupt project continuation for an independent response.
- **AW-B013 — fixed:** sleeping always targeted
  the first shelter, even if all access was occupied; unreachable shared food
  could outrank nearby berries. Rest now chooses a reachable shelter or bedding,
  with slower outdoor recovery if neither is accessible. Shared-food candidates
  require a reachable pickup point.
- **AW-B014 — fixed:** construction rescanned
  sites during work and could abandon a legal current site when another person
  vacated an earlier tile. The current legal site now takes precedence.
- **AW-B015 — fixed:** finite wild timber left
  later generations without renewable building/fuel inputs. A dependency-linked
  coppice package adds delayed cultivation without refilling depleted wild nodes.
- **AW-B016 — fixed:** activity/condition logs
  split descendant IDs at the first colon. Known complete IDs now resolve event
  ownership; private prose remains excluded.

Focused regressions cover mentor interruption, occupied shelters, outdoor rest,
current-site completion, additive forestry activation/rollback and descendant
condition logs. A controlled parenthood scenario with opt-in fast biological
aging verifies birth, real feeding, two save/reloads, adulthood, earned training
and completed construction within 12,000 ticks, using deterministic providers.
It is not a claim that arbitrary seeds or population growth are balanced.

- **AW-B010 — fixed:** partnership kinship checks discarded death-ended
  parentage and only checked direct parents, allowing siblings after parental
  death and missing grandparents. Historical parentage now supplies sibling
  and full direct-ancestor checks; runtime scenarios cover living/dead
  intermediate relatives and incorrectly revoked legacy parentage.
- **AW-B011 — fixed:** generic relationship revocation could revoke biological
  parentage despite the immutable-history contract. Revocation and acceptance
  of fabricated parentage proposals now reject without changing edges/births;
  proposal creation remains restricted to the birth transaction.

- **AW-B009 — fixed:** revoking one caregiver relationship removed the adult
  from household caregiver tracking even when another dependent still had an
  accepted care edge. Projection now retains the adult until the last relevant
  obligation ends; a two-dependent regression covers both transitions.

- **AW-B008 — fixed:** an idle decision could cache choices created by another
  inhabitant after its observation was taken, suppressing a response to a new
  offer. The cache is now bound at observation enqueue; barter tests prove the
  recipient sees the new choice on the next tick, including across restart.

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
