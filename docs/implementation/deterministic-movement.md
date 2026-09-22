---
title: Deterministic Movement Evidence
type: implementation-evidence
status: complete
updated: 2026-09-22
---

# Deterministic Movement Evidence

This is the evidence record for [#91](https://github.com/compoodment/AgentWorld/issues/91).
It adds the first-world's pure movement-resolution fixture to the existing
cardinal A* route finder. Its input is actor positions plus one-tile requests;
its output is a sorted next actor state and explicit moved/blocked events.

## Resolution rules

- A request must target one passable north/east/south/west neighbor.
- Competing claims for one destination are ordered by greatest
  `move_wait_ticks`, then immutable actor ID.
- An accepted move resets its wait counter; a losing request stays in place,
  increments it, and receives `movement_blocked` with a reason.
- A direct two-actor reciprocal swap is legal when both moves are otherwise
  valid.
- Longer cycles, pushes, and movement through another occupied tile are
  rejected together; no actor in a rejected cycle partially moves.

The resolver sorts input actors and intents by immutable ID before producing
output, so caller collection order cannot choose the winner. It has its own
canonical state and event digest helpers for fixture comparison.

## Route cache boundary

Routes remain derived, non-authoritative data. `NonAuthoritativeRouteCache`
keys a computed route by origin, destination, movement profile, actor capability
epoch, transport epoch, topology epoch, route configuration version, and
simulation version. A changed injury/capability or transport epoch therefore
causes a fresh route computation even if the current grid happens to yield the
same path.

## Reproduce

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet test --configuration Release --no-restore
```

The movement fixture tests contention with reversed caller order, actor-ID
tie-breaking, legal swaps, rejected cardinal four-actor cycles, and cache
recomputation after capability/transport changes. Each repeated resolution
compares canonical state and ordered-event digests.

## Limits

This is the pure movement phase model, ready to attach to the staged tick
transaction from [#90](https://github.com/compoodment/AgentWorld/issues/90).
It does not yet run a multi-actor world through every kernel phase, implement
route cost modifiers, persist a cache, reveal routes to a client, or add crowds
and pathfinding beyond the first-world rules.
