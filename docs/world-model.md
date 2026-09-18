---
title: World Model
type: concept
status: draft
updated: 2026-09-18
---

# World Model

AgentWorld is easiest to reason about as four layers. The separation is a
design boundary, not necessarily four separate programs.

## 1. Engine kernel — protected reality

The kernel defines the minimum rules that make the world coherent. In the
default world, inhabitants and ordinary mods cannot remove or bypass:

- time progression and causal ordering
- the 365-day calendar, day/night cycle, seasons, and pause semantics
- coordinates, occupancy, movement validation, and physical reachability
- health, injury, hunger, rest, and mortality
- resource ownership, inventory access, consumption, transfer, and
  conservation rules
- action validation and authoritative state transitions
- persistence, save integrity, and recovery
- identity, permissions, population limits, and world ownership
- mod capability boundaries, quotas, and rollback
- LLM/API budget limits and emergency shutdown behaviour

The exact list is still open. The important principle is that survival laws
must not be editable by the same actor whose survival depends on them.

## 2. World rules — extensible systems

World rules describe systems that can be added, tuned, or replaced through
versioned mods when the world policy permits it. Examples include:

- agriculture, irrigation, and food preservation
- crafting and manufacturing
- weather and seasons
- creatures, ecology, and domestication
- factions, law, currency, and trade
- technology and research
- magic or other fictional systems, if a world chooses to introduce them
- additional worlds or dimensions

The kernel validates their effects. A farming mod can define irrigation and a
crop can have a powerful nutritional effect, but neither can write directly to
health or hunger, duplicate resources, or bypass the transaction ledger.
Economy details such as prices, wages, property law, currency, and communal
allocation remain world rules. The accounting facts underneath them remain
authoritative kernel state.

## 3. World content — things inhabitants make

Content is the least privileged layer and should be the easiest to create:

- buildings and settlements
- furniture, tools, clothing, and decorations
- items, recipes, and resources
- art, signs, stories, and music-like in-world objects
- creatures and non-kernel behaviours
- games, rituals, festivals, and social spaces
- new maps, biomes, and eventually dimensions

Content should still have schemas, resource costs, spatial validation, and
version metadata. “Creative” does not mean “unbounded mutation.”

An asset is inert data. Its appearance, animation, and metadata may be
inhabitant-created, but behaviour enters through declared interactions and
validated rules. See [Assets and Art Pipeline](assets-and-art.md).

## 4. Persistent world state

This is what actually happened:

- terrain, weather, time, and discovered areas
- tile properties, world objects, landmarks, and spatial knowledge held by each
  inhabitant
- living inhabitants, relationships, families, and deaths
- inventories, resources, buildings, settlements, and projects
- ownership, offers, contracts, transactions, prices, and economic history
- active rules and installed content mods
- proposals, approvals, failures, and rollback history
- simulation events and notable decisions

State should be reconstructible from a snapshot plus an append-only event
history, subject to the final storage design.

## Human authority and authoring mode

The human has an explicit control lane above ordinary inhabitant decisions. A
suggestive instruction may be rejected; a must-do instruction overrides the
selected inhabitant's priorities and makes it attempt the order. Both still
enter the action validator and cannot forge physical state or dictate another
inhabitant's response.

The human can also enter a paused authoring mode before or during a world to
edit terrain, resources, ecosystem objects, and approved assets. Such edits
are authoritative interventions recorded in the event history. The exact edit
toolset and which operations require a pause remain open, but authoring is not
an invisible mutation of the save.

## Action lifecycle

An inhabitant or human participant should never directly mutate state. The
normal path is:

```text
observe
  → choose intention or submit instruction
  → request structured action
  → validate against kernel and current state
  → apply atomically
  → emit event
  → persist
```

Invalid actions should produce a useful reason. The LLM can then revise its
plan instead of the engine quietly accepting impossible state.

## Mod lifecycle

World-changing proposals use a slower path:

```text
propose
  → schema validation
  → capability and safety checks
  → run tests in an isolated copy
  → simulate representative cases
  → check resources and permissions
  → approve, reject, or queue for review
  → version and apply
```

Every applied mod needs an identifier, author, version, dependency list,
declared capabilities, migration strategy, and rollback strategy. These fields
are design requirements even though the package format is not chosen yet.
