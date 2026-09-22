---
title: Phase 4 Implementation Ledger
type: implementation-ledger
status: complete
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

The bounded Phase 4 implementation is complete. The society fixture and its
composition root are authoritative, deterministic, replayable, and independent
of hosted providers. The optional cognition scheduler is reconciled with the
active lifecycle population after every society mutation.

## Implemented slices

1. **Multi-inhabitant scheduler:** independent per-inhabitant runtimes, one
   in-flight request per actor, deterministic priority ordering, coalescing,
   bounded queue backpressure, lifecycle reconciliation, and persistence.
2. **Relationship graph:** proposal, consent, acceptance, refusal, revocation,
   partnership cardinality, privacy classes, and deterministic relationship
   events.
3. **Households and care:** household membership, caregiver projection,
   protected family edges, birth readiness, and removal/death review.
4. **Authoritative exchange:** direct transfer and simple barter through the
   existing inventory/reservation ledger, including cancellation on death and
   deterministic estate settlement.
5. **Family lifecycle:** idempotent birth commit, food reservation and
   consumption, child provider resolution, age-band transitions, natural and
   hazard death, relationship tombstones, and replayable estate escrow.

## Evidence

- `SocietyFixture` owns immutable checkpoint transitions.
- `SocietyWorldRuntime` composes society and cognition into one save/restore
  boundary and removes dead inhabitants from the active cognition population.
- `SocietyCognitionScheduler` provides deterministic multi-inhabitant fairness,
  coalescing, backpressure, and runtime persistence.
- `SocietyCheckpointCodec` provides versioned JSON round-tripping with stable
  state and event digests.
- `SocietyWorldRuntimeCodec` persists the combined society and cognition
  boundary and rejects schema or active-population mismatches on restore.
- `InventoryFixture` exposes authoritative reservation consumption and
  provenance-preserving transfers for society operations.
- `SocietyTests` covers relationship consent/cardinality, idempotent
  birth, mortality and estate settlement, transfer/barter, cognition fairness,
  lifecycle reconciliation, death cleanup, combined runtime persistence,
  caregiver projection, organizations, and checkpoint replay (11 Phase 4
  tests; 158 tests in the full suite).

The fixture population size is a test-load parameter, not a product population
cap. All hosted-provider use remains opt-in and measurable; deterministic
providers make the fixture playable without credentials.

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

The Phase 4 gate is met by the bounded authoritative fixture: the repository
contains replayable acceptance fixtures, and society/cognition state restores
without losing causal ordering or active-population agreement. Rendering a few
inhabitants is not the evidence; checkpoint and event invariants are.

## Accepted mortality model

Natural mortality is now an accepted first-world policy:

- there is no hard maximum age;
- risk is zero before elderhood and rises gradually from the elder band;
- ordinary outcomes should cluster around roughly 80–100 world years, with
  rare survivors reaching approximately 110–120 under the current tuning;
- hazards, illness, and accidents may kill earlier;
- the elder band is a social/lifecycle marker, not an automatic death trigger;
- the kernel, never an inhabitant or provider, commits the death outcome;
- the mortality curve is versioned configuration and can be tuned from replay
  evidence without changing the policy.

The default curve remains below 100% at every finite age, preserving the
no-hard-maximum rule. This is a gameplay tuning target, not a promise about a
particular individual lifespan.
