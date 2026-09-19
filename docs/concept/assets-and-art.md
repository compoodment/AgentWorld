---
title: Assets and Art Pipeline
type: concept
status: frozen
updated: 2026-09-19
---

# Assets and Art Pipeline

The deterministic asset identity, resource limits, and export rights/integrity
requirements are specified in the
[content-governance contract](../planning/content-governance-contract.md).

AgentWorld needs a visual world without making every inhabitant a trusted game
developer. The key separation is:

> An inhabitant may propose what something looks like and how it behaves, but
> an image or generated file never gets to execute code by itself.

## Asset sources

The pipeline may accept:

- project-maintained base art
- human-contributed sprites, tiles, sounds, and illustrations
- procedural or palette-based generation
- optional image-generation models
- inhabitant-authored pixel grids, vector-like shapes, descriptions, or
  transformations of existing permitted assets

The concept does not lock one final art style yet. A coherent world may have a
shared palette and rendering language while still allowing regional, cultural,
or historical variation.

## Inhabitant asset proposal

An inhabitant does not write directly into the running game's asset folder. It
submits a structured proposal such as:

```text
Name: Sunroot
Type: crop
Appearance: pale orange roots with teal leaves
Animation: four-frame wind cycle
Footprint: 1×1 tile
Interactions: plant, water, harvest
Style: current settlement palette
```

The proposal links visual data to an existing content schema. Behaviour comes
from declared interactions, recipes, and validated world rules, not arbitrary
instructions hidden in the image file.

## Normalization and validation

An asset pipeline should:

1. accept a candidate image, shape, animation, or description
2. check provenance, permissions, dimensions, transparency, and format
3. normalize scale, palette, anchor points, animation timing, and naming
4. validate collision, footprint, layering, and interaction metadata
5. preview the asset in an isolated test scene
6. run the associated content proposal through simulation validation
7. version the accepted asset and record its author and dependencies
8. apply it atomically or reject it without touching canon

Bad-looking art is not automatically invalid. A strange local style may be a
real cultural creation. The hard checks are safety, format, permission,
performance, and consistency with the declared content behaviour.

## Generated art is still world history

An accepted asset should record:

- source type and provenance
- creator or proposing inhabitant
- content and asset version
- dependencies and replacement history
- the world event that introduced it

An ugly mushroom chair, an elegant civic mural, or a crop with an accidental
colour mutation can become part of a society's history. Rejected candidates
remain outside the active world, while rollback keeps accepted versions
recoverable.

## Capability boundary

Asset data may describe appearance, sound, text, animation, and inert metadata.
It may not:

- execute arbitrary host code
- modify health, hunger, ownership, or persistence directly
- bypass collision, access, or transaction validation
- consume unlimited memory, storage, or rendering time

An inhabitant-created asset can be visually spectacular and behaviourally
important, but its effects still enter through the same declared action and mod
pipeline as project-authored content.

## Initial recommendation

The first visual prototype should use a small, hand-authored or procedural
base set plus a few generated candidates. The pipeline should be proven before
promising fully autonomous image generation, because the difficult problem is
not producing a picture; it is turning a picture into safe, inspectable,
versioned world content.
