---
title: Phase 4 Implementation Plan
type: implementation-plan
status: planned
updated: 2026-09-22
---

# Phase 4 Implementation Plan

Phase 4 is the first multi-inhabitant and society experiment. It extends the
authoritative Phase 3 cognition boundary without turning provider output into
world authority or silently introducing an unbounded population simulation.

The normative rules are already recorded in the
[cognition and society contract](../planning/cognition-and-society-contract.md)
and the accepted policy defaults are in the
[decision register](../decisions/decision-register.md). This document is the
implementation ledger for evidence produced against those rules.

## Documentation baseline

The Phase 4 policy baseline is settled enough to implement:

- inhabitants are independent identities with one in-flight cognition request
  each, subject to shared queue fairness and provider backpressure;
- relationships use typed, consent-aware edges; household membership,
  caregiver status, biological parentage, and legal guardianship are distinct;
- inventories, ownership, reservations, and exchange remain authoritative
  kernel state and settle atomically;
- birth requires consent and material readiness, has no global population cap,
  and creates one idempotent child identity;
- age is calculated from birth tick and the versioned calendar, and protected
  age-band transitions are deterministic;
- death is terminal and historical, while estate settlement, relationship
  tombstones, caregiver review, and replay remain explicit kernel events;
- deterministic cognition remains the default; Jev is optional for bounded
  choices and large-model cognition remains optional for open-ended work.

The implementation is not started yet. The open lifespan/mortality question is
tracked separately so it cannot be accidentally answered by code defaults.

## First bounded implementation slices

1. **Multi-inhabitant scheduler:** replace the one-inhabitant admission fixture
   with independent per-inhabitant schedules, one in-flight request per actor,
   shared queue fairness, coalescing, and observable backpressure.
2. **Relationship graph:** implement proposal, consent, acceptance, revocation,
   cardinality checks, privacy classes, and deterministic relationship events.
3. **Households and care:** implement household membership, caregiver
   obligations, household projections, and split/removal/death review.
4. **Authoritative exchange:** expose direct transfer, gift, and simple barter
   through the existing inventory/reservation ledger; do not add a mandatory
   currency or economic ideology.
5. **Family lifecycle:** implement birth readiness, reservations, idempotent
   birth commit, child provider resolution, age transitions, and death/estate
   replay once the lifespan policy is explicitly settled.

The fixture population size is a test-load parameter, not a product population
cap. All hosted-provider use remains opt-in and measurable; deterministic
providers must continue to make the fixture playable without credentials.

## Explicit non-goals

- a fixed global population limit;
- a complete banking, taxation, credit, law, or currency system;
- multiplayer roles or public-world hosting;
- agent-created executable content or mods;
- a large social dashboard or raw private-model trace in the owner client;
- silent natural-death semantics chosen only to make a test pass.

## Phase 4 gate

A bounded settlement fixture must demonstrate that:

- several inhabitants can make independent progress without starvation or
  unbounded cognition queue growth;
- relationship proposals and consent cannot be forged by owner commands or
  provider output;
- ownership, reservations, exchange, care, and household changes are atomic
  and replayable;
- birth, age transitions, and death cannot create duplicate identities,
  dangling active edges, stale actions, or unaccounted inventory;
- provider usage, fallback, pause, and recovery remain observable per actor and
  at world scope;
- the owner projection explains causal public outcomes without exporting raw
  private memory, prompts, responses, or credentials.

Every accepted slice must add replayable fixtures and update this ledger with
the relevant commit, test counts, and remaining scope. The Phase 4 gate is not
met merely because a few inhabitants render on screen.

## Current open policy question

The existing contract defines age bands and the mechanics of a death transition,
but it does not yet choose the first-world natural lifespan model. Before the
family-lifecycle slice is implemented, decide whether natural aging death is:

- absent from the first playable society, with death initially limited to
  authoritative hazards and explicit world rules;
- present with an age-based mortality curve and no hard maximum; or
- present with an age-based curve plus a declared maximum-age rule.

The choice should also establish whether the optional `elder` social band at
65 is only a social marker or a gameplay mortality signal. The exact numerical
curve belongs in versioned configuration and fixture evidence after the policy
choice.
