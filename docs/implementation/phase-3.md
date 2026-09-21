---
title: Phase 3 Implementation Plan
type: implementation-plan
status: active
updated: 2026-09-21
---

# Phase 3 Implementation Plan

Phase 3 attaches one real, bounded cognition loop to the existing authoritative
world. The owner client remains an observer-director surface; it does not run
the inhabitant or apply model output locally.

## Starting point

Phase 2 is complete. The paired Godot client can observe the seeded world,
inspect the scripted inhabitant, and submit server-validated instructions. The
current observation explicitly reports that cognition is not active yet. The
deterministic mock/provider-result fixtures in the kernel prove stale-result
rejection, but they are not a live cognition runtime.

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

The first hosted provider integration is deliberately separate from the mock
runtime gate. Credentials remain installation-local and never enter world
state, saves, or telemetry.

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

The [cognition and society contract](../planning/cognition-and-society-contract.md)
is the normative boundary for this work.
