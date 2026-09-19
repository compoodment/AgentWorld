---
title: Inhabitants
type: concept
status: draft
updated: 2026-09-19
---

# Inhabitants

The exact cognition, provider, memory, relationship, consent, birth, age, and
death semantics are specified in the
[cognition and society contract](../planning/cognition-and-society-contract.md).

Inhabitants are LLM-driven agents embodied in the world. They are not just
chat sessions with sprites; their identity, needs, possessions, relationships,
and memories have consequences in the simulation.

Their possessions are not decorative prompt text. An inhabitant may own food,
tools, clothing, currency, or a share in a building, subject to the world's
property and access rules. See [Economy, Inventories, and Exchange](economy.md).

## Creation

The first world begins with a small group of multiple, unrelated founders. A
creator may provide:

- a name, or permission for the inhabitant to choose one
- personality traits and values
- starting skills or knowledge
- an appearance or visual description
- an initial aspiration or interest
- a creator/owner identity and permissions
- a model/provider policy from the world's configured provider registry

The human enters the providers and models they want to use, including hosted
API providers such as Ollama Cloud. Each world has a human-selected default;
an individual inhabitant may override it at creation, and the human may change
that inhabitant's provider or model after the world starts. There is no built-in
provider or local-model assumption.

The inhabitant should still have room to interpret the starting material. A
role such as “farmer” is an initial condition or aspiration, not a permanent
class that forbids other lives.

## Needs and stakes

The kernel owns basic needs. The initial candidate set is:

- health and injury
- hunger and nutrition
- rest and fatigue
- shelter and exposure
- safety and environmental danger

The final set, rates, and interactions require design and balance work. Needs
must be hardcoded enough that an inhabitant cannot simply code hunger away. An
inhabitant can discover farming, preservation, medicine, shelter, automation,
or another solution to a need.

The world has a 365-day calendar, a normal day/night cycle, and sleep. An
inhabitant decides when to sleep rather than being automatically put to bed by
the simulation. Low energy slows work and walking; sustained exhaustion causes
health damage. If necessary, an inhabitant may sleep wherever it is, even in a
place that is ultimately unsafe. For the initial model, a sleeping place is
safe when its ground is stable, it is not inside a severe environmental
exposure hazard, and no active predator or hostile threat is immediately
present. Missing a bed or shelter alone does not make sleep impossible; it
makes recovery worse. Unsafe sleep can risk injury, illness, or death depending
on the hazard. Exact thresholds, sleep duration, and recovery rates remain
prototype-tuning work.
Night makes sleep and energy recovery more relevant without forcing every
inhabitant into the same schedule.

The distinction is important: a crop can reduce hunger through a valid,
declared food effect; an inhabitant cannot directly edit its hunger value. A
very productive crop may still be accepted if it pays real inputs and passes
validation. If it creates abundance, the economy and society must adapt.

## Roles and identity

Roles should emerge from behaviour and social recognition:

- an inhabitant may become a farmer by repeatedly tending food
- a builder may become an engineer after solving construction problems
- a storyteller may become a ritual leader or teacher
- a person may change roles when circumstances or interests change

The game may expose tags such as current work, expertise, reputation, and
aspiration, but should avoid turning them into rigid classes too early.

## Family and population

Inhabitants may form relationships and have children if world conditions
permit, rather than spawning arbitrary new agents through an unbounded API.

Candidate conditions include:

- compatible relationship and consent rules defined by the world
- adequate food and resource outlook
- available housing or a credible plan to provide it
- care capacity and time
- resource, housing, and care conditions that make additional population
  sustainable
- creator/world-owner policy, if the world requires approval for new minds

Children should be new identities. They may inherit tendencies, appearance
features, cultural knowledge, or physical traits, but not be simple copies of a
parent prompt. Birth, childhood, teaching, adolescence, adulthood, and death
are all potential systems; the firm direction is that population grows through
world conditions and family formation rather than arbitrary spawning or a fixed
numeric cap.

Authorized arrivals or player-created founders may be added later, but they
must use the same population accounting and permission rules as family growth.
The initial design has no fixed numeric population cap; resource, housing,
care, and simulation capacity still determine what is sustainable.

## Cognition model

The proposed cognition split is:

### Local simulation

Cheap deterministic systems handle:

- time, movement, pathfinding, route execution, and collisions
- need changes and resource consumption
- routine work already chosen
- simple reactions and survival priorities
- construction progress and production

### LLM decisions

An LLM is consulted when an inhabitant reaches a meaningful decision point:

- choosing or revising a project
- responding to a crisis or discovery
- deciding between competing needs
- forming or repairing a relationship
- teaching, negotiating, or planning collectively
- deciding whether to eat, trade, gift, reserve, or stockpile possessions
- setting a price, accepting a bargain, joining a cooperative, or hiring help
- proposing new world content or a mod
- reflecting on an outcome and changing an aspiration

An observation is not an omniscient dump of the world. It is assembled from
the inhabitant's current perception, communication, durable memory, and known
spatial facts. The current tile is always included. Nearby visual surroundings,
messages, changes to known locations, danger, urgent needs, body/task state,
time and season, nearby weather, and direct interactions may also enter the
observation. An inhabitant can remember a bed, resource location, landmark,
destination, or route and later plan to travel there. The LLM chooses the
destination and broad travel intention; deterministic simulation calculates the
route and executes it with tile-grid pathfinding. Common routes may be reused.
Roads are preferred when their lower movement cost makes them faster. Terrain,
roads, health, and transport affect travel time; carried weight and weather
are later additions, not first-prototype factors.

All meaningful event categories may wake cognition: urgent needs, nearby
danger, messages, discoveries, failed actions, human instructions, project
milestones, relationship events, and noteworthy changes that make the current
plan worth reconsidering. Repeated small events are coalesced into one
observation, with the exact interval tuned experimentally and monitored during
prototype operation. During normal model latency, the inhabitant continues its
last valid intention. If a model remains unavailable for long enough, it
switches to simple deterministic behaviour: eat when hungry, seek shelter,
flee danger, sleep when exhausted, continue routine work, or return home as
appropriate. Urgent danger and survival problems use fallback behaviour shaped
by personality and learned habits; without a valid intention or safe fallback,
it stops safely.

Human instructions enter through the same validated action boundary. Suggestive
instructions can be rejected; must-do instructions override the selected
inhabitant's normal priorities and values, subject to physical possibility and
the independent responses of other inhabitants. A must-do instruction does not
magically interrupt sleep, travel, or crafting; it waits in the instruction
queue until the next valid decision point. An ordinary project finishes its
current small atomic step before reconsidering; immediate interruption is
reserved for urgent danger and survival conditions.

Sleep is possible wherever the inhabitant chooses, including an unsafe
location. A bed or bedroll improves energy recovery, and proper shelter
improves recovery and protection from exposure. The camp-start preset provides
basic shelter, a bed or bedroll, storage, basic tools, starting food, and a
fire/cooking setup.

The runtime must be able to fail safely when a model response is slow,
malformed, or temporarily unavailable. A prolonged individual failure can use
simple local behaviour, but a provider-level outage pauses the game and notifies
the human rather than silently running up an unknown failure state.

## Memory

An inhabitant needs a compact representation of what matters to it, but the
storage design is open. Candidate layers are:

- current perception
- short-term working memory
- durable personal memories
- spatial memory: visited tiles, landmarks, objects, and known routes
- shared cultural knowledge
- world facts and discovered recipes

Memory must not override authoritative state. If an inhabitant remembers that a
bridge exists but the bridge was destroyed, the world state wins and the
discrepancy can become a meaningful experience.

## Population cost controls

All inhabitants do not need to receive an LLM call on every simulation step.
The design should support:

- active, nearby, or decision-ready inhabitants
- sleeping or background inhabitants with local simulation only
- event-triggered cognition
- provider/account limits chosen by the human
- safe fallback behaviour after an individual model failure
- provider-outage pause and human notification
- mock or scripted inhabitants for development and tests

The first real-world experiment should measure one inhabitant before adding
more. Population is not capped by an arbitrary fixed number in the initial
design; food, housing, care, and actual simulation/provider capacity determine
what the world can sustain.

Children are part of the first playable society: birth and age-appropriate
growth are included rather than skipping directly to adult inhabitants. They
inherit biologically grounded traits, personality tendencies, initial skills,
and culture, but not a parent's raw memories. Adoption is deferred and is not
part of the initial family model. The world setup retains the previously
decided provider policy: select each child, inherit from a parent, use the
world default, or use a hybrid policy.
