# Changelog

All notable player-facing, world-simulation, save-compatibility, deployment,
and security changes are documented here. ClankerWorld has not published a
release yet.

## Unreleased

### Added

- Pause Menu → **Save World** now creates an unlimited named checkpoint of the
  paused world. Main Menu → **Load Save** lists those checkpoints, confirms a
  rewind, preserves the current state as a new checkpoint first, and opens the
  loaded world paused. Saves survive restart and retain per-agent model/key-slot
  choices without copying API-key secrets into world files.

- Freshly placed founders and later added adults can choose their own names in
  an accepted ordinary personal-model decision, without a separate naming
  request. If the player renames one first, a delayed model answer cannot
  overwrite that choice. Names persist with the world.

- The selected-agent card now lets the paired player rename an agent. The
  chosen name updates the visible world and family tree and survives reload;
  biological identity and relationships do not change.

- After the four-founder start, the top bar now offers **Add Agent**. Choose a
  provider, model, and saved or new API key, then click an empty passable tile.
  The adult joins the running world in a separate one-person household and
  survives save/reload. Placement requires the paired owner and rejects
  occupied or impassable tiles; the current map has no property claims yet.

- The Windows client now starts at a Main Menu with Continue, Settings,
  connection/pairing and Quit Game. Quit to Menu pauses the host world; Continue
  returns to it. New World remains unavailable until generated worlds can be
  played; Load Save handles named checkpoints of the current development world.

- Building placement now has an explicit buildable-ground rule. The existing
  mountain tiles reject construction, including owner-authored placement;
  future peak terrain is also reserved as no-build ground.

- A fresh private world now opens with an empty two-household base camp. The
  paired player picks a provider, model and saved or new API key for each of
  four unrelated founders, then places them on empty map tiles. Progress is
  saved after every placement; time cannot resume until the player explicitly
  chooses **Start World**. Existing development worlds keep their state.

- In World Settings, an inhabitant can now use one personal provider/model for
  both daily and project decisions. Players can save multiple named API keys
  for the same provider and choose which one an agent uses. Keys remain in
  private host storage, never in the world save or owner status. Existing
  provider settings migrate without dropping their saved credentials. The
  selected agent's card also opens those model/key controls directly.

- Fresh private worlds now start paused with six-minute days, a 40-day year and four
  10-day seasons. Agents born in those worlds become children at day 3, adults
  at day 15, elders at day 45 and cannot survive past day 60 from birth.
  Agent profiles show age in days. The prior development save was archived,
  and a fresh paused world was created to playtest this pace.

- World Settings now has a saved Jev assistance switch. Disabling it while
  paused keeps memories and credentials, invalidates older decisions, and
  routes work Jev would have handled to the agent's personal planning model,
  the world planner, or a local safe fallback. Jev can be re-enabled later;
  changing the setting writes a newer save format so older hosts cannot
  silently discard the choice.

- The top bar, World Info, agent histories and event log now format dates and
  time from the saved world's calendar pace. Game Settings offers DD-MM-YYYY,
  MM-DD-YYYY and YYYY-MM-DD display without changing world time. The old aging
  multiplier has moved out of World Settings into clearly labelled prototype
  developer tools; it is not the decided custom calendar.

- Agent profiles now open a separate Memories panel showing that agent's
  saved records, including private memories and memories retained after death.
  These records no longer masquerade as public social notes. Quit Game now
  asks for confirmation before closing the client.

- Agent profiles now show a small scrollable history of recent private thoughts
  written alongside accepted personal-model decisions. These thoughts survive
  saves and remain inspectable after death; routine fallback and hidden model
  reasoning are not presented as thoughts.
- Important out-of-view births, deaths and building proposals can raise brief
  notices that jump to their location or open the event log. Game Settings
  remembers notice choices by category without hiding full log entries.

- Game Settings now remembers a 24-hour or AM/PM clock preference on this
  installation. The top bar, World Info, settlement view, and event log use
  the same display choice without changing world time.

- An interactive Family Tree opens from an agent's card. It draws accepted
  parent–child and partnership links, keeps unrelated household members
  separate, and opens living or deceased relatives' profiles when clicked.

- Slow hosted agent-model calls no longer hold the entire world tick in the
  private-host build. Other agents and world systems advance while a request
  waits; pause/disconnect discard the external call, and saved unresolved
  decisions can be retried safely after reload. An agent card marks a queued
  decision so the wait is visible.
- The Events panel can jump to the recorded map location of an
  actor-associated event; global events remain informational.

- Deceased inhabitants now leave a saved, read-only record with their last
  location, age, role, relationships and death details. They remain inspectable
  from the inhabitant list after reload without appearing as living map actors.

- The Windows world view now supports mouse-wheel zoom, WASD/arrow and
  middle-drag panning, plus a top-left Map button. Its data-drawn overview
  marks the visible area and lets players click or drag to move the camera;
  this adds no second regional texture set. The top bar shows the living-agent
  count and opens a concise World Info panel; the pause menu separates current
  Game Settings from World Settings.

- Experienced builders can now propose bounded 1×1 shelter, storehouse or
  hearth designs through planning cognition. Completed building practice gates
  the choice; proposals are rate-limited, retain durable inhabitant authorship
  and enter Menu → Create as **proposed only**, labelled with their proposer.
  They never validate, approve,
  stage, activate or consume live materials without the owner's existing review
  steps.

- Added bounded directed trust earned from completed material help, mutually
  accepted barter and finished teaching. Trust survives restart, is visible on
  inhabitant cards, prioritizes familiar barter partners and remains required
  before proposing partnership. Refusal, withdrawal and disagreement do not
  reduce trust. Save schema 12 persists scores while projecting older
  cooperation memories without rewriting paused saves.

- Added bounded building, farming and crafting practice. Inhabitants earn one
  point only when useful work completes successfully; every ten points speeds
  the hands-on preparation stage without bypassing roles, materials or crop
  growth. Practice persists in save schema 11 and appears on inhabitant cards.

- Added Menu → Create: a data-only building workbench for named shelters,
  storehouses and fuelled hearths. Isolated construction previews consume no
  live materials or paid cognition. Saved designs require separate validation,
  approval and staging; activation waits while paused. Used designs cannot be
  withdrawn destructively. Inhabitants can select and construct active designs.
- Constructed buildings now appear on the map with readable names, footprints
  and hover help instead of existing only in the server's building list.

- Package withdrawal now rejects committed building, production-history and
  settlement-project references until an explicit migration is available.
  A rejected request preserves the world; withdrawing unused content no longer
  cancels another package's work. Rollback outcome logs omit private reason text.

- Resource markers show remaining stock and capacity, with working hover help
  for finite deposits and seasonal regrowth. Markers persist across refreshes
  so updates no longer discard the hovered control.
- Unfinished production and crop jobs are cancelled when their worker dies,
  releasing remaining reserved inputs before completion can produce output.
  Finished work remains intact; operators receive a safe cancellation event.

- Added managed coppice: farmers reserve seeds and a fertile plot for a full
  world day, then harvest timber and replacement seeds. Exhausted wild wood is
  not refilled; food crops and forestry compete for the same growing space.
- Fixed crowded rest and food access: inhabitants choose reachable shelters,
  fall back to bedding or slower outdoor rest, and do not keep choosing blocked
  shared food over reachable alternatives. Outdoor rest retains exposure risk.
- Construction retains the worker's current legal site instead of chasing newly
  vacated earlier tiles. Busy mentors can respond to teaching requests before
  finishing their existing project.
- Descendant activity/condition logs preserve complete colon-bearing inhabitant
  IDs instead of truncating them or silently losing condition events.
- Descendants can now finish buildings: generated instance IDs are canonical
  stable hashes when society IDs contain separators. Existing founder-building
  IDs remain unchanged, so already-built structures are still recognized.
- Preserved sibling and direct-ancestor partnership exclusions after relatives
  die, including grandparents. Ordinary relationship commands can no longer
  revoke historical parentage or accept a fabricated parentage proposal.
- Connected replacement caregiving after a dependent loses all active carers.
  Adults volunteer; infants receive protected care without fabricated consent,
  while older dependents independently accept/refuse. Offers expire, either
  participant may withdraw, and replacement carers use the real feeding loop.
  The client shows missing care and readable caregiving decisions.
- Fixed household caregiver tracking when one of several care obligations ends;
  remaining dependents no longer lose the adult from the household projection.
- Added pause-only, signed **Life pace** settings: original calendar aging or
  opt-in generational aging. Changes preserve current ages, birth dates and the
  365-day world calendar; they affect future biological aging, not tick or model
  cadence. Faster modes bring adulthood, elderhood and mortality sooner.
- Persist biological clock anchors and newborn life dates in schema 10, show
  biological ages on inhabitant cards, and keep adult work/social choices
  unavailable to minors. Old saves remain unchanged until the owner opts in.
- Connected separate parenthood proposals and consent to delayed, atomic births
  when food, shelter and caregivers remain available. Withdrawal, separation or
  loss of a parent cancels preparation; partnership alone never creates a child.
- Added dependent infants with physical needs and actual caregiver food/warmth
  delivery. Infants make no hosted-model calls or adult work decisions; the UI
  shows age bands and family-plan status. Schema 9 preserves preparation across
  restart without changing an older paused save.
- Connected adult partnership proposals to independent acceptance/refusal and
  unilateral withdrawal. Prior cooperation opens a choice, not automatic
  consent; unanswered proposals expire and rejected pairs have a cooldown.
  Partnerships are visible in the relationship panel and do not create children.
- Added persistent practical apprenticeships: eligible adults request a builder
  or farmer role, a qualified mentor independently accepts/refuses, and joint
  lessons at camp earn the role. Hunger, exhaustion, exposure, cancellation and
  mentor death cannot silently grant completion.
- Show work roles and lesson progress on inhabitant cards, preserve training
  across pause/restart in schema 8, and record bounded lesson-stage telemetry.
- Added a persistent household council: demonstrated contributors become
  stewards, but food-allocation changes require independent majority votes.
  A scarce-food reserve protects hungry members' access; rejection retains the
  previous rule. Policies survive leadership succession and restart.
- Show the steward, active food rule and vote counts in the Settlement panel.
  Schema 7 stores council state and ballots; older paused saves remain unchanged.
- Connected bounded inhabitant barter: surplus-for-needed-item offers, separate
  planning-provider acceptance/refusal, expiry and unusable-item cancellation,
  with no transfer until both parties agree. Completed exchanges leave public
  memories; pending offers and social notes appear in the settlement UI.
- Fixed idle reuse hiding choices created earlier in the same tick by another
  inhabitant. Cached decision context now reflects the provider's observation,
  not the later world state at execution time.
- Kept clothing, fire tending and warmth-seeking decisions on the assigned
  routine provider rather than accidentally routing them to the planning model.
- Connected weather exposure to warmth and recoverable illness, with clothing
  insulation, shelter, fuelled hearths, better rest from bedding and faster
  project work with carried tools. Inhabitants collect equipment and seek heat.
- Added perishable-food decay, slower household decay with a storehouse, and
  fresh-only consumption/reservation selection. Production now observes stock
  targets instead of endlessly manufacturing equipment.
- Weather now changes crop food yields and travel fatigue. Inhabitants prefer
  varied food sources; diet quality survives transfers and affects fatigue.
- Cancelled production safely when reserved ingredients become unusable,
  releasing remaining inputs instead of repeatedly failing the world tick.
- Fixed builders repeatedly selecting occupied or unreachable sites. Urgent
  exposure still permits protective construction and clothing work, while
  interrupting unrelated projects; exposure changes invalidate idle reuse.
- Added visible warmth/illness/equipment state and bounded survival-transition
  logs. Save schema 6 retains survival conditions and fire fuel deadlines;
  older paused saves are unchanged and receive no retroactive spoilage.

- Added persistent settlement projects with material acquisition, travel, work,
  interruption/restart recovery and explicit blockers. Other inhabitants can
  fulfil material requests; successful help leaves inspectable public memories.
- Added a versioned settlement supplement with stone, fiber and seed sources,
  hearth/weaving buildings, bedding, clothing, meals and grain production.
  Existing worlds receive additive resources only after resuming; schema 5
  preserves projects and validates additions against the original seeded map.
- Show shared stores, project progress and cooperation memories in the normal
  world/inhabitant UI, with technical identifiers relegated to diagnostics.
- Added a Settlement toolbar panel and secret-safe `settlement_activity` logs
  for project transitions and fulfilled requests, without logging content text.
- Kept authoring revisions monotonic after event-history compaction.

- Bounded hot event histories with durable, hash-verified archive segments and
  explicit stale-cursor snapshot resets. Save schema 4 keeps global event IDs;
  backups must include the save's adjacent `.history` directory.
- Hardened ticks against cancelled or slow providers: observations and pause
  stay responsive, and cancelled/superseded ticks leave no partial world state.
- Cancelled in-flight provider work when the last client lease expires or the
  owner pauses, and rechecked client presence before committing a proposed tick.
- Capped hosted-provider response bodies at 256 KiB before JSON parsing.
- Corrected reservation/barter expiry, automatic release on society clock
  advancement, asset-charge conflicts/overflow, and prefix-ID ledger restore.
- Made dependency quarantine block subsequent content activation and enforce
  dependency-first activation order. Active dependents must be rolled back
  before their dependency, preventing an unrecoverable content graph.
- Included crop work in owner job projections and kept empty-population worlds
  observable after all inhabitants die.
- Eliminated idle authority-file rewrites: one-use challenges are process-local
  and fail closed across restart; paired identities and revocations stay durable.

- Added per-inhabitant routine/planning provider and model overrides, with
  explicit inheritance from world defaults and shared host-only credentials.
- Added automatic, versioned starter content activation on the first resumed
  client-present tick: shelter, storage, cooking fire, workshop, crops, meals
  and tools. Existing paused worlds remain unchanged until resumed.
- Connected household food pickup to movement, inventory ownership and eating.
- Added per-inhabitant accepted decision, fallback, model, token and latency
  telemetry to the selection card without showing prompts or keys.
- Reused unchanged idle decisions across ticks and reloads, with a bounded
  reevaluation deadline and immediate reconsideration when legal choices change.
- Compacted cognition settings and centered the game menu independently of
  inhabitant selection, with engine-level layout regression checks.
- Limited the product roadmap to single-player; multiplayer is out of scope.

- Added structured, secret-safe live observability for cognition provider
  calls, authoritative intention outcomes, usage, latency, and world lifecycle
  gates so private-world behavior can be diagnosed from host logs.
- Added a deterministic .NET 10 headless simulation with an integer world
  clock, atomic ticks, durable pause/resume epochs, bounded recovery, and a
  pinned PCG32 random stream.
- Added seeded world generation with canonical terrain and resource manifests,
  stable cardinal routing, deterministic multi-inhabitant movement, contention
  resolution, legal direct swaps, and route-cache invalidation.
- Added survival simulation for hunger, energy, harvesting, eating, sleeping,
  renewable resources, and resource regeneration.
- Added canonical snapshots and event logs, schema migrations, save/reload,
  physical replay, compatibility checks, and digest proofs for deterministic
  world recovery.
- Added lot-based inventories with quantity, freshness, spoilage, reservations,
  ownership, capacity checks, atomic transfers, and exact barter settlement.
- Added durable world commands and messages with idempotency, stale-result
  rejection, and crash-safe processing boundaries.
- Added a private hosted world runtime with reconnectable observation, a
  browser diagnostic viewer, and a Godot client for normal play.
- Added owner-device pairing, signed owner requests, anti-replay protection,
  device management, and owner-only controls for pausing, resuming, and giving
  inhabitants instructions.
- Added deterministic cognition with bounded legal choices, validated
  responses, retries, fallbacks, usage accounting, provider-outage pausing,
  and restart-safe pending decisions. Supported adapters include the local
  deterministic provider, Jev, OpenAI-compatible providers, and Ollama Cloud.
- Added player-managed hybrid cognition settings. A paired owner can assign
  Deterministic or Jev to routine survival decisions, assign Deterministic,
  OpenAI, or Ollama Cloud to planning and work, and save, replace, or forget
  each provider's API key directly in the game.
- Added independently scheduled inhabitants with persistent identities, roles,
  needs, skills, intentions, inventories, and per-inhabitant cognition
  configuration.
- Added typed, consent-aware relationships, households, caregivers, social
  interactions, and atomic person-to-person trade.
- Added family and mortality simulation including birth, aging, death,
  tombstones, estates, inheritance, and household continuity.
- Added a four-inhabitant private world that saves and restores movement,
  needs, inventories, relationships, cognition, production, and world events.
- Added ecology and weather state, factions and laws, currencies, cultural
  state, and deterministic chunk manifests to the private-world simulation.
- Added governed data-only content packages with canonical IDs, semantic
  versions, dependency locks, validation, owner approval, staging, activation,
  rollback, and quarantine.
- Added typed content definitions for materials, buildings, recipes, and
  production, with deterministic preview and validation before activation.
- Added deterministic building placement, production jobs, ingredient and
  asset reservations, workstation checks, and inventory completion.
- Added asset governance with format and quota validation, rights and source
  metadata, provenance manifests, canonical package digests, deterministic
  cache accounting, previews, and portable artifact envelopes.
- Added public inhabitant intention and relationship summaries without
  exposing private model reasoning.
- Added inhabitant-chosen `build` actions. Inhabitants can independently choose
  valid structures or recipes; every generated world supplies reachable fertile
  land, and crop builds such as carrots complete into household inventory.
- Added a portable Windows 11 x64 game build and an automated verification
  pipeline that tests the simulation, starts the Godot client, exports Windows,
  and uploads the resulting artifact.

### Changed

- Completed the pre-release ClankerWorld rename across save/content identifiers,
  package and signature domains, Windows user storage/device-key names, and the
  systemd template and installation paths. Previous development saves and
  pairings are not compatible; the old installation is retained as a rollback
  backup, while provider credentials are carried into the fresh installation.

- Renamed the game, .NET projects, Godot client and Windows export to
  **ClankerWorld**.
- Changed private-world lifetime so simulation ticks and hosted-provider calls
  run only while at least one authenticated game client remains connected.
  Closing or losing the last client stops the world after a five-second grace
  period; reconnecting does not clear a manual pause or simulate offline time.
- Changed inhabitant cognition from one provider request per person per world
  second to bounded, persistent intentions. Inhabitants now carry out legal
  movement, rest, gathering, eating, and building work locally until the plan
  completes, becomes invalid, or reaches a scheduled reevaluation; hosted Jev
  decisions for different inhabitants are dispatched concurrently.
- Animated inhabitant movement between observed tiles and added compact
  activity markers and intention summaries, making travel and current work
  visible without exposing private reasoning.
- Changed hosted-provider configuration to a durable private-world setting.
  Provider-role and model changes take effect at the next cognition boundary
  while stale responses from an older configuration epoch are rejected.
- Changed urgent survival candidate generation to withhold strategic building
  work until hunger and exhaustion are out of the critical range.
- Replaced the inspector-style Godot shell with a world-first, responsive 16:9
  play surface. The world now fills the screen beneath a compact clock and
  weather HUD instead of sharing space with permanent developer panels.
- Moved the inhabitants roster and recent events into temporary popovers, and
  moved settings, display controls, and developer tools into the pause menu.
- Changed inhabitant inspection to a closeable card anchored near the selected
  person. No empty selection panel is shown before a person is selected.
- Replaced raw ticks with a player-readable `Day N · HH:MM` clock and filtered
  routine movement, cognition, and tick noise out of the recent-events view so
  it can focus on meaningful world events.
- Removed protocol versions, fixture IDs, provider state, authoring drafts, and
  other implementation language from ordinary play; diagnostics remain
  available under Developer tools.
- Made the integrated private world the intended playable runtime and isolated
  its save and pairing authority from the preserved one-person fixture used by
  diagnostics.
- Expanded the map from a fixture grid into layered terrain, resources,
  buildings, and selectable inhabitants with contextual inspection.

### Fixed

- An unavailable or low-confidence model now leaves its agent on the explicit
  safe-idle fallback instead of silently choosing the highest-priority legal
  action. Pending instructions are retained rather than marked completed by
  that fallback; repeated failure no longer pauses the legacy fixture world.

- Fixed the four-inhabitant world deadlocking around a single berry tile.
  Inhabitants now route around occupied tiles, interact with resources from an
  adjacent tile, prioritize critical sleep, and suppress repeated blocked-path
  noise while they yield or retry.
- Fixed survival gathering so renewable ecology produces carried food rather
  than trying to transfer a depleted household fixture lot. Regenerating
  resources are no longer treated as harvestable, and inhabitants stop
  stripping the patch while they still carry food.
- Fixed re-pairing after a world-authority change by exposing a `Pair again`
  action in Settings; players no longer need to find and delete client files to
  replace an obsolete saved registration.
- Fixed restart and replay edge cases across inventories, reservations,
  owner-control idempotency, cognition scheduling, stale provider responses,
  and durable command recovery.
- Fixed client selection and map-layer interactions so clearing a selection,
  selecting an inhabitant from either the map or roster, and reconnecting all
  produce the same observation state.
- Fixed the game viewport so it scales to the display without making the whole
  play screen scrollable or reserving a permanent right-hand sidebar.

### Security

- Stored paired-owner signing keys as non-exportable Windows CNG keys and
  required explicit pairing approval before owner capabilities are granted.
- Stored provider credentials in a separate service-account-only file with
  atomic replacement and `0600` Unix permissions. Keys are accepted only over
  signed paired-owner requests, are represented by a digest in canonical
  request bindings, and are never returned to the client or written to world
  saves, replay digests, observations, telemetry, or the Windows client.
- Kept content packages data-only and fail-closed: executable mods, invalid
  dependencies, unapproved assets, and over-quota packages cannot activate.
