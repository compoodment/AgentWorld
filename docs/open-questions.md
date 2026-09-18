---
title: Open Questions
type: design
status: active
updated: 2026-09-18
---

# Open Questions

These are intentionally unresolved. Closing them should produce a decision,
prototype result, or explicit reason to defer—not a guess hidden in code.

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

## Batch 4 — next questions

The following mechanics remain unresolved and are the next interview targets:

### Travel and world scale

- What exact pathfinding approach and route-cache rules are appropriate for the
  first map?
- How are rivers, mountains, coastlines, boats, and future continents handled?
- What additional travel factors, such as carried load or weather, should wait
  until after the prototype?
- What can interrupt an active route, and how is a changed destination handled?

### Cognition and embodiment

- What makes a sleeping place safe or unsafe, and what are the exact fatigue,
  sleep duration, bed, shelter, exposure, wake-up, and health-damage formulas?
- What observation fields are always present, and which are optional or
  attention-selected?
- How are event priority, coalescing windows, and cognition cooldowns balanced?
- How should instructions interact with an already-running project or route at
  the next decision point?
- What should the client expose as summarized decision factors, memories, and
  intentions without presenting hidden chain-of-thought as a game feature?

### Runtime, family, and authoring detail

- What local fallback behaviours are available, and how do personality and
  learned habits modify them?
- Which provider/model is the first supported default, and what policy assigns a
  newborn when the human has not selected one?
- How are birth, development stages, care, adoption, inheritance, and death
  represented in the first playable society?
- Which asset format, performance checks, provenance fields, and preview steps
  are needed for the first client?

## Founders and family

- Can a founder self-name and self-describe, or must the creator approve its
  identity?
- What exactly counts as a family and how are relationships represented?
- How are consent, partnership, birth, adoption, and care modelled without
  reducing them to a crude population button?
- Is the population limit fixed per world, resource-based, or both?
- How much inherited memory, culture, appearance, and skill should children
  receive?

## Kernel and modding

- What is the smallest genuinely immutable kernel?
- Which systems belong in moddable world rules rather than the kernel?
- Can a world owner choose a different constitution when creating a new world?
- What capabilities can be auto-approved, and which require human review?
- Which sandbox technology is appropriate for future executable behaviours?
- How are mods migrated when the simulation version changes?
- How do we prove that a mod cannot create runaway resources, entities, or
  computation?

## Economy and assets

- Which ownership models should the first world support: personal, household,
  communal, cooperative, or all of them through one abstraction?
- Is direct barter enough for the first economy, or should a local exchange
  token exist from the start?
- How should prices, wages, debt, theft, taxation, and contracts interact with
  consent and local law?
- Which economic events deserve an LLM decision and which stay deterministic?
- What asset formats and normalization rules are needed for the first client?
- Which generated-art sources are acceptable, and how should provenance and
  licensing be recorded?
- How much visual inconsistency should a culture be allowed to create before
  the world becomes unreadable to a human viewer?

## LLM runtime and technology

- Which model/provider should be the first supported default?
- What is the observation format and maximum context size?
- What personal memory is useful enough to retain, and how is it compressed?
- How do we evaluate whether a choice is coherent without judging it only by
  how entertaining it sounds?
- Is Godot the simulation engine, the client only, or merely a prototype tool?
- What storage engine best supports snapshots, events, and migrations?
- What is the smallest useful logical map and chunk size?
- Does the first visual style require custom pixel art or temporary generated
  assets?
- Should the client be a native Godot application, a web client, or both?
- What protocol keeps a future multiplayer client possible without overbuilding
  networking now?

## Scope

- When does a new dimension become a justified design need rather than scope
  inflation?
- Which systems are essential for the first compelling world after movement,
  cognition, and survival are proven?
- How many inhabitants can the target VPS run at acceptable cost?
- What is the first feature that should be removed if the foundation becomes
  too complex?
