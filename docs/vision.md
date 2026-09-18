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
- survival creates pressure and meaningful stakes
- personalities and relationships affect choices
- roles are discovered through behaviour rather than selected as permanent
  classes
- settlements, institutions, games, and customs may emerge
- inhabitants can attempt to improve the world when they encounter a problem

There is no mandatory victory condition. A small family surviving, building a
home, and inventing a local game can be a successful world. Another world may
develop agriculture, politics, trade, religions, machines, or entirely new
dimensions over a much longer run.

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
- a large WorldBox-like environment made from logical tiles
- one to three founding inhabitants
- configurable names, personalities, skills, values, and aspirations
- hardcoded basic needs and mortality
- limited family formation and population growth
- gathering, shelter, food production, construction, exploration, and social
  interaction
- a Godot-based pixel-art viewer, if the engine remains the best fit after a
  feasibility prototype
- one real LLM inhabitant at first, with local mock inhabitants for tests

The map's logical tile count is not the same as visible pixel resolution. A
logical world can be large while using a smaller pixel-art tile for rendering.
Chunking and active-region simulation are expected to matter more than choosing
one permanent map size now.

## Agent-created world

The defining long-term feature is not merely that agents play a game. It is
that they may create parts of the game they are playing:

- buildings and objects
- recipes and production chains
- creatures and ecological relationships
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
