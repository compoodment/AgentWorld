---
title: Open Questions
type: design
status: active
updated: 2026-09-18
---

# Open Questions

These are intentionally unresolved. Closing them should produce a decision,
prototype result, or explicit reason to defer—not a guess hidden in code.

## World and experience

- How much direct control should a human have after creating a world?
- Is the default experience an observer, a god, a participant, or a mixture?
- What makes an event “interesting enough” to wake an LLM?
- Is there a soft goal, narrative arc, or only survival and self-directed life?
- How much simulation should happen in distant regions?
- What does time mean: real-time, accelerated time, or configurable time?

## Founders and family

- Should a world begin with one founder, a couple, or a small founding group?
- Can a founder self-name and self-describe, or must the creator approve its
  identity?
- What exactly counts as a family and how are relationships represented?
- How are consent, partnership, birth, adoption, and care modelled without
  reducing them to a crude “population button”?
- Is the population limit fixed per world, resource-based, or both?
- Are new inhabitants allowed only through family, or can authorized players
  add immigrants later?
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

## LLM runtime

- Which model/provider should be the first supported default?
- What is the observation format and maximum context size?
- How often should an inhabitant receive a cognition turn?
- Which decisions deserve separate LLM calls versus local rules?
- What personal memory is useful enough to retain?
- What happens when the API is unavailable or the budget is exhausted?
- How do we evaluate whether a choice is coherent without judging it only by
  how entertaining it sounds?

## Technology

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
- Which systems are essential for the first compelling world: farming,
  crafting, inventories, barter, ecology, trade, weather, combat, or something
  else?
- How many inhabitants can the target VPS run at acceptable cost?
- What is the first feature that should be removed if the foundation becomes
  too complex?
