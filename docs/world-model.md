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
- coordinates, occupancy, movement validation, and physical reachability
- health, injury, hunger, rest, and mortality
- resource ownership, consumption, and conservation rules
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

The kernel validates their effects. A farming mod can define irrigation; it
cannot make a negative food balance harmless by writing directly to health.

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

## 4. Persistent world state

This is what actually happened:

- terrain, weather, time, and discovered areas
- living inhabitants, relationships, families, and deaths
- inventories, resources, buildings, settlements, and projects
- active rules and installed content mods
- proposals, approvals, failures, and rollback history
- simulation events and notable decisions

State should be reconstructible from a snapshot plus an append-only event
history, subject to the final storage design.

## Action lifecycle

An inhabitant or human participant should never directly mutate state. The
normal path is:

```text
observe
  → choose intention
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
