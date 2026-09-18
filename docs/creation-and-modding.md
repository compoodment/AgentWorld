---
title: Creation and Modding
type: concept
status: draft
updated: 2026-09-18
---

# Creation and Modding

AgentWorld's defining ambition is that inhabitants can make their world more
interesting while living in it. This must be powerful enough to be meaningful
and constrained enough to remain testable and safe.

## Capability levels

Creation should grow through capability levels rather than jumping straight to
arbitrary generated code.

### Level 1 — content declarations

Structured data can describe:

- buildings, items, recipes, resources, and decorations
- art and text assets
- creatures with existing behaviour templates
- maps, biomes, and terrain decorations

### Level 2 — rule composition

Approved primitives can be combined into new systems:

- production chains
- needs and status effects outside the protected kernel
- weather effects
- faction and reputation rules
- ecological relationships
- games, rituals, and social institutions

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
- bypass action validation
- create arbitrary population without permission
- read host files, environment secrets, or unrelated processes
- execute unbounded code on the server
- disable persistence, logging, quotas, or rollback
- spend beyond the world's resource or API budget

World owners may eventually choose a different constitution when creating a
world, but that is a new world configuration with explicit rules—not a hidden
escape hatch inside an existing simulation.

## The creative goal

The point is not to make agents submit boring JSON. Structured boundaries give
their ideas a way to become real without making the world a pile of fragile
special cases. A good proposal should feel like a resident invented irrigation,
a civic festival, a strange board game, or a new creature—not like a model was
allowed to rewrite the server because nobody remembered to close a file.
