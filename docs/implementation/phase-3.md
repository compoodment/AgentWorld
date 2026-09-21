---
title: Phase 3 Implementation Plan
type: implementation-plan
status: complete
updated: 2026-09-22
---

# Phase 3 Implementation Plan

Phase 3 attaches one real, bounded cognition loop to the existing authoritative
world. The owner client remains an observer-director surface; it does not run
the inhabitant or apply model output locally.

## Starting point

Phase 2 is complete. The paired Godot client can observe the seeded world,
inspect the scripted inhabitant, and submit server-validated instructions. The
provider-neutral runtime, authoritative action executor, owner projection, and
opt-in Jev adapter now run in the same server-side boundary. The owner client
still does not run cognition locally.

## First implementation slice

Build the smallest end-to-end loop for one inhabitant:

1. define a versioned provider adapter and structured decision response;
2. create one event-driven cognition request from a committed observation;
3. admit at most one in-flight request for the fixture inhabitant;
4. validate the response against run epoch, provider epoch, decision generation,
   observation digest, and the declared action schema;
5. commit a destination-level intention or deterministic safe fallback;
6. let the existing authoritative movement/needs kernel execute the intention;
7. expose request, admission, response, rejection, fallback, and usage events
   through the owner projection;
8. prove restart, pause, provider failure, duplicate response, and stale
   response behaviour with a deterministic mock provider.

The Jev adapter is deliberately optional and separate from the deterministic
default. Credentials remain installation-local and are read from
`TYPESAFE_API_KEY` at request time; they never enter world state, saves,
telemetry, or owner observations.

## Explicit non-goals

- multiple independently thinking inhabitants
- social cognition, family growth, or emergent roles
- agent-created assets or executable mods
- a provider-specific prompt format becoming authoritative state
- a large cognition dashboard in the normal client
- further broad UI polish while the first cognition loop is unproven

## Gate

One inhabitant can make a meaningful, inspectable choice and survive while:

- the simulation remains authoritative and deterministic between decisions;
- malformed, late, duplicate, paused, and superseded responses cannot mutate
  current state;
- individual provider failure enters declared local fallback;
- provider-wide failure can pause the world and notify the owner;
- queue admission, retry, usage, and stop/fallback events are observable;
- the paired client can explain what the inhabitant decided without exposing
  raw private model reasoning.

Evidence for this checkpoint: 147 .NET tests pass, including deterministic
movement execution, restart persistence, bounded retry/fallback, provider
outage pause, and a fake-HTTP Jev contract test. The Godot 4.7.2 headless
startup and isolated Windows 11 x64 export checks also pass.

The [cognition and society contract](../planning/cognition-and-society-contract.md)
is the normative boundary for this work.

## Decision-provider policy

The first cognition implementation keeps provider choice behind a small typed
boundary in `AgentWorld.Simulation.Cognition`:

```text
authoritative observation + legal candidates
              |
              v
       IDecisionProvider
        /      |       \
 deterministic  Jev   large LLM
```

- The deterministic provider is always available and is the default. It is
  also the individual fallback when a hosted provider fails or has low
  confidence.
- Jev is optional. It is intended for small, bounded choices among candidates
  that the kernel has already generated; it is not an authority, planner,
  dialogue model, or source of new action IDs.
- A large LLM is optional and reserved for decisions that genuinely need
  open-ended planning, reflection, communication, or social reasoning.
- The server/kernel remains authoritative regardless of provider. Provider
  output can only select a currently legal candidate, never mutate world state
  directly.

Phase 3 is complete: typed observations and responses, one in-flight request,
provider/run/generation/observation-digest validation, one bounded retry,
confidence-based local fallback, pause/supersede rejection, provider-outage
pause, restart-safe cognition state, authoritative destination movement,
needs/resource execution, owner projection, usage events, and the opt-in Jev
HTTP adapter are covered by the implementation and tests. No Jev key is
required by the default runtime or CI.
