---
title: Creation and Modding
type: concept
status: draft
updated: 2026-09-19
---

# Creation and Modding

The package identity, activation, compatibility, quota, and constitutional
rules are specified in the
[content-governance contract](../planning/content-governance-contract.md).

AgentWorld's defining ambition is that inhabitants can make their world more
interesting while living in it. This must be powerful enough to be meaningful
and constrained enough to remain testable and safe.

## The governing distinction

Creation should preserve **creative sovereignty** without granting **reality
sovereignty**. Inhabitants may invent a crop that changes food scarcity, a
building that transforms work, or an art style nobody expected. They may not
forge health, create inventory from nothing, bypass ownership, or rewrite the
kernel's causal records.

The goal is to restrict impossible state mutations, not powerful ideas.

## Capability levels

Creation should grow through capability levels rather than jumping straight to
arbitrary generated code.

### Level 1 — content declarations

Structured data can describe:

- buildings, items, recipes, resources, and decorations
- art and text assets
- creatures with existing behaviour templates
- maps, biomes, and terrain decorations
- visual assets and asset metadata; see [Assets and Art Pipeline](assets-and-art.md)

### Level 2 — rule composition

Approved primitives can be combined into new systems:

- production chains
- needs and status effects outside the protected kernel
- weather effects
- faction and reputation rules
- ecological relationships
- games, rituals, and social institutions
- economic rules such as prices, wages, contracts, and exchange mechanisms

### Level 3 — sandboxed behaviour

Only after the security and compatibility model is proven should inhabitants
generate executable behaviour. Candidate technologies include a restricted DSL,
WebAssembly, or a separate sandboxed process. Generated GDScript must not be
executed freely on the VPS.

## Proposal pipeline

Every world-changing proposal follows the same broad lifecycle:

1. The inhabitant describes a problem, idea, and intended change.
2. The agent runtime emits a structured proposal.
3. The validator checks schema, capabilities, dependencies, and resource costs.
4. An isolated test world exercises the proposal.
5. Representative simulation runs check for crashes, impossible state, runaway
   resource use, and kernel violations.
6. World policy decides whether to auto-apply, queue for owner review, or reject.
7. The accepted proposal receives a version and is applied atomically.
8. The event log records who proposed it, what changed, and how to roll it back.

Human-created assets use the same normalization and provenance path, but the
human is the approving authority rather than an inhabitant. After format and
performance checks, a human-created visual asset may be used immediately in
paused authoring mode. New behaviours and rules still require the deeper
validation path. Paused authoring mode may preview and place approved human or
inhabitant content; it does not turn asset files into executable host code.

Authoring may create resources from nothing, but every such placement is an
explicit, recorded god-mode intervention rather than an ordinary world action.
The initial authoring surface includes terrain, rivers and water, resources,
trees and plants, buildings, inhabitants, weather and seasons, and approved
assets.

## What a proposal must declare

The eventual format should require at least:

- name, author, version, and description
- requested capabilities
- dependencies and conflicts
- affected content and rules
- inputs, outputs, and resource costs
- expected effects and known limitations
- test cases or simulation scenarios
- compatibility range
- migration and rollback plan

## Protected boundaries

No mod or inhabitant proposal may directly:

- edit kernel health or mortality semantics
- write directly to hunger, health, ownership, inventories, or transaction
  history
- bypass action validation
- create arbitrary population without permission
- read host files, environment secrets, or unrelated processes
- execute unbounded code on the server
- disable persistence, logging, quotas, or rollback
- bypass provider/account controls or any future configured world cognition
  limits

World owners choose a constitution when creating a world. A constitution may
later change only through an explicit, paused, validated migration: either the
owner authorizes it, or an in-world constitutional process authorizes it when
the current constitution has explicitly delegated that authority. It is a
versioned historical event, not a hidden escape hatch inside an existing
simulation. No constitution may bypass protected kernel invariants.

## Inhabitant authorship and authority

Inhabitants can author more than decorative content. They may propose tools,
items, buildings, recipes, clothing, art, festivals, customs, professions,
organizations, crops, domesticated species, local events, and declarative world
rules. A fungus crop and its harvest festival are ordinary world creativity;
altered biology, resurrection, or new physical laws are higher-risk changes.

Validated data-only creations may enter the world through ordinary activity
under the world policy. Major rule changes require the authority assigned by the
world constitution—by default the world owner, or an explicit in-world process
where that constitution permits it. Every accepted creation remains subject to
resource costs, spatial checks, quotas, and the protected boundaries above.

Inhabitant-authored creations are private to the world by default. A human
world owner may export one as a reusable package with its author/provenance,
dependencies, compatibility range, declared capabilities, permissions, and
preview/test results. A public registry is a later feature. Inhabitants do not
gain automatic authority to publish, install executable code, or grant their
own proposals privileges.

## The abundance test

Every proposal that appears to remove a survival constraint must answer:

- what inputs and infrastructure does it require?
- what time, space, labour, or ecological conditions limit it?
- what happens when it fails, spreads, spoils, or is monopolized?
- which inventories and transactions record its effects?
- is it changing a world rule, or attempting to forge kernel state?

An instant, eternal crop that simply sets hunger to zero fails the test. A
resource-intensive crop that produces large quantities and changes prices,
labour, ecology, and power may pass it. The latter is a world-changing
discovery, not an exploit.

## The creative goal

The point is not to make agents submit boring JSON. Structured boundaries give
their ideas a way to become real without making the world a pile of fragile
special cases. A good proposal should feel like a resident invented irrigation,
a civic festival, a strange board game, or a new creature—not like a model was
allowed to rewrite the server because nobody remembered to close a file.
