---
title: Current Product State
type: product-status
status: active
updated: 2026-09-26
---

# Current product state

This is the canonical answer to **what the ClankerWorld prototype actually does
today**. It
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

In the repository build, hosted model work now runs **between** world ticks.
The agent's unresolved decision remains in the saved cognition queue while
the world and other agents advance; urgent food, rest and exposure routines
can continue, but new projects wait for a valid answer. Pause, client absence,
provider changes and quit discard the external call, not the queued decision.
Late answers are admitted only against the same request/provider/run epoch and
a still-legal action. Deterministic decisions remain in the isolated tick.
The server build is installed on the live VPS. Its active world is now a fresh,
paused empty camp at tick 0 with no founders; the previous development save is
only a rollback backup. Pairing and provider credentials remain configured.
The matching Windows artifact has not been tested on computment's laptop; a
live model-wait playtest is still outstanding.

Save schema 17 preserves the per-world Jev switch and incomplete four-founder setup; bounded private thoughts from accepted personal-model decisions, deceased records, optional bounded directed trust and work practice alongside biological
life-clock anchors, parenthood preparation, practical lessons and council
policy/ballots, survival conditions, fuel deadlines, work projects and additive
settlement resources;
the schema-4 history mechanism bounds hot histories
and archives older events with verified hashes; reconnect explicitly resets
stale cursors. See [architecture](architecture.md#authority-and-failure-boundaries)
for the save and recovery boundary.
Signed polling uses process-local one-use challenges rather than rewriting
the authority file; old challenges fail closed after restart.

The event stream now stores a location for actor-associated events. The Godot
Events panel marks those entries as navigable and moves the camera to the
recorded event location when clicked. Out-of-view births, deaths and building
proposals can also raise a short notice; Game Settings controls those pop-ups
by category without removing the full event-log entry. Settlement notices are
configurable but await a settlement-founding event. The selected agent card
shows when a decision is queued and a scrollable history of recent private
thoughts supplied by accepted personal-model decisions. Archived thoughts and
saved social memories remain inspectable on deceased profiles without
continuing to update. Global events have no map destination.

## Capability matrix

| Area | Status | What exists now | Important limitation |
| --- | --- | --- | --- |
| World host and persistence | **Playable** | Persistent private checkpoint, pause and client-presence gate; signed Main Menu New World and Load World select independently saved worlds. Current-world named manual saves remain in the pause menu, with rotating autosaves controlled in World Settings. Provider/model/key-slot assignments and autosave choices follow each world; API keys and device pairing stay installation-local | New World currently offers Small/Medium, seed, wrapping and water share; climate controls, Large/Huge/Mega playable storage and player-local packaging remain unfinished. The active recovery checkpoint remains automatic even when rotating autosaves are off |
| Owner security | **Playable** | Windows device key, host-approved pairing, signed/replay-resistant owner requests, revocation and origin pinning | Private single-owner model only |
| Map and movement | **Playable** | Deterministic Small/Medium generated maps with an empty base camp, rivers/lakes/ocean, no-build mountain/peak terrain, four-founder setup, camera-visible terrain drawing and a draggable top-left overview. The older 6×5 world remains selectable. Generated terrain travels in compact row-major owner observations | Climate/surface/vegetation layers collapse into physical terrain; whole packed maps still resend every observation and saves still contain per-tile JSON. East/west wrap does not yet apply to all agent routes. Large/Huge/Mega exist only in compact geography generation |
| Survival | **Playable** | Hunger, energy, warmth, exposure illness/recovery, fresh food, source-based diet variety, sleep, clothing, fuelled heat, shelter and bedding | Illness and monotonous diets increase fatigue rather than causing mortality; balance is still early-alpha |
| Observation and control | **Playable** | World view, top-bar living-agent count and World Info, inhabitants, needs, intentions, inventories, relationships, located event-log jumps and out-of-view event notices, pause/resume, separated Game/World Settings panels and suggestive/must-do instructions; newly deceased inhabitants retain an inspectable last-state record across save/reload. The selected-agent card lets the paired player rename an agent without changing its identity or relationships. The family-tree panel links accepted parentage and partnerships, lets the player inspect living or deceased relatives, and excludes household-only links. Game Settings persists date-order (DD-MM-YYYY, MM-DD-YYYY or YYYY-MM-DD), 24-hour or AM/PM time display, and event-notice choices locally. Personal-model agents show a bounded scrollable private-thought history; a separate Memories panel shows up to 16 recent saved memories per agent, including private and deceased records. Quit Game requires confirmation | Historical records currently cover deaths after this archive is introduced, not earlier lost physical state; thoughts only exist for new accepted personal-model decisions, not past play or model-internal reasoning. Belief uncertainty, broad episodic memory and Jev-assisted compaction are not yet available. World Info shows current facts, not future ownership/capability discovery; settings contain only existing prototype controls and some diagnostics remain operator-only |
| Cognition | **Playable** | World defaults and one personal provider/model selection per inhabitant, applied to routine and planning decisions; independently named API-key slots allow different agents to use different keys for the same provider. The selected agent card opens these controls directly. New worlds require a personal provider/model/key for each founder. After starting, the top-bar Add Agent flow accepts a stored or new key and places an adult on an empty tile; the person and model assignment persist. Fresh founders and added adults may supply their own name with an accepted personal-model decision, without a separate call; a player rename takes precedence over a pending answer. A saved per-world, pause-only Jev switch, validation, fallback, retry, safe logs and selection-card telemetry also operate | The current map has no household property or settlement claim data, so each post-start player-added adult gets an unrelated one-person household; claim-based joining and border overlays are not active. A model that omits or fails to provide a valid name leaves the placeholder until a later decision or player rename. Jev has no memory compaction/retrieval role yet; legal planning covers projects, material help, barter and bounded building proposals, not free-form social reasoning |
| Inventory and ownership | **Playable** | Carried items and shared stores, gathering wood/stone/fiber/seeds, material requests, household sharing and food pickup | Negotiated barter and a broader economy remain incomplete |
| Buildings and production | **Playable** | Persistent acquisition/work projects; hearth fuel, shelter insulation, storehouse preservation, bedding rest, clothing insulation, tool benefits and bounded building/farming/crafting practice earned from completed work | Equipment durability, repair and sophisticated logistics remain incomplete |
| Trade and economy | **Integrated but thin** | Inhabitants offer personal surplus for needed items; each party independently accepts or refuses through planning cognition. Expiry/cancellation releases reservations; exchanges leave visible public memories | One-for-one barter, not negotiated pricing or an autonomous currency economy; opportunities depend on actual personal surplus |
| Relationships and households | **Integrated but thin** | Households, visible directed trust earned from completed cooperation, majority-voted food access, steward succession, practical apprenticeships, work practice and independently accepted/refused adult partnerships with unilateral withdrawal | Trust is bounded and affects barter-partner order and partnership eligibility; resentment, reconciliation and rich conflict remain incomplete |
| Family, aging and death | **Integrated but thin** | Separate parental consent, preparation, dependent infants and actual caregiver food/warmth delivery; infants have no paid cognition. Household adults can volunteer to replace lost carers; older dependents accept/refuse independently. Fresh host-created worlds start with an empty camp and require four unrelated founders before time begins. They use six-minute days, a 40-day year, and day-based stages: child at day 3, adult at day 15, elder at day 45, death by day 60 from birth. The card shows age in days. Freshly placed adults may choose their own name on an accepted ordinary personal-model decision; a player rename takes precedence. A pause-only prototype aging override remains under Developer tools | Initial personality/aspirations and stage-specific social capability remain thin; lifespan balance needs playtesting; legal guardianship and inheritance remain thin |
| Ecology and weather | **Integrated but thin** | Renewable resources and seasons persist. Generated worlds have deterministic 32×32-tile weather regions; weather affects agents' local warmth/clothing/fire choices, travel fatigue and crop yields at the field. The top bar and World Info follow camera-local weather | Rain does not yet build a soil-moisture history for crops; biome weighting, moving weather fronts and precipitation art remain unfinished. Broader ecosystems and balance also need work |
| Factions, law, currency and culture | **Integrated but thin** | A household council can change shared-food access by majority vote; persistent faction/currency/culture contracts exist | Broader institutions, currency circulation and contested law remain incomplete |
| Content governance | **Integrated but thin** | Menu → Create provides named shelter/storage/hearth designs, isolated construction preview, durable player and experienced-builder proposals, separate validation/approval/staging and unused withdrawal; active designs enter inhabitant planning | Bounded 1×1 buildings only; no general recipe/asset invention; committed references require explicit migration before removal |
| Asset governance | **Verified primitive** | Provenance, rights metadata, quotas, cache/reservation accounting, preview contracts and artifact envelopes | No end-to-end creator/approval experience and no production art pipeline |
| Client presentation | **Playable** | Startup Main Menu with Continue, New World, Load World, Settings, pairing and Quit Game. New World previews its deterministic terrain and marked camp, then creates a paused camp; founder setup and Start World happen in-world. The pause menu owns Save World/Load Save. In-world panels cover shared stores, work, social notes and agent settings | New World still lacks climate and deeper generation controls. Prototype visuals and no dedicated economy/project management screen |
| Multiplayer/public worlds | **Excluded** | Single-player only by owner decision | Multiple paired owner devices are not multiplayer |
| Executable generated mods | **Planned/disabled** | Data-only packages are fail-closed | No sandbox has been selected; arbitrary generated code does not run on the host |

## Decided-vision reconciliation

This is the current code audit against [decided finished-game behavior](vision-interview.md),
not a claim that all interview proposals should be implemented at once.

| Priority | Decided behavior | Current playable behavior / remaining work |
| --- | --- | --- |
| 1 | A slow or unavailable personal model does not stop unrelated agents or invent an important choice | The private-world host dispatches hosted decisions between committed ticks; unrelated agents and world systems keep advancing. Failed or low-confidence requests can select only `safe_idle`, never complete an instruction or invent a strategic choice. Pending decisions survive save/reload and stale answers after pause/provider changes are rejected. A live Windows/VPS model-wait playtest remains to be done. |
| 2 | New World creates a map and empty base camp; four configured founders are added in-world, then Start World begins time | The signed host and Main Menu now preview, create and select separately saved Small/Medium worlds. Generated worlds open paused at an empty camp; founder placement and explicit Start World are shared with the previous development world. The five named size presets and 64×64 geography foundation exist, but climate/deeper generation controls and scalable physical-map persistence are still missing; Large/Huge/Mega cannot be played yet. |
| 3 | Playtest a six-minute day and custom 40-day/four-season year, with at most six hours of life from birth | Fresh host-created worlds save 360 ticks/day, 40 days/year and four 10-day seasons; day-based stages and a hard 60-day lifespan use the same saved pace. The host still aims for one tick per real second, subject to load. The live VPS has a paused development world with this calendar; its former save is archived. The decided pacing and founder setup still need a Windows/VPS playtest. |
| 4 | Each agent owns a personal model and key; Jev is an optional per-world support layer | The selected agent card and World Settings can choose one provider/model and a named saved key for an inhabitant, applying to both routine and planning decisions. The founder setup uses that same private key routing before Start World. Several keys from the same provider can coexist in private host storage. The per-world Jev switch is saved and functional; Jev-assisted memory work remains unbuilt. |
| 5 | One continuous zoomable pixel-art world view with an always-available draggable overview and inspection controls | The Godot Main Menu offers Continue, New World and Load World when paired. The view has single-view zoom, camera-bounded terrain drawing, overview navigation, located events, notices, private thoughts and a family tree. Large playable maps, filters and rich belief/compaction memory remain unbuilt; art is prototype-quality. |
| 6 | Local Windows install runs the authoritative simulation and keeps saves/keys on that PC | The Godot client currently requires the private VPS host. Preserve it for development, then package the same simulation locally and prove a fresh install without the VPS. |
| 7 | Regional weather, bounded inventions and mods, cross-generation social life | Generated worlds now use local weather regions for survival, work and display; seasonal effects and data-only building proposals remain thin. Soil moisture/weather movement, the Mod Library, restricted scripted content, deep conversation/memory and full inheritance are not yet connected. |

## Current cognition behavior

The server generates a bounded observation and legal candidate list. Provider
output can choose one candidate; it cannot invent an undeclared world mutation.
Provider failure or low confidence now permits only the explicit `safe_idle`
fallback; without that candidate the request is rejected rather than choosing
a strategic action. A failed request cannot complete a pending
instruction. Hosted requests wait outside tick commits, so they do not stall
unrelated agents or world systems.

| Role | Options | Typical current work |
| --- | --- | --- |
| Routine survival | Deterministic, Jev | Eat, sleep, gather, move, wear clothing, tend fire, seek warmth or idle |
| Planning and work | Deterministic, OpenAI, Ollama Cloud | Projects, material help, barter, bounded building proposals, household policy choices and apprenticeship requests/acceptance/refusal |

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

After three completed buildings, a builder may choose among bounded shelter,
storehouse and hearth proposals. Practice can reduce the ordinary wood cost by
at most three units; size, effects and allowed tags remain the existing safe
workbench format. Each author may propose a purpose once, with at least 300
ticks between different ideas. The content registry keeps the author as a
durable governance event, while safe host logs contain only inhabitant and
package IDs. The proposal appears in Menu → Create with the proposer's readable
name for the same isolated review
and construction preview as a player design. It remains `proposed`: only the
owner can validate, approve and stage it, and pause still blocks activation.

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
This is a small barter loop, not negotiated pricing or currency circulation.

Completed material help, mutually accepted barter and finished teaching now
increase bounded directed trust. The client shows each positive score; barter
opportunities prefer trusted partners and partnership proposals require prior
trust. Existing public cooperation memories project their old meaning until a
new event persists schema 12. Refusal, withdrawal and policy disagreement do
not reduce trust. The world still lacks justified harm, resentment and
reconciliation mechanics, so this is a positive-cooperation consequence rather
than a complete relationship simulation.

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

The selected agent card opens an in-place model/key editor; World Settings can
also select either **World defaults** or a named inhabitant. World defaults
retain separate routine and planning roles; a named inhabitant selects one
personal provider/model for both roles or inherits the defaults. A hosted agent
can use the provider's default key, an existing named key, or add a new named
key for that same provider. Keys remain in installation-local host storage;
only non-secret slot labels and IDs are returned to the owner. Removing a
provider's default key removes only assignments that depended on that default;
named-key assignments remain. Personal selections are signed, target-bound
and durable.

Unchanged idle intentions are reused for up to 300 ticks, including across
reloads. Changed legal choices or urgent need bands trigger reconsideration;
an observation with only `safe_idle` does not need a provider call. The selected
inhabitant card shows the last accepted decision, with available usage/model/
latency details in a tooltip. Missing model/latency is shown as unavailable;
adapter token counts can be zero when the provider omits usage.

## Evidence boundary

The repository's executable evidence is the merged code, automated tests,
Godot startup/export checks and exact-commit CI. Retired phase documents are
available in Git history, not active status sources. The
[vision ledger](vision-interview.md) describes the intended finished game,
which is not interchangeable with this prototype capability matrix.

Future changes must update this document when a capability moves between
planned, verified primitive, integrated or playable status.
