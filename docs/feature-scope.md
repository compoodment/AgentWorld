---
title: Feature Scope and Priority
type: design
status: active
updated: 2026-09-18
---

# Feature Scope and Priority

This is a concept inventory, not a promise that every item will be built. It
exists to stop attractive ideas from silently becoming first-milestone
requirements.

## Protected foundation

These are the smallest laws that make the rest meaningful:

- causal time and ordered events
- a 365-day calendar, simple seasons, day/night, and pausing
- locations, reachability, occupancy, and action validation
- health, injury, hunger, nutrition, rest, exposure, and mortality
- resource conservation and truthful production/consumption
- inventories, ownership, access, and atomic transfer
- identity, permissions, population limits, and world ownership
- persistence, snapshots, event history, recovery, and replay fixtures
- mod capabilities, quotas, compatibility, rollback, and host isolation
- provider/account configuration, timeouts, fallback behaviour, provider-outage
  pause, and emergency shutdown; game-side cognition ceilings are deferred
- complete human observability, explicit suggestive/must-do instructions, and
  recorded paused authoring-mode edits

## First compelling world

The first world should be able to demonstrate a small society, not a full
civilization simulator. Priority systems are:

- multiple unrelated founders with distinct identities and needs
- gathering, food production, storage, shelter, construction, and basic craft
- inventories, ownership, direct barter, and simple local exchange
- routine work plus meaningful LLM decisions
- relationships, emergent roles, teaching, cooperation, and conflict
- limited family growth and population constraints
- exploration across a modest logical map
- a seeded temperate ecosystem with simple terrain tiles, objects, and seasons
- spatial memory of visited places, landmarks, beds, resources, and routes
- destination-level LLM travel intentions with deterministic route execution
- local observations, coalesced cognition triggers, and urgency-based fallback
- inhabitant-chosen sleep, including unsafe fallback, with fatigue slowdowns,
  eventual exhaustion damage, and bed/shelter recovery bonuses
- a camp-start prototype preset
- camp-start supplies: basic shelter, bed or bedroll, storage, basic tools,
  starting food, and a fire/cooking setup
- a small asset/content proposal pipeline with safe declarative rules
- inspectable events, decisions, failures, and world history

This is enough to test whether autonomous life becomes interesting. It does
not require every possible institution or a large generated-content ecosystem.

## Expansion after the core loop

Add these only when the first world shows a concrete need for them:

- richer ecology, more detailed seasons and weather, and domestication
- organizations, laws, taxes, wages, contracts, and currency
- research, technology trees, and larger production chains
- faction politics, religion, festivals, games, and formal education
- richer art generation and inhabitant-created assets
- sandboxed behaviours beyond declarative rule composition
- larger maps, distant-region simulation, and multiple settlements

These are compatible with the concept but should not be smuggled into the
kernel or first prototype as assumed requirements.

## Explicitly deferred

- arbitrary generated code running on the host
- public multiplayer, MMO scale, and world discovery
- a fixed final engine, art style, map size, storage engine, or model provider
- multiple dimensions as a default feature
- combat as a mandatory foundation system
- magic as a default world rule

Dimensions, combat, and magic are not forbidden forever. They require a
deliberate world-constitution decision and a demonstrated reason to expand the
simulation, rather than being automatic answers to every design problem.

## Scope rule

When a proposed feature arrives, ask:

1. Does it protect causality or merely add content?
2. Does the first world need it to make survival and society legible?
3. Can it be expressed as a declarative world rule before executable code?
4. What new state, failure modes, and cognition cost does it introduce?
5. What existing feature can be delayed or removed to pay for it?

The project should prefer a smaller coherent world over a catalogue of
half-simulated systems.
