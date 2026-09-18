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
  provider setup, with optional game-side cognition limits as a second guard.
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

## Batch 2 — embodiment, movement, and cognition

These are the next interview topics. They are deliberately specific enough to
turn the observer-director concept into a runnable simulation.

### Movement and travel

- Is movement represented as discrete tile steps, continuous coordinates, or a
  logical action that the client animates between tiles?
- What determines travel time: terrain, carried load, weather, roads, health,
  and transport capacity?
- Should an inhabitant commit to a path and follow it locally, or reconsider
  its route at meaningful waypoints and interruptions?
- How should rivers, mountains, coastlines, boats, and future continents fit
  into the same travel model?
- What can interrupt travel, and what happens when the destination or route is
  no longer valid?

### Perception and cognition

- Which facts are forced into an observation because they are immediately
  perceived, and which may the inhabitant choose to inspect or remember?
- What events wake cognition: urgent needs, nearby danger, social messages,
  discoveries, failed actions, instruction delivery, project milestones, or a
  narrower set?
- While a model is thinking or unavailable, does the inhabitant continue its
  last valid intention, switch to local survival behaviour, or stop safely?
- How should a human instruction interact with an existing project, path,
  sleep state, or already-running action?
- What should the client expose as summarized decision factors, memories, and
  intentions without presenting hidden chain-of-thought as a game feature?

### Bodies, sleep, and starting conditions

- What are the exact fatigue, sleep, bed, shelter, exposure, and wake-up rules?
- Which starting preset is the default prototype: wild start, camp start, or
  settlement start, and what precisely does each place in the world?
- How are birth, development stages, care, adoption, inheritance, and death
  represented in the first playable society?
- If a human has not selected a newborn's provider, which temporary policy is
  used and when does provider assignment become fixed?

### Runtime limits and authoring

- Which game-side limits should exist independently of provider billing: calls,
  tokens, estimated cost, concurrency, wall-clock time, or local CPU/memory?
- What is the fallback when a provider limit is reached, an API is unavailable,
  or an Ollama model is overloaded?
- Which terrain/resource/object edits are allowed in paused authoring mode, and
  must every edit pause the world or only edits that affect simulation state?
- How are human-created assets imported, previewed, and approved relative to
  inhabitant-created proposals?

## Founders and family

- Should a world normally begin with one founder, a couple, or a small group?
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
