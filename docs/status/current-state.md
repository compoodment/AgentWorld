---
title: Current Product State
type: product-status
status: active
updated: 2026-09-23
---

# Current product state

This is the canonical answer to **what AgentWorld actually does today**. It
describes the default private-world runtime and Godot client, not only schemas,
contracts or isolated fixtures.

## Status vocabulary

- **Playable** — connected to the default private world and observable or
  controllable through the normal Godot client.
- **Integrated but thin** — participates in the live save/runtime, but does not
  yet form a rich or recurring gameplay loop.
- **Verified primitive** — implemented and tested in a bounded fixture or API,
  but not meaningfully connected to normal play.
- **Planned** — accepted direction without the required implementation.

“Implemented” never means “the type exists.” The player must be able to
encounter the behavior through the normal game path before this document calls
it playable.

## Playable path

The supported product path is an unsigned Windows 11 x64 Godot client paired to
one private headless .NET host. The paired client observes the complete world
and submits signed requests. The server validates and commits all state.

The host remains reachable continuously, but simulation ticks and hosted-model
calls run only while at least one authenticated client has a current presence
lease. The last disconnect closes the gate after five seconds. Reconnect does
not simulate offline time or clear an explicit manual pause.

The static web assets retained by the HTTP host are legacy protocol-diagnostic
infrastructure. They are not a supported game client or an alternative owner
interface.

Provider work runs against an isolated proposed tick, so owner observations and
pause remain responsive. Cancellation or intervening owner changes discard the
proposal without partial world mutations. In-flight provider work is cancelled
on manual pause or client lease expiry.
Save schema 10 preserves optional biological life-clock anchors, parenthood preparation, practical lessons and council policy/ballots alongside survival conditions,
fuel deadlines, work projects and additive settlement resources;
the schema-4 history mechanism bounds hot histories
and archives older events with verified hashes; reconnect explicitly resets
stale cursors. See [persistence and backup requirements](../planning/architecture.md#persistence-and-replay).
Signed polling uses process-local one-use challenges rather than rewriting
the authority file; old challenges fail closed after restart.

## Capability matrix

| Area | Status | What exists now | Important limitation |
| --- | --- | --- | --- |
| World host and persistence | **Playable** | Persistent private save, event stream, restart recovery, manual pause and client-presence gate | No player-facing save selection or migration UI |
| Owner security | **Playable** | Windows device key, host-approved pairing, signed/replay-resistant owner requests, revocation and origin pinning | Private single-owner model only |
| Map and movement | **Playable** | Seeded tile map, deterministic routing, occupancy, contention, adjacent resource interaction and animated client movement | Small fixed first-world presentation; no broad exploration loop |
| Survival | **Playable** | Hunger, energy, warmth, exposure illness/recovery, fresh food, source-based diet variety, sleep, clothing, fuelled heat, shelter and bedding | Illness and monotonous diets increase fatigue rather than causing mortality; balance is still early-alpha |
| Observation and control | **Playable** | World view, inhabitants, needs, intentions, inventories, relationships, events, pause/resume and suggestive/must-do instructions | UI remains early-alpha and some diagnostics are operator-only |
| Cognition | **Playable** | World defaults and per-inhabitant provider/model overrides; Jev for routine and OpenAI/Ollama Cloud for planning; validation, fallback, retry, safe logs and selection-card telemetry | Legal planning covers projects, material help and barter, not free-form social reasoning |
| Inventory and ownership | **Playable** | Carried items and shared stores, gathering wood/stone/fiber/seeds, material requests, household sharing and food pickup | Negotiated barter and a broader economy remain incomplete |
| Buildings and production | **Playable** | Persistent acquisition/work projects; hearth fuel, shelter insulation, storehouse preservation, bedding rest, clothing insulation and tool work-speed benefits | Equipment durability, repair and sophisticated logistics remain incomplete |
| Trade and economy | **Integrated but thin** | Inhabitants offer personal surplus for needed items; each party independently accepts or refuses through planning cognition. Expiry/cancellation releases reservations; exchanges leave visible public memories | One-for-one barter, not negotiated pricing or an autonomous currency economy; opportunities depend on actual personal surplus |
| Relationships and households | **Integrated but thin** | Households, cooperation memories, majority-voted food access, steward succession, practical apprenticeships and independently accepted/refused adult partnerships with unilateral withdrawal | Rich conflict and quantified trust remain incomplete; learning currently grants basic builder/farmer roles |
| Family, aging and death | **Integrated but thin** | Separate parental consent, preparation, dependent infants and actual caregiver food/warmth delivery; infants have no paid cognition. Household adults can volunteer to replace lost carers; older dependents accept/refuse independently. Pause-only Life pace settings optionally make biological years last one world day or a quarter-day, preserving existing ages and historical dates. Lifecycle, estates and inheritance remain in the society runtime | Calendar aging remains the default; legal guardianship and long-run intergenerational balance need further work |
| Ecology and weather | **Integrated but thin** | Renewable resources, seasons and weather affect warmth, fuel demand, illness, crop food yields and travel fatigue | Broader ecosystems, drought/flood damage and long-run tuning remain incomplete |
| Factions, law, currency and culture | **Integrated but thin** | A household council can change shared-food access by majority vote; persistent faction/currency/culture contracts exist | Broader institutions, currency circulation and contested law remain incomplete |
| Content governance | **Integrated but thin** | Canonical data-only packages and bundled starter content; validation, approval, staging, activation and unused-package withdrawal into quarantine | No friendly player proposal/approval workflow; committed references require an explicit migration before removal |
| Asset governance | **Verified primitive** | Provenance, rights metadata, quotas, cache/reservation accounting, preview contracts and artifact envelopes | No end-to-end creator/approval experience and no production art pipeline |
| Client presentation | **Playable** | World-first view, shared stores, project phases/blockers, social notes, provider activity, compact per-inhabitant settings and centered menu | Prototype visuals; no dedicated economy/project management screen |
| Multiplayer/public worlds | **Excluded** | Single-player only by owner decision | Multiple paired owner devices are not multiplayer |
| Executable generated mods | **Planned/disabled** | Data-only packages are fail-closed | No sandbox has been selected; arbitrary generated code does not run on the host |

## Current cognition behavior

The server generates a bounded observation and legal candidate list. Provider
output can choose one candidate; it cannot invent a world mutation.

| Role | Options | Typical current work |
| --- | --- | --- |
| Routine survival | Deterministic, Jev | Eat, sleep, gather, move, wear clothing, tend fire, seek warmth or idle |
| Planning and work | Deterministic, OpenAI, Ollama Cloud | Projects, material help, barter, household policy choices and apprenticeship requests/acceptance/refusal |

The host stages the built-in starter package through the validated content
registry on the first client-present, unpaused tick. It activates at the tick
boundary and supplies legal building/recipe choices. This also works for old
saves; already registered, rolled-back or quarantined starter packages are not
silently reinstalled. A manually paused save is not migrated merely by starting
the service.

A dependency-linked settlement supplement adds hearth/weaving/grain content
and stone, fiber and seed sources on free, passable cells. It never replaces
the starter package or silently reactivates quarantined content. Map restore
verifies both the original generator output and the bounded registered resource
additions. Work projects persist their phase, accumulated work and production
job; urgent needs interrupt work, and missing materials create requests that
other inhabitants can help fulfil. Blocked projects become eligible for
reconsideration after 60 ticks. Food production feeds ordinary eating. Carried
tools double work progress; clothing reduces exposure; shelter and bedding
improve rest. Hearths consume wood for 120 ticks of heat. Critical exposure
interrupts projects, and warmth plus food permits illness recovery. Food decays
without affecting non-perishables, and storehouses halve household decay.
Production stock targets reduce surplus equipment work. Snow halves crop food
yield, storms retain three quarters, and bad weather adds travel fatigue.
Food provenance distinguishes foraging, crops, cooked meals and camp rations;
inhabitants prefer a different available source, while monotonous diets reduce
their diet score. Low scores add fatigue. Spoiled reserved ingredients cancel
the affected production job and release its remaining inputs rather than
stalling the world. These are bounded first survival rules, not a finished
nutrition/health/ecology model.

Worker death cancels unfinished production and crop jobs before the next
completion pass, releasing unused reservations without undoing completed work.
The operator journal records `production_cancelled` with tick, job and worker
IDs, never private inhabitant text. Regression coverage includes death exactly
at the completion boundary and save/reload afterwards.

Resource markers display authoritative stock/capacity and stable hover help
for finite deposits or seasonal regrowth. Legacy hosts without these optional
fields show unknown details rather than fabricated quantities. Engine checks
exercise hover, stock refresh and marker removal alongside menu geometry.

Content rollback is conservative: placed buildings, production/crop history
and recorded settlement projects block removal before any mutation. Unused
packages can still enter quarantine without cancelling unrelated work. The
signed endpoint reports a conflict for referenced content; `content_rollback`
logs contain only tick, package ID and outcome. A reference-preserving conversion
or executable-content suspension workflow is not implemented by this guard.

An additive forestry package supplies managed coppice: two seeds and a fertile
plot are committed for 1,440 world ticks, producing 24 wood and two replacement
seeds. This competes with food cultivation and leaves depleted wild timber
depleted. Activation waits for an unpaused tick and an active settlement
package; rollback is respected across restart. Rest chooses reachable shelter
or bedding, with slower outdoor recovery when access is blocked; outdoor sleep
does not remove weather exposure. Shared-food choices require reachable pickup.
Workers retain a valid current building site, and pending teaching requests
give busy mentors a decision point without forcing acceptance.
Descendant building IDs use a stable canonical hash when society IDs contain
separators; existing founder-building IDs remain unchanged. The controlled
generation regression verifies birth, feeding, two save/reloads, adulthood,
earned training and completed construction with deterministic providers and
optional fast aging. This is a connected path, not population-balance proof.

Barter candidates require personal surplus and a useful different item held by
another inhabitant. Offers reserve one unit from each side for at most 120 ticks;
no ownership changes until both independently accept. Either party can decline
or withdraw. Spoilage cancels pending settlement safely, and a pair cooldown
prevents repeated requests. Pending decisions and completed exchange memories
appear in social notes; the host emits bounded `settlement_trade` outcomes.
This is a small barter loop, not pricing, currency circulation or measured trust.

After settlement activation, public contributions qualify a household steward.
The steward may propose reserving scarce shared food or reopening abundant
stores, but each adult member independently votes. A majority is required;
refusal or expiry retains the previous policy. The reserve rule blocks pickup
by members with hunger at least 4,500 only while shared stock is at or below one
serving per inhabitant; hungrier members retain access. It does not confiscate
personal food. The current rule persists when a living successor replaces a
departed steward. The Settlement panel shows leadership, policy and ballot
counts; `settlement_council` logs report bounded transitions, not free-form text.

Adult traders and unassigned adults can request basic builder/farmer training.
A practitioner in that role or a teacher must independently accept. Twenty
joint work ticks near camp grant the requested role, enabling its ordinary
building/crop choices. Food, energy and warmth needs interrupt progress;
refusal, cancellation, expiry or mentor death grants no role. Each mentor has
one active learner. Lessons survive pause/restart, appear on inhabitant cards,
and create public gratitude when completed. This is not a general skill tree.

Settings select either **World defaults** or a named inhabitant. Each role may
inherit its world default or override its provider/model. API keys remain in
the host credential store; removing a shared provider key also removes its
personal overrides. Personal assignments are signed, target-bound and durable.

Unchanged idle intentions are reused for up to 300 ticks, including across
reloads. Changed legal choices or urgent need bands trigger reconsideration;
an observation with only `safe_idle` does not need a provider call. The selected
inhabitant card shows the last accepted decision, with available usage/model/
latency details in a tooltip. Missing model/latency is shown as unavailable;
adapter token counts can be zero when the provider omits usage.

## Evidence boundary

The phase ledgers under `docs/implementation/` remain useful proof that
specific invariants and fixtures passed. They are not the current product
status. The repository's executable evidence is the merged code, automated
tests, Godot startup/export checks and exact-commit CI.

Future changes must update this document when a capability moves between
planned, verified primitive, integrated or playable status.
