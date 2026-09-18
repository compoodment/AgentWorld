---
title: Inhabitants
type: concept
status: draft
updated: 2026-09-18
---

# Inhabitants

Inhabitants are LLM-driven agents embodied in the world. They are not just
chat sessions with sprites; their identity, needs, possessions, relationships,
and memories have consequences in the simulation.

Their possessions are not decorative prompt text. An inhabitant may own food,
tools, clothing, currency, or a share in a building, subject to the world's
property and access rules. See [Economy, Inventories, and Exchange](economy.md).

## Creation

The first world begins with one to three founders. A creator may provide:

- a name, or permission for the inhabitant to choose one
- personality traits and values
- starting skills or knowledge
- an appearance or visual description
- an initial aspiration or interest
- a creator/owner identity and permissions
- a model/provider policy, selected for the world or this inhabitant

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

The world has a 365-day calendar, a normal day/night cycle, and sleep. Fatigue
and sleep make an inhabitant unavailable for ordinary cognition while it rests;
the deterministic simulation continues around it. Exact sleep duration, beds,
shelter quality, and travel interruptions remain to be designed.

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

Population growth is intentionally limited. Inhabitants may form relationships
and have children if world conditions permit, rather than spawning arbitrary
new agents through an unbounded API.

Candidate conditions include:

- compatible relationship and consent rules defined by the world
- adequate food and resource outlook
- available housing or a credible plan to provide it
- care capacity and time
- a configurable population cap
- creator/world-owner policy, if the world requires approval for new minds

Children should be new identities. They may inherit tendencies, appearance
features, cultural knowledge, or physical traits, but not be simple copies of a
parent prompt. Birth, childhood, teaching, adolescence, adulthood, and death
are all potential systems; only the population constraint is currently a firm
direction.

Authorized arrivals or player-created founders may be added later, but they
must use the same population and permission rules as family growth.

## Cognition model

The proposed cognition split is:

### Local simulation

Cheap deterministic systems handle:

- time, movement, pathfinding, and collisions
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
spatial facts. An inhabitant can remember visited tiles, a bed, a resource
location, a landmark, or a route and later plan to travel there. Movement,
pathfinding, travel time, and the exact perception schedule are still open
mechanics.

Human instructions enter through the same validated action boundary. Suggestive
instructions can be rejected; must-do instructions override the selected
inhabitant's normal priorities and values, subject to physical possibility and
the independent responses of other inhabitants.

The runtime must be able to continue safely when no model is available. API
budget exhaustion, timeouts, malformed output, and provider failure are normal
operational states, not reasons to corrupt the world.

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
- optional game-level per-world and per-inhabitant cognition limits
- safe fallback behaviour when a limit or provider is unavailable
- mock or scripted inhabitants for development and tests

The first real-world experiment should measure one inhabitant before adding
more. Population limits are a gameplay rule and an operational safety control.

Children are LLM-influenced from birth, with age-appropriate cognition rather
than an immediate adult planning load. At world creation, the human chooses a
provider policy: select each child, inherit from a parent, use the world
default, or use a hybrid policy. The exact handoff and fallback when a human
has not selected a provider are still open.
