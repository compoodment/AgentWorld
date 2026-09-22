---
title: Private Single-World Alpha
type: implementation-plan
status: active
updated: 2026-09-22
---

# Private Single-World Alpha

The immediate product target is a feature-complete private single-world alpha,
not a deliberately thin vertical slice. It should contain the currently
decided first-world systems and their integrations before computment begins
hands-on playtesting. The purpose of playtesting is then to critique the actual
game's interaction, UI, pacing, model behaviour, and visual language—not to
discover that half of the agreed world is still absent.

This work is an integration and completion track across the existing roadmap,
not a new roadmap phase. Multiplayer/public-world work remains explicitly out
of scope.

## Current implementation checkpoint

The host integration now loads or creates a persistent four-inhabitant
settlement by default. The private runtime owns one checkpoint containing the
seeded map, society, cognition scheduler, physical positions/needs, inventory
transitions, content governance state, bounded ecology/weather/faction/law/
currency/culture/chunk state, and a replayable world-event stream. The owner
observation boundary projects all four active inhabitants, richer-world
summaries, content package status, and their bounded local knowledge.
`WorldMode=fixture` remains available for the older owner-protocol
compatibility suite, and mode-specific services no longer load each other's
saves.

The signed owner instruction path is live for the private runtime, including
idempotency, suggestive bias, legal `must_do` enforcement, and persisted queue
events. The signed private content path now accepts data-only package
definitions, validates typed building/recipe payloads, activates them on the
next world tick, persists them, and exposes rollback/quarantine. Aggregate
inert-asset budgets and metadata-only preview contracts are implemented as a
deterministic validation lane. The Godot surface now has a coherent temporary
layout for the map, roster, selected inhabitant, world controls, cognition
status, event history, content status, typed-content counts, and richer-world
summaries. The remaining work is deeper building/recipe/economy interaction,
world-wide asset reservation/cache accounting, isolated preview/test-world
execution, and final launch packaging. It is therefore materially closer to
the requested game, but is not being called playtest-complete yet.

## Alpha gate

The alpha is ready for first human playtesting when a fresh checkout can:

1. start one private world and the Godot client with a documented command;
2. show a small group of actual active inhabitants rather than authoring drafts
   or a single scripted actor;
3. advance a persistent world clock while an authenticated game client is
   present, stop shortly after the last client leaves, and retain explicit
   pause, resume, and speed controls without offline catch-up;
4. let the owner inspect inhabitants, needs, inventories, current intentions,
   relationships, work, and recent events;
5. let the owner issue suggestive and must-do instructions and observe their
   consequences;
6. run deterministic cognition, Jev, OpenAI, and Ollama Cloud through the same
   bounded structured-decision contract, with model selection in host config;
7. save, reload, and continue the same world without losing event history;
8. include the currently decided content/asset proposal and validation path,
   with active content and rollback surviving private-world reload;
9. include the explicitly scoped richer-world systems: ecology and weather,
   factions, law, currency, culture, expressive declarative rules, and a
   modest chunk-capable map boundary;
10. present a coherent temporary visual language that is readable and calm
    enough for UI feedback, even before final art or textures exist.

## Implementation order

### 1. Complete the live world integration

Replace the current one-actor owner fixture as the primary playtest source with
the society/cognition composition: multiple active inhabitants, deterministic
clock advancement, needs, movement, relationships, work, inventories, family
lifecycle, estates, and event projection in one save boundary.

### 2. Hosted model playtest boundary

The OpenAI-compatible adapter now supports OpenAI and Ollama Cloud endpoints.
The paired owner selects separate routine and planning provider roles and
supplies or forgets API keys in the game Settings screen. Jev may handle
routine survival while OpenAI or Ollama Cloud handles planning and work in the
same world. Credentials remain server-side in a separate restricted store, and
the server still validates every returned candidate.

### 3. Content and richer-world completion

Implement the decided data-only content and asset pipeline, then the named
Phase 6 world systems at a bounded first-world scale. Arbitrary executable
mods remain disabled until their sandbox/runtime contract is actually decided;
that is not a reason to omit the rest of the world.

### 4. Godot play surface

Evolve the current owner inspector into a usable game-facing surface: map,
roster, selected-inhabitant panel, event timeline, world controls, instruction
composer, cognition/model status, and clear failure states. Keep the authority
boundary unchanged.

### 5. Temporary visual language

Use a deliberately small procedural/vector vocabulary first: readable terrain,
distinct inhabitant markers, restrained palette, consistent typography, clear
selection states, and legible event/need indicators. Final asset production can
follow actual playtest criticism.

### 6. Playtest loop

Package the launch path, provider setup, seeded world, save location, and known
limitations. Then iterate from observed playtest feedback instead of adding
more speculative systems.

## Decisions and deferrals

No product decision currently blocks the alpha. The executable-content sandbox
question remains deferred and disabled; it is the one documented capability
that is not safe to invent. Multiplayer/public worlds remain deferred by
choice. Provider credentials are supplied by the paired owner in the game and
are never returned to the client or written to saves or the Godot client.
