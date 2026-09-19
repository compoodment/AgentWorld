---
title: AgentWorld Design Log
type: decision-history
status: history
updated: 2026-09-19
---

# Design Log

This is the public chronological record of how decisions were made. The
[decision register](decision-register.md), not this log, is the current source
of truth.

## 2026-09-18 — Initial concept capture

### Current decisions

- The project name is **AgentWorld**.
- The project will be open source on GitHub.
- The project is in concept and foundation design, not implementation.
- The world should persist and continue without cron-triggered tasks or a human
  issuing every action.
- A default world protects its core survival laws: health, needs, mortality,
  time, validation, persistence, permissions, and sandbox boundaries.
- The first world begins with a small group of multiple, unrelated founders.
- Inhabitants may self-name and have personalities, values, skills, roles, and
  aspirations.
- Roles should be emergent rather than permanent classes.
- Population should grow through family formation rather than arbitrary agent
  spawning.
- The first world is private; multiplayer is a later goal, but the server
  boundary should be authoritative enough to support it later.
- Godot is the leading visual/client candidate, not a final commitment.
- The first real LLM experiment should use one inhabitant and event-driven
  cognition, with local mock agents for development and testing.
- Agent-created world changes should begin as safe declarative proposals and
  only become executable code if a real sandbox and rollback model exists.

### Deliberately not decided at that stage

- exact founder count, founder identity-approval flow, and family model
- final need list and balance
- exact world size, tile scale, and chunking strategy
- model/provider name, call frequency, and API budget
- Godot versus a separate simulation service
- storage and networking implementation
- the full mod schema and sandbox technology
- the amount of direct human control
- whether dimensions, combat, rich ecology, or weather belong in the first
  compelling world; basic exchange is now a first-world requirement, while
  complex economic institutions remain later scope

### Design principle

The concept comes before the foundation. The foundation should implement a
reasonably complete concept, not force the concept to become whatever the
first framework makes convenient.

## 2026-09-18 — Observer-director and world-clock interview

### Current decisions

- The human role is an **observer-director**. The player sees the complete
  world and can inspect selected inhabitants, summarized decision factors,
  memories, intentions, and known facts.
- Human influence uses two instruction strengths: suggestive instructions may
  be rejected; must-do instructions override the selected inhabitant's normal
  priorities, values, and relationships and make it attempt the order.
- Persistent world directives and direct broadcasts are separate controls.
  Directives apply immediately and to inhabitants added or born later.
- Must-do instructions still pass through physical reality and cannot dictate
  another inhabitant's consent or response.
- Fog of war is not part of AgentWorld. Inhabitants are still limited by
  perception, communication, memory, and spatial knowledge.
- The first world uses a 365-day in-world calendar, four simple seasons, and a
  provisional four-real-minute day: about 2:40 daylight and 1:20 night. The
  timing can be changed through world configuration after playtesting.
- The world continues and may spend provider resources while unpaused and
  unattended. Provider/account limits are controlled outside the game; the
  runtime may add optional game-level limits and local-provider controls.
- Prototype maps are seeded and procedurally generated, begin with a living
  ecosystem, and use a small temperate biome. Tile properties are simulation
  data; trees, resources, buildings, and other occupants are separate world
  objects rendered by the client.
- A paused authoring mode may edit terrain, resources, ecosystem objects, and
  approved assets before or during a world, with interventions recorded.
- Inhabitants can remember visited tiles, landmarks, objects, resources, and
  routes. Movement and travel are the next major design interview.
- Children are LLM-influenced from birth with age-appropriate cognition. The
  world setup offers per-child selection, parent inheritance, world default,
  and hybrid provider policies.

### Deliberately not decided at the end of this batch

- movement representation, pathfinding, travel time, transport, and boats
- perception inputs, cognition wake events, model latency, and action
  interruption rules
- exact sleep, shelter, bed, fatigue, and starting-preset mechanics
- provider fallback and the distinction between game-side limits and provider
  billing limits
- the exact paused authoring toolset and asset approval path

### Design principle

The human can be powerful without making the simulation omniscient for its
inhabitants. Full map visibility belongs to the observatory; limited knowledge
belongs to the people living in the world.

## 2026-09-18 — Movement and local cognition interview

### Current decisions

- Movement is logically tile-backed, with smooth client animation allowed
  between authoritative tile positions.
- An inhabitant's LLM chooses a destination and broad travel intention. The
  deterministic simulation calculates, validates, and executes the route.
- Terrain, roads, health, and transport affect travel time in the prototype.
- Spatial memory emphasizes meaningful landmarks and destinations, with
  lightweight route memories instead of recording every tile as prose.
- The inhabitant always knows its current tile, including its material,
  occupants, objects, and relevant effects.
- Local observations include nearby visual surroundings, messages, changes to
  known locations, danger, urgent needs, body/task state, time and season,
  nearby weather, and direct interactions.
- All meaningful event categories may wake cognition. Repeated small events are
  coalesced into one observation rather than one model call per event.
- During normal model latency, an inhabitant continues its last valid
  intention. Urgent danger or survival problems use deterministic emergency
  behaviour; with no valid intention or safe fallback, it stops safely.
- Must-do instructions queue instead of immediately interrupting sleep, travel,
  or crafting. They apply at the next valid decision point.
- Any safe location permits sleep. Beds improve energy recovery, shelter
  improves recovery and exposure protection, and the prototype uses camp start.

### Deliberately not decided at the end of this batch

- exact pathfinding and route-cache implementation
- future rivers, mountains, coastlines, boats, and continent-scale travel
- exact fatigue, sleep, exposure, and wake-up formulas
- cognition priority, coalescing windows, cooldowns, and observation schema
- provider fallback, game-side limit semantics, and paused authoring details

### Design principle

The LLM chooses meaningful intentions; the simulation handles repetitive,
physical execution. That keeps inhabitants expressive without charging a model
to rediscover how to walk from one square to the next.

## 2026-09-18 — Survival, providers, and authoring interview

### Current decisions

- An inhabitant decides when to sleep. Low energy slows tasks and walking;
  sustained exhaustion eventually damages health.
- If exhausted, an inhabitant may sleep wherever it is, including an unsafe
  location. The exact safety and exposure model is still a prototype-tuning
  question.
- Night makes sleep and energy recovery more relevant, but does not impose one
  universal sleep schedule.
- Camp start provides basic shelter, a bed or bedroll, storage, basic tools,
  starting food, and a fire/cooking setup.
- Meaningful events may cause an inhabitant to reconsider an ordinary plan.
  The exact coalescing interval and cognition cooldown are left for
  measurement.
- A prolonged individual model failure switches the inhabitant to simple local
  behaviour. Emergency behaviour is shaped by personality and learned habits.
- If the provider itself is unavailable, the game pauses and notifies the
  human.
- At creation, the human may choose an inhabitant's provider, model,
  personality, and skills. Provider and model choices may be changed after the
  world starts.
- Game-side cognition ceilings are deferred; provider/account controls remain
  the initial cost boundary.
- Paused authoring may edit terrain, rivers and water, resources, trees and
  plants, buildings, inhabitants, weather and seasons, and approved assets.
- Authoring may create resources from nothing, but records each placement as an
  explicit god-mode intervention.
- Human-created assets may be used after format and performance checks; new
  behaviours and rules require deeper validation.
- The first camp contains multiple unrelated founders.

### Deliberately not decided at the end of this batch

- what makes a sleeping place safe or unsafe, and the exact fatigue, recovery,
  exposure, and health-damage formulas
- the cognition coalescing interval, cooldowns, and event-priority policy
- the local fallback behaviour library and how learned habits modify it
- the first supported provider/model default and provider assignment for
  newborns when the human has not chosen one
- the exact pathfinding and route-cache implementation

### Design principle

Failure handling should preserve the world and make the operational boundary
visible. A single bad thought can fall back locally; a provider outage is a
human-visible pause, not a silent simulation that spends blindly.

## 2026-09-19 — Movement, fallback, and family interview

### Current decisions

- The first map uses deterministic tile-grid A*-style pathfinding with
  reusable route caches. Roads reduce movement cost and are preferred when
  faster; relevant map changes invalidate cached routes.
- Rivers, mountains, and coastlines are impassable in the first prototype.
  Boats and other traversal modes come later.
- Blocked routes, danger, urgent needs, must-do orders, and irrelevant
  destinations may interrupt travel at the next action boundary. Carried weight
  and weather are deferred as travel factors.
- Safe sleep requires stable ground, no severe exposure hazard, and no active
  nearby predator or hostile threat. Beds and shelter improve recovery but are
  not prerequisites. Unsafe sleep can risk injury, illness, or death.
- The observation has a stable core of current tile, body/urgent needs,
  current task/intention/destination, and immediate local perception. Other
  context is selected when relevant. Cognition coalescing is tuned
  experimentally and monitored during the prototype.
- Ordinary instructions wait for the current atomic step to finish. The
  developer view exposes full structured decision telemetry, not raw hidden
  chain-of-thought.
- Deterministic fallback covers eating, shelter-seeking, fleeing, sleeping,
  routine work, and returning home. Personality and learned habits influence
  the choice and risk tolerance.
- The human configures hosted provider APIs and models, chooses a world
  default, and can override or change an inhabitant's provider/model. Ollama
  means Ollama Cloud through its API; local inference is not a target.
- Birth and child growth are first-playable features. Children inherit
  biological traits, personality tendencies, initial skills, and culture, but
  not raw parental memories. Adoption is deferred.
- There is no fixed numeric population cap; food, housing, care, and actual
  simulation/provider capacity determine sustainability.

### Deliberately not decided at the end of this batch

- exact fatigue, sleep, exposure, and unsafe-sleep risk formulas
- experimentally measured cognition coalescing and cooldown values
- provider registry capabilities and active provider/model change mechanics
- detailed family, care, adolescence, adulthood, and death systems
- later traversal modes such as boats

### Design principle

Keep intent expressive and execution deterministic. Roads, memories, habits,
and personality should shape choices without asking an LLM to micromanage every
tile or turning population into an arbitrary counter.

## 2026-09-18 — Economy, assets, and scope pass

### Current decisions

- Inventories and ownership are first-class world state, not flavour text.
- Trades, gifts, wages, and contracts settle through an authoritative atomic
  transaction ledger.
- The kernel protects accounting and causality, but does not prescribe
  capitalism, communism, barter, currency, or another economic ideology.
- Inhabitants may create powerful abundance through valid world proposals, but
  may not write hunger, health, ownership, or inventory state directly.
- Art and assets enter through a proposal, normalization, validation, and
  versioning pipeline. Asset files are inert and cannot execute host code.
- The first compelling world includes basic exchange and a small content/asset
  pipeline. Full law, credit, rich ecology, multiple dimensions, combat, and
  magic are expansions or deferred choices rather than foundation requirements.

### Design principle

Creative freedom should be limited by causality and inspectability, not by a
permanent ban on powerful ideas. If inhabitants discover abundance, society
should change around it.

## 2026-09-19 — Batch 5A survival and cognition defaults

### Current decisions

- Fatigue uses a moderate curve: it progressively slows work and walking,
  beds and shelter improve recovery, and sustained exhaustion damages health.
- Unsafe sleep is risky but not arbitrarily lethal. Severe exposure, repeated
  neglect, or already-poor health can produce serious outcomes.
- Cognition uses a middle-ground cadence: deterministic emergency response is
  immediate; urgent events coalesce briefly; normal and background events are
  batched more slowly; normal decisions have a cooldown unless relevant state
  changes.
- Attention is selected deterministically from urgency, current goals,
  recency, relationship relevance, novelty, and redundancy. The model does not
  receive an unbounded raw memory dump.

### Deliberately not decided

- The exact constants for fatigue, recovery, exposure, cognition windows, and
  attention budgets remain measurable prototype tuning parameters.

### Design principle

The first world should feel alive without turning every small stimulus into an
LLM invoice. Deterministic filtering and coalescing protect both legibility and
cost while preserving meaningful decisions.

## 2026-09-19 — Batch 5B telemetry, routing, and providers

### Current decisions

- Developer saves retain full structured cognition telemetry and raw structured
  model responses; ordinary saves may retain summaries and hashes.
- Route caches are rebuildable performance data and are discarded/rebuilt on
  load rather than treated as authoritative world state.
- The first user-configurable hosted providers are Ollama Cloud API and OpenAI
  API. A deterministic mock provider remains available for tests and offline
  development.
- Provider/model changes apply at the next cognition boundary. The current
  atomic action continues, and stale in-flight responses are discarded rather
  than applied under the new configuration.

### Design principle

Provider choice belongs to world configuration; deterministic mocks belong to
the test harness. Neither provider responses nor route caches are allowed to
become hidden authoritative world state.

## 2026-09-19 — Batch 5C family and mortality defaults

### Current decisions

- Relationships use a typed graph. Household membership is separate from
  biological, social, or legal ties.
- Adulthood is hybrid: kernel age bands govern protected rules, while cultures
  may add ceremonies, apprenticeships, and social recognition.
- Children rely on caregiver networks. Birth readiness considers consent,
  health, food, sleeping space, care capacity, and safety. There is no global
  population cap or default human approval gate for births.
- Death is an authoritative, recorded kernel transition. Future world rules or
  validated mods may add constrained resurrection, but revival creates a new
  explicit event/state and cannot erase the historical death.

### Design principle

The world can expand its metaphysics without making mortality meaningless:
extraordinary restoration must be an explicit, costly, inspectable rule rather
than a silent undo button.

## 2026-09-19 — Batch 5D credentials, model, and asset defaults

### Current decisions

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

### Deliberately not decided

- The exact provider capability schema, tile-scale choice, numerical asset
  budgets, and final client implementation remain engineering decisions.

### Design principle

Keep secret material outside world state, keep the initial model contract
narrow, and preserve creative variation inside an inspectable asset pipeline.

## 2026-09-19 — Batch 5E replay, traversal, and failure defaults

### Current decisions

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

### Deliberately not decided

- Exact serialization formats, retention limits, replay UI, and
  traversal-specific mechanics remain engineering work.

### Design principle

Replay should preserve inspectability without depending on an unavailable
provider, and new movement modes should extend the simulation contract rather
than smuggle nondeterminism into pathfinding.

## 2026-09-19 — Batch 6A kernel and modding boundaries

### Current decisions

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
  isolation. Review applies to outside contributors and to packages installed
  on a hosted world; it is not the sole security boundary.

### Provisional recommendation

- Future executable mods should probably use a capability-limited WebAssembly
  sandbox. A private fork can change host code directly, but that is a separate
  deployment fork rather than an installed-mod capability.

### Deliberately not decided

- Mod migration, formal resource-proof/testing strategy, and the final
  executable sandbox/runtime remain open.

### Design principle

The game should be highly moddable without requiring a hosted server to trust
arbitrary packages with its filesystem, network, credentials, or process.

## 2026-09-19 — Batch 6B mod packages and safety defaults

### Current decisions

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
- Inhabitants may author objects, items, recipes, buildings, customs, events,
  and potentially world-rule proposals. Their creations use the same
  validation and application path and can later be exported as normal mod
  packages.

### Later resolved

- Batch 6C established that validated data-only content may be adopted through
  ordinary world activity, major changes follow constitutional authority, and
  export remains a human world-owner action. Authorship still grants neither
  kernel nor executable privileges.

### Design principle

Modding is not only an external developer feature. The world itself should be
able to invent and preserve new content without turning creativity into an
unvalidated back door.

## 2026-09-19 — Batch 6C inhabitant authorship and constitutional change

### Current decisions

- Inhabitants may propose tools, items, buildings, recipes, clothing, art,
  festivals, customs, professions, organizations, crops, domesticated species,
  local events, and declarative world rules. Altered biology, resurrection,
  and new physical laws follow a higher-risk proposal path.
- Validated data-only content may be adopted through ordinary world activity.
  Major rule changes require the authority established by the world
  constitution: human-owner approval by default, or an explicit in-world
  constitutional process where the constitution delegates that authority.
- A constitution may change after world creation only through an explicit,
  paused, validated, versioned migration. It is a historical event with a
  rollback path; kernel safety and causality cannot be removed.
- Inhabitant-authored creations are private to their world by default. A human
  owner may export a reusable package with author/provenance, dependencies,
  compatibility, capabilities, permissions, and preview/test results. A public
  registry is deferred.

### Design principle

Inhabitants can become genuine authors of their world without acquiring the
unstated power to rewrite protected reality, publish arbitrary code, or erase
the history of how their world changed.

## 2026-09-19 — Review-contract remediation

### Current decisions

- A broad external review identified overlapping but real execution-contract
  gaps. The project resolved them as three maintained contracts rather than a
  new collection of disconnected issue-specific prose.
- The deterministic kernel contract fixes the replay/pause/time/RNG/save/map/
  movement/inventory/message boundary and Phase 1 acceptance fixtures.
- The cognition and society contract fixes authority provenance, provider
  binding/failure, privacy, memory, consent, family lifecycle, and estates.
- The content-governance contract fixes immutable package identity, live
  data-only activation, authoring batches, dependency resolution, quarantine,
  asset rights/limits, and constitutional migrations.
- A confirmed provider-wide outage is now unambiguously a full-world atomic
  pause requiring recovery probes and explicit owner resume; it does not let
  unrelated simulation time advance in the background.

### Design principle

If a rule changes replay, authority, safety, or resource conservation, it needs
an executable contract and fixture—not another vague “we should think about
this later” bullet. Fancy phrasing is not a state machine. Sad but true.

## 2026-09-19 — Release versioning

### Current decisions

- Public AgentWorld releases use pre-1.0 Semantic Versioning and annotated
  `v`-prefixed Git tags. The project has no release tag until a runnable,
  replay-verified vertical slice passes its gate; the first target is
  `v0.1.0-alpha.1`.
- Release labels are distinct from source identity and saved-world
  compatibility. The exact commit SHA identifies a build, while the existing
  contract, simulation, and schema versions continue to govern persistence and
  replay compatibility.
- Later package metadata will be the runtime version authority. A Python build
  may use the equivalent PEP 440 form (`0.1.0a1`) internally while public tags
  and release notes use the SemVer label (`v0.1.0-alpha.1`).

### Design principle

People need to know whether a build is an experimental game release, while the
simulation needs to know whether a world can safely load. Those are related,
but they are not the same bloody number.

## 2026-09-19 — Phase 1 C# toolchain

### Current decisions

- The Phase 1 authoritative kernel uses C# 14 on .NET 10 LTS, with an exact
  `10.0.401` SDK baseline and committed NuGet dependency locks.
- xUnit provides the initial `dotnet test` path; formatting and tests run in
  GitHub Actions using the same locked dependency commands as a fresh clone.
- The core is a normal headless .NET library. Godot remains a potential later
  viewer rather than a simulation dependency, even though C# lets both layers
  share a language if that viewer direction survives prototyping.

### Design principle

One language should reduce friction between the world brain and its future
body, not let the body quietly become reality. The deterministic kernel stays
boringly headless on purpose.
