---
title: AgentWorld Decision Register
type: decision-record
status: active
updated: 2026-09-19
---

# Decision Register

This is the current authority for accepted first-world product decisions. It is
not an interview transcript: chronological rationale belongs in the
[design log](design-log.md), while genuinely unresolved work belongs in
[open questions](open-questions.md).

Change a decision only through an explicit update with the reason, prototype
evidence where applicable, and a matching design-log entry.

## Batch 1 — closed decisions

The first design interview established these decisions:

- The human is an **observer-director**. They observe by default and issue
  suggestive or must-do instructions through an instruction interface, not a
  conversational chatbox.
- Persistent world directives and direct broadcasts are separate from
  individual instructions. Directives apply immediately and to inhabitants
  added or born later.
- Must-do instructions override the selected inhabitant's priorities, values,
  and relationships, but still obey physical reality and cannot dictate another
  inhabitant's response.
- The human sees the complete map and world state by default. Fog of war is not
  a planned feature.
- The world uses a 365-day in-world calendar, four simple seasons, and a
  provisional four-real-minute day: about 2:40 daylight and 1:20 night. The
  timing is configurable after playtesting.
- The world continues while unpaused and unattended. Model calls may continue
  and incur provider cost; provider/account limits belong to the human's
  provider setup. Game-side cognition ceilings are deferred rather than an
  initial promise.
- The prototype starts from a seeded, procedurally generated world with a
  living ecosystem. A small temperate biome is enough for the first test;
  richer terrain and continents remain part of the larger concept.
- The human can use paused authoring mode to edit terrain, resources,
  ecosystem objects, and approved assets before or during a world.
- Inhabitants have bounded personal knowledge. They can remember visited tiles,
  objects, landmarks, resources, and routes and use that knowledge later,
  while the human remains omniscient.
- Children are LLM-influenced from birth with age-appropriate cognition. The
  world setup offers a provider policy of per-child selection, parent
  inheritance, world default, or a hybrid.

## Batch 2 — closed decisions

The second design interview established the movement, perception, and basic
cognition contract:

### Movement and travel

- Movement is logically tile-backed. The client may animate an inhabitant
  smoothly between tiles, but the simulation owns the authoritative position.
- The LLM chooses a destination and broad travel intention. The simulation
  calculates, validates, and executes the route.
- Terrain, roads, health, and transport affect travel time in the prototype.
- Inhabitants remember meaningful landmarks and destinations, with lightweight
  route memories rather than a giant transcript of every visited tile.
- A blocked or dangerous route becomes an event that can cause the inhabitant
  to reconsider.

### Perception and cognition

- The current tile is always known, including its material, occupants, objects,
  and relevant effects.
- Local observations may include nearby visual surroundings, messages, changes
  to known locations, danger, urgent needs, body/task state, time and season,
  nearby weather, and direct interactions.
- All meaningful event categories may wake cognition: urgent needs, danger,
  messages, discoveries, failed actions, human instructions, project
  milestones, and relationship events.
- Repeated small events are coalesced into one observation rather than causing
  one model call each.
- During normal model latency, an inhabitant continues its last valid
  intention. Urgent danger or survival problems use deterministic emergency
  behaviour. Without a valid intention or safe fallback, it stops safely.
- Must-do instructions do not immediately interrupt sleep, travel, or crafting;
  they wait in the instruction queue until the next valid decision point.

### Sleep and starting conditions

- An inhabitant decides when to sleep and may sleep wherever it is, including
  an unsafe location. Low energy slows tasks and walking; sustained exhaustion
  eventually causes health damage.
- A bed improves energy recovery, and proper shelter improves recovery and
  protection from exposure. The exact safe/unsafe test remains a later tuning
  question.
- The prototype uses a camp-start preset; Batch 3 defines its starting shelter,
  bed or bedroll, storage, tools, food, and fire/cooking setup.

## Batch 3 — closed decisions

The third design interview established the survival, provider, authoring, and
founder contract:

### Survival and camp start

- Inhabitants decide when to sleep. Low energy slows tasks and walking;
  sustained exhaustion eventually causes health damage.
- An exhausted inhabitant may sleep wherever it is, including an unsafe place.
  The exact meaning of safe or unsafe sleep remains a tuning question rather
  than an excuse to prevent sleep entirely.
- Night makes sleep and energy recovery more relevant without imposing one
  universal schedule.
- Camp start provides basic shelter, a bed or bedroll, storage, basic tools,
  starting food, and a fire/cooking setup.

### Cognition and provider behaviour

- Meaningful changes may cause an inhabitant to reconsider an ordinary plan;
  the prototype will measure the right coalescing interval instead of fixing a
  number prematurely.
- A prolonged individual model failure eventually switches to simple local
  behaviour. Emergency behaviour is influenced by personality and learned
  habits.
- A provider-level outage pauses the game and notifies the human.
- The human chooses provider, model, personality, and skills when creating an
  inhabitant, and may change provider or model after world start.
- Optional game-side cognition ceilings are deferred. Provider/account limits
  remain the initial cost boundary.

### Authoring and founders

- Paused authoring may edit terrain, rivers and water, resources, trees and
  plants, buildings, inhabitants, weather and seasons, and approved assets.
- Creating resources from nothing is allowed but is recorded as an explicit
  god-mode intervention.
- Human-created assets may be used after format and performance checks; new
  behaviours and rules require deeper validation.
- The first camp contains multiple unrelated founders.

## Batch 4 — closed decisions

The fourth design interview established the first routing, sleep, fallback,
provider, and family contracts:

### Travel and world scale

- The first map uses deterministic tile-grid pathfinding, A*-style routing, and
  reusable route caches.
- Roads lower movement cost and are preferred when they are faster. Relevant
  terrain, roads, or obstacles invalidate cached routes.
- Rivers, mountains, and coastlines are impassable in the first prototype.
  Boats, climbing, and other traversal systems are later additions.
- Blocked routes, danger, urgent needs, must-do orders, and irrelevant
  destinations may all interrupt travel at the next action boundary rather
  than halfway through an atomic movement step.
- Carried weight and weather do not affect travel in the first prototype.

### Sleep and cognition

- Safe sleep means stable ground, no severe environmental exposure hazard, and
  no immediately active predator or hostile threat. Lack of a bed or shelter
  worsens recovery but does not prevent sleep.
- Unsafe sleep can risk injury, illness, or death depending on the hazard.
- The observation always includes the current tile, the inhabitant's own body
  and urgent needs, its current task/intention/destination, and immediate local
  perception. Messages, memories, route details, weather, and known-location
  changes are included when relevant.
- Event coalescing and cognition timing will be tuned experimentally and
  monitored during prototype operation.
- Ordinary instructions wait until the current small atomic step finishes;
  urgent danger and survival conditions may interrupt sooner.
- The human-facing developer view exposes the full structured decision record:
  observation fields, retrieved memories, event triggers, model output,
  selected intention/action, and fallback reason. It does not pretend raw
  hidden chain-of-thought is a reliable game state.

### Fallback behaviour

- Deterministic fallback may eat when hungry, seek shelter, flee danger, sleep
  when exhausted, continue routine work, or return home.
- Personality and learned habits influence fallback priorities and risk
  tolerance.

### Providers and family

- The human configures the world's provider and model registry, selects a world
  default, and may override or change provider/model per inhabitant.
- Ollama means **Ollama Cloud through its API** in this project; local model
  inference is not a target.
- The previously decided newborn provider policy remains: per-child selection,
  parent inheritance, world default, or a hybrid.
- Birth and child growth are included in the first playable society.
- Children inherit biologically grounded traits, personality tendencies, initial
  skills, and culture, but not raw parental memories.
- Adoption is deferred and not part of the initial family model.
- There is no fixed numeric population cap. Food, housing, care, and actual
  simulation/provider capacity determine what is sustainable.

## Batch 5 — closed decisions

The Batch 5 policy decisions are settled. Their numerical constants and exact
formats are prototype-tuning or engineering work, not a reason to reopen the
product-policy batch.

### Batch 5A — accepted prototype defaults

The first survival and cognition pass accepted the worksheet's baseline:

- Fatigue is moderate: it slows work and walking progressively, with bed and
  shelter improving recovery; sustained exhaustion damages health.
- Unsafe sleep is meaningfully risky but not arbitrarily lethal. Severe
  exposure, repeated neglect, or already-poor health can make it deadly.
- Cognition uses the middle-ground cadence: immediate deterministic emergency
  response, short coalescing for urgent events, longer batching for normal and
  background events, and a cooldown unless something important changes.
- Observation attention is filtered deterministically by urgency, current
  goals, recency, relationships, novelty, and redundancy rather than dumping a
  large raw memory context into the model.

The numerical constants remain prototype tuning targets and may change after
measurement without reopening these policy decisions.

### Batch 5B — accepted prototype defaults

The telemetry, routing, and provider pass accepted these defaults:

- Developer saves retain full structured decision telemetry and raw structured
  model responses; ordinary saves may retain summaries and hashes.
- Route caches are rebuildable performance data and are discarded/rebuilt on
  load rather than treated as authoritative world state.
- The first user-configurable hosted providers are Ollama Cloud API and OpenAI
  API. A deterministic mock provider remains part of the test harness.
- Provider/model changes apply at the next cognition boundary. The current
  atomic action continues, and stale in-flight responses cannot overwrite the
  new configuration.

### Batch 5C — accepted family and mortality defaults

The family and population pass accepted these defaults:

- Relationships are a typed graph rather than one universal family label.
  Household membership is separate from biological, social, or legal ties.
- Adulthood is hybrid: kernel age bands govern protected rules, while cultures
  may add ceremonies, apprenticeships, and social recognition.
- Children rely on caregiver networks. Birth readiness considers consent,
  health, food, sleeping space, care capacity, and safety, without a global
  population cap or default human approval gate.
- Death is an authoritative, recorded kernel transition. Future world rules or
  mods may add constrained resurrection, but revival creates a new explicit
  event/state and cannot erase the historical death.

### Batch 5D — accepted credentials, model, and asset defaults

The credentials and content pass accepted these defaults:

- Credentials stay outside world saves. Worlds reference opaque provider
  configuration IDs, and secrets never appear in saves, logs, prompts, or
  telemetry. Assignment uses preflight validation and preserves the last
  known-good assignment when validation fails.
- Reliable structured JSON actions are the only required model capability for
  the first prototype. Tool calls, vision, streaming, and unusually large
  context windows remain optional extensions.
- The first asset contract uses inert PNG files plus JSON manifests on a
  16×16 or 32×32 world-grid scale. Dimensions, anchors, frames, collision,
  draw cost, and isolated previews are validated before acceptance.
- Cultures may develop distinct visual styles. Shared readability rules and
  strict provenance, performance, and replacement-history checks matter more
  than a single universal palette.

### Batch 5E — accepted replay, traversal, and failure defaults

The runtime resilience pass accepted these defaults:

- Developer replay uses versioned JSON decision records containing the tick,
  inhabitant/world IDs, triggers, filtered observation, memory references,
  provider/model/config epoch, structured response, validation, selected
  action or fallback, resulting event IDs, and timing/token metadata. Replay
  uses recorded outputs and does not call a live provider by default.
- Future traversal modes share a deterministic route interface. Walking and
  roads are first; later routes may compose segments such as walking to a dock,
  travelling by boat, and walking onward. Boats are excluded from the first
  prototype.
- A hosted-provider failure gets one bounded retry, then deterministic fallback
  behaviour. The last known-good assignment remains active, the failure and
  fallback are recorded, and the current atomic action continues safely.

### Survival and cognition tuning

The policy questions in Batch 5 are now closed. Exact serialization formats,
retention limits, replay UI, and traversal-specific mechanics remain
engineering work rather than unresolved world-design choices.

## Batch 6 — closed decisions

### Batch 6A — accepted kernel and modding boundaries

- The kernel protects identity and lifecycle, time and causal ordering,
  authoritative state mutation, spatial validity, ownership and transactions,
  consent and protected biological rules, death, persistence, and mod quotas.
- World rules and content may add or tune weather, needs, cultures, economies,
  species, buildings, professions, events, objects, items, recipes, and
  visual/narrative material. They submit validated proposals rather than
  directly rewriting protected state.
- Each world may choose a constitution or rule package at creation, while
  kernel safety and causality remain mandatory.
- Data-only content can be auto-approved; declarative rules require explicit
  world-owner enablement; executable code, network, filesystem, and host
  integration are disabled by default and require stronger review and
  isolation. This distinction applies both to outside contributors and to
  packages installed on a hosted world.

The current recommendation for future executable mods is a capability-limited
WebAssembly sandbox, but that technology choice is still provisional. A private
fork can change host code directly; that is separate from the installed-mod
contract.

### Batch 6B — accepted mod package and safety defaults

- Mods use versioned packages with manifests, dependencies, declared
  capabilities, compatibility ranges, optional declarative rules, and
  migration/rollback metadata. Private packages may be loaded from a local
  folder, archive, or repository without requiring a fork.
- New mods apply only at world creation or during an explicitly paused,
  previewed migration. Active worlds do not change silently.
- Updates checkpoint the world, test in an isolated copy, run compatibility and
  representative simulation checks, and retain rollback metadata.
- Kernel quotas limit entities per tick, event queues, resource creation,
  computation, memory/storage, and recursion depth. A violation rejects or
  disables the mod, rolls back the current transaction, and records an error.
- Inhabitants may author the same kinds of content and rule proposals. Their
  creations use the same validation and application path and can later be
  exported as normal mod packages.

### Batch 6C — accepted inhabitant authorship and constitution defaults

- Inhabitants may propose tools, items, buildings, recipes, clothing, art,
  festivals, customs, professions, organizations, crops, domesticated species,
  local events, and declarative world rules. Altered biology, resurrection,
  and new physical laws take a higher-risk proposal path.
- Validated data-only creations may be adopted through ordinary world activity.
  Major rule changes require world-owner approval by default, or an explicit
  in-world constitutional process where the current constitution delegates that
  authority.
- Constitutions can change after world creation only through an explicit,
  paused, tested, versioned migration. The change is historical and reversible
  through its migration record; kernel safety and causality remain mandatory.
- Creations are private to their world by default. A human world owner may
  export a reusable package with provenance, dependencies, compatibility,
  capabilities, permissions, and preview/test results. A public registry is
  deferred.

## 2026-09-19 — Contract remediation decisions

The post-freeze review exposed implementation-critical ambiguity, not a need
for another broad design interview. The linked contracts now define the current
authority for deterministic execution, cognition/social state, and content
governance.

- A confirmed provider-wide outage pauses the entire world at an atomic boundary
  and needs explicit owner resume after recovery probes; individual failures use
  bounded local fallback first.
- Tick order, pause/resume, RNG streams, genesis identity, migrations, route
  resolution, inventories/lots, commands, and replay fixtures are fixed by the
  [deterministic kernel contract](../planning/deterministic-kernel-contract.md).
- Inhabitants are identities rather than owned objects. Consent, guardian care,
  provider precedence, cognition epochs, private telemetry, birth/age/death,
  and the default estate path are fixed by the
  [cognition and society contract](../planning/cognition-and-society-contract.md).
- Content activation, package locks, compatibility, authoring batches, runtime
  quarantine, asset integrity/rights, and constitutional changes are fixed by
  the [content-governance contract](../planning/content-governance-contract.md).

The contract documents may be changed only through an explicit decision and
design-log entry, with migration/fixture impact considered alongside prose.
