---
title: AgentWorld Vision
type: concept
status: draft
updated: 2026-09-18
---

# AgentWorld Vision

## One sentence

AgentWorld is a persistent, open-ended world where LLM-driven inhabitants
survive, form families, build a society, and gradually create new parts of the
world they inhabit.

## The experience

The player does not issue every command. The world should feel like a place
that exists between visits:

- inhabitants have needs and limited lives
- resources can be scarce
- possessions and food can be owned, shared, traded, or withheld
- survival creates pressure and meaningful stakes
- personalities and relationships affect choices
- roles are discovered through behaviour rather than selected as permanent
  classes
- settlements, institutions, games, and customs may emerge
- inhabitants can attempt to improve the world when they encounter a problem
- inhabitants can create art, goods, institutions, and systems that change the
  incentives faced by everyone else

There is no mandatory victory condition. A small family surviving, building a
home, and inventing a local game can be a successful world. Another world may
develop agriculture, politics, trade, religions, machines, or entirely new
dimensions over a much longer run.

## The human role: observer-director

The human is an observer-director rather than an RTS unit controller or a chat
partner for every inhabitant. The normal interface is an observatory and an
instruction panel, not an always-open conversational chatbox. The human can:

- inspect the whole map, all settlements, inhabitants, statistics, and history
- select an inhabitant and inspect its current situation, known facts, memories,
  intention, and summarized decision factors
- send a **suggestive instruction** that the inhabitant may accept or reject
- send a **must-do instruction** that overrides that inhabitant's priorities,
  values, and relationships and makes it attempt the order
- issue a persistent world directive that applies immediately and to inhabitants
  added or born later
- broadcast a direct instruction to the current inhabitants

Must-do instructions still pass through the protected reality: they cannot
teleport an inhabitant, forge resources, or make another inhabitant consent or
respond in a particular way. The player can influence an inhabitant completely
within the bounds of what that inhabitant can physically attempt. Pure
observation remains a valid playstyle.

The human view is omniscient by design. AgentWorld will not use fog of war as a
player-facing feature. Inhabitants remain epistemically limited: they know what
they currently perceive, have explored, remember, or learn from others.

## Design pillars

### Autonomous, not unattended automation

The simulation is a long-running process. It is not a cron script that wakes
up, performs a task, and disappears. The world has its own clock and event
loop. Human viewers and external agents are observers or participants, not the
world's heartbeat.

### Consequences before spectacle

Inhabitants should have reasons to act. Hunger, shelter, illness, danger,
resource scarcity, curiosity, relationships, and unfinished projects create
pressure. Visual novelty is welcome, but it should emerge from a legible world
instead of replacing one.

### A protected reality with an extensible culture

The world needs stable laws so that actions have meaning. Its inhabitants can
invent solutions and add content, but they cannot silently remove the rules
that make survival, effort, and creation matter. See [World model](world-model.md).

### Creative sovereignty without reality sovereignty

Inhabitants should be free to invent powerful things. They are not free to
forge state directly. A miraculous crop, new exchange system, or beautiful
building must enter through declared inputs, outputs, costs, permissions, and
validation. This preserves consequences without forcing the world to remain
poor forever: legitimate abundance is allowed to transform a civilization.

### LLMs at meaningful decision points

An LLM should not be called for every tile movement or hunger decrement. Local
simulation handles routine work. LLM turns are reserved for intentions,
planning, social decisions, reflection, discoveries, and world-creation
proposals. This reduces cost and makes the simulation testable without an API.

### Persistent and inspectable

The world should be saveable, replayable, and understandable. Important state
changes need an event history. A bug should be reproducible from a seed,
version, inputs, and approved mods rather than explained as “the model got
weird.”

## First-world shape

The current design target is:

- one private persistent world
- a seeded procedural environment made from logical tiles and world objects
- a 365-day in-world calendar with four simple seasons
- a fixed normal day/night cycle of roughly 2:40 daylight and 1:20 night
- a small temperate prototype biome with a living ecosystem; richer terrain,
  continents, oceans, rivers, mountains, deserts, wetlands, and boat travel are
  future scale rather than requirements for the first test
- multiple unrelated founding inhabitants
- configurable names, personalities, skills, values, and aspirations
- hardcoded basic needs and mortality
- inhabitant-chosen sleep, with fatigue slowdowns and eventual exhaustion
  damage when rest is delayed
- limited family formation and population growth
- gathering, shelter, food production, construction, exploration, and social
  interaction
- a camp-start preset with basic shelter, bed or bedroll, storage, tools, food,
  and fire/cooking setup
- inventories, ownership, direct barter, and early local exchange
- a small asset pipeline that can turn inhabitant ideas into safe visual and
  interactive content
- a Godot-based pixel-art viewer, if the engine remains the best fit after a
  feasibility prototype
- one real LLM inhabitant at first, with local mock inhabitants for tests

The map's logical tile count is not the same as visible pixel resolution. A
logical world can be large while using a smaller pixel-art tile for rendering.
Chunking and active-region simulation are expected to matter more than choosing
one permanent map size now.

Procedural generation creates simulation-valid terrain: tile properties such as
material, moisture, fertility, elevation, and biome are data, not just painted
pixels. Trees, plants, resources, buildings, and other things that occupy a
tile are separate world objects with growth, condition, and interaction state.
The client or engine renders those facts using authored textures, sprites, and
animations. A pretty image is never the authority for where a resource or
pathable tile exists.

Inhabitants choose destinations and reasons for travel; the simulation handles
route calculation, movement, and travel time. Their local observations always
include the tile they occupy, while landmarks, destinations, and routes become
part of personal spatial memory.

## Agent-created world

The defining long-term feature is not merely that agents play a game. It is
that they may create parts of the game they are playing:

- buildings and objects
- recipes and production chains
- creatures and ecological relationships
- art, objects, and assets authored by inhabitants or generated from their
  proposals
- social institutions and rules
- games and rituals
- new biomes or dimensions
- eventually, carefully sandboxed executable behaviours

Creation is a proposal pipeline, not direct mutation of the running server.
The first implementation should use a safe declarative format. More powerful
code generation can come later only if the sandbox, review policy, rollback,
resource limits, and compatibility model are genuinely adequate.

## Multiplayer direction

Multiplayer is not a first milestone. The initial goal is one private world
that can run on a VPS and be viewed remotely. However, the simulation should
have an authoritative-server boundary from the beginning so that later
multiplayer does not require rewriting the world model.

Future participants might:

- watch a shared world
- add authorized founders
- introduce their own agents
- submit or review mods
- build or trade in the world

These are future capabilities, not current requirements.

## Explicit non-goals for the concept phase

- building an MMO before the single-world loop is compelling
- allowing arbitrary generated code to execute on the host
- adding dozens of LLMs before measuring cost and cognition quality
- fixing the final art style, map size, model provider, or storage engine by
  assumption
- hiding unresolved design questions behind a large framework
