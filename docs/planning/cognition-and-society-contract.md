---
title: Cognition, Authority, and Society Contract
type: implementation-contract
status: active
updated: 2026-09-22
addresses:
  - 6
  - 7
  - 9
  - 13
  - 14
  - 25
  - 26
  - 27
  - 28
  - 29
  - 30
  - 31
  - 32
  - 33
  - 34
  - 56
  - 58
  - 59
  - 60
  - 61
  - 63
  - 64
  - 65
  - 69
  - 70
---

# Cognition, Authority, and Society Contract

This contract defines the non-physical state that must remain deterministic,
private where appropriate, and unable to counterfeit authority. It supplements
the [deterministic kernel contract](deterministic-kernel-contract.md), which
owns tick ordering and durable commits.

## Authority and human access

An inhabitant is a full identity, not property. A human owner configures a
world, but the owner cannot use a must-do instruction to make a different
inhabitant consent, transfer property, enter a private space, form a
relationship, reproduce, or give a particular reply.

The first private-world roles are:

| Role | Normal access | Sensitive access |
| --- | --- | --- |
| owner | world configuration, the complete world-state projection, directives, paused authoring | grants/revokes roles; may enable developer capture or a named private-observation grant |
| operator | only the owner-granted world-operation scopes | no private memory, raw prompt, or raw response by default |
| viewer | read-only public world state and event history | no private memory or raw trace |
| developer | explicitly enabled diagnostics for the local private world | redacted structured trace; raw capture only under the policy below |
| no-access | none, including provider status and world existence beyond an explicit share | none |

Every access, grant, revocation, authoring batch, and authority-bearing command
is an audited server-issued record. Revocation takes effect at the next ingress
phase and invalidates pending permissions. The omniscient player view means
complete **world state**, not automatic access to an inhabitant's private
memories, raw prompt, raw model response, or credentials.

An access request carries a principal ID, world ID, requested projection and
operation, role-grant ID, grant epoch, target scope, request ID, and creation
tick. The server authorizes it at ingress against the grant that is effective
for that tick; clients cannot select a stronger projection by changing a field
in the request. The first-world projections are `public_world`,
`owner_world`, `private_inhabitant`, `diagnostic`, and `raw_capture`:

- `public_world` contains world facts and causal events safe for a viewer;
- `owner_world` adds complete observable state and configuration, but still
  omits private memory and raw cognition unless a separate named grant exists;
- `private_inhabitant` is limited to the inhabitant and explicitly authorized
  recipients of that record's visibility class;
- `diagnostic` contains filtered observations, validation, fallback, and
  response hashes for a granted local developer/operator scope; and
- `raw_capture` is the expiring, host-protected developer capture described
  below and is never part of ordinary remote viewing.

Until a remote role service is implemented, Phase 2 remote viewing is
owner-only. Later viewer/operator grants use the same server-issued records and
revocation rules; they do not broaden the `owner_world` or private projections.
An owner may understand causality from public events and validation reasons
without being granted the private content that led to an inhabitant's choice.

### Trusted control records

Instructions, directives, and owner broadcasts contain an immutable command ID,
issuer ID, authenticated authority scope, target scope, creation tick,
submission sequence, requested strength, expiry/cancellation state, and a
server signature/reference. Observation builders may state that a command is
trusted only by referring to this record. Plain text, memories, signs, a model
claim, or a copied message never creates authority. A revoked or superseded
record cannot be revived by replaying its text.

## Cognition request lifecycle

`CognitionRequest` has its own immutable request ID, inhabitant ID, trigger set,
observation digest, created tick, priority, run epoch, provider-config epoch,
and **decision generation**. The decision generation changes whenever a later
fact makes a response obsolete: a new must-do instruction, emergency,
death/incapacitation, cancellation, restore/migration, provider rebinding, or a
newer accepted intention.

Responses can commit only once and only when their request ID, run epoch,
provider-config epoch, decision generation, and observation preconditions still
match. A late or duplicate response is recorded as `superseded`/`duplicate` and
never replaces a current intention. This remains true when the provider/model
did not change.

A request is dispatched from a committed observation boundary. Its trigger set
is an ordered set of immutable trigger IDs, not a free-text reason. A trigger
records its source class, semantic key, source tick, and event ID. An in-flight
request is never edited in place: a later trigger increments the inhabitant's
decision generation and either joins the next eligible aggregate or causes a
fresh request after the current response is validated. This prevents a response
for an old observation from being made current merely because it arrived last.

### Queue fairness and backpressure

The queue uses four declared bands: `critical`, `urgent`, `normal`, and
`background`. Critical survival behaviour is deterministic immediately; its
model request is optional and cannot block it. Within a band, requests sort by
the resolver's source class, then oldest eligible tick and inhabitant ID. Each
waiting request receives an
integer age boost after its configured maximum wait, so normal/background work
cannot starve indefinitely under a stream of newer requests. Per-inhabitant
in-flight concurrency is one; repeated triggers coalesce into the existing
eligible request.

Queue limits, coalescing intervals, provider parallelism, and maximum wait are
versioned configuration values, measured during prototypes rather than hidden
constants. At saturation the runtime coalesces first, then records a
backpressure event and uses deterministic fallback; it never drops a request
silently or exceeds the provider/account policy.

The first-world queue resolver is deterministic:

1. `critical` contains an emergency/survival trigger. Its local survival
   transition runs first and a model request is advisory only.
2. `urgent` contains an active danger trigger, then a valid must-do instruction
   at an eligible decision point, then a relationship/family event. The order
   within these classes is oldest eligible tick, submission sequence, and
   inhabitant ID.
3. `normal` contains project, failed-action, discovery, and ordinary routine
   reconsideration. `background` contains reflection and non-urgent upkeep.
4. For otherwise equal work, the key is
   `(effective_band_rank, source_class_rank, -age_boost, eligible_tick,
   submission_sequence, inhabitant_id, request_id)`. A waiting request gains
   one age boost at every `max_wait_ticks` interval, as declared by the active
   queue configuration. For queued (non-`critical`) work,
   `effective_band_rank = max(urgent_rank, band_rank - age_boost)`; this lets a
   sufficiently old normal/background request enter the urgent service class
   without ever outranking the deterministic critical transition. The source
   class rank still makes must-do work precede relationship work, and
   relationship work precede routine work at the same effective band.

Coalescing uses `(inhabitant_id, decision_boundary, semantic_key)` during the
configured coalescing window. The first eligible request ID is retained; the
aggregate keeps every distinct trigger ID in sorted order, raises its band to
the strongest trigger, and uses the latest committed observation digest at
dispatch. Identical trigger IDs are idempotent, and no distinct event is
silently erased. A trigger arriving after dispatch belongs to the next request
and invalidates the old decision generation when it can change the outcome.
Per-inhabitant concurrency is one, while the scheduler's source cursor visits
inhabitants and event-source classes in immutable-ID order after each age
promotion. Thus a single actor or source cannot consume all ordinary slots.

Admission has two explicit outcomes. If a provider slot is available, the
resolver dispatches the next request. If the queue, provider semaphore, or
configured operational guard is full, it records `cognition_backpressure` with
the retained request/trigger IDs and executes the declared deterministic
fallback at that boundary; it does not pretend that a call occurred. Under a
finite-load fixture with at least one available slot, every non-cancelled
request is dispatched or receives fallback by its configured starvation bound
(`max_wait_ticks` plus the declared in-flight drain), and the bound is part of
the fixture oracle rather than an informal promise.

## Provider configuration and failures

A provider configuration is installation-local and non-secret. World state may
reference only:

```text
provider_binding_id, capability_fingerprint, provider_config_epoch
```

The capability descriptor declares a structured-action schema/version, maximum
context/output sizes, timeout/error classes, and optional tool/vision/streaming
support. Reliable structured JSON matching the declared action schema is the
only required first-world capability. Assignment runs a preflight probe; a
missing or mismatched binding restores as `unbound` and uses the configured
deterministic safe behaviour until the owner explicitly rebinds it. Secrets,
headers, raw credential values, and provider account identifiers never enter a
save or telemetry record.

Provider precedence is:

1. a valid explicit per-inhabitant override;
2. the provider binding resolved and snapshotted at that inhabitant's birth;
3. the valid current world default; then
4. `unbound` deterministic fallback.

The assignment source is persisted as one of `explicit_override`,
`birth_snapshot`, `world_default_snapshot`, or `unbound`; the effective binding,
capability fingerprint, and provider-config epoch are persisted with it. An
inhabitant with an explicit override keeps it until the owner explicitly
changes or removes that override. Removing it returns to the birth snapshot
when one exists; otherwise the current valid world default is snapshotted in
that same configuration transaction, or the inhabitant becomes `unbound`.
Changing the world default changes future snapshots and currently unbound
inhabitants only. It never silently rewrites an explicit override or a birth
snapshot. Every explicit assignment/removal and default change is an audited
`provider_assignment_changed` event containing old/new opaque binding IDs,
capability fingerprints, source, reason, effective tick, and new config epoch;
it contains no credentials.

The first-world newborn rule is `hybrid`: inherit a shared valid parent binding
only when all recorded parents who have an active provider assignment agree on
the same capability-compatible binding; otherwise snapshot the world default.
If neither is valid, the child is born `unbound` rather than receiving a random
or secret configuration. Later world-default changes do not silently rewrite a
per-inhabitant override or a birth snapshot.

`newborn_provider_policy` is a versioned world setting whose first-world
default is `hybrid`. If a valid explicit per-child policy is supplied, it is
used; if it is absent or invalid, `hybrid` is used rather than selecting a
provider by model output or arrival timing. At the birth commit boundary, the
runtime first validates every recorded parent's assignment and capability,
then applies this resolver:

- with one or more active assigned parents, all must resolve to the same
  capability-compatible binding for inheritance;
- with disagreement, an unbound parent, or no eligible parent, use the valid
  world default snapshot from the birth commit's world-config epoch; and
- with no valid default, persist `unbound` and run deterministic safe behaviour.

The parent set, policy value, default snapshot, capability checks, resulting
source, and config epoch are included in a `newborn_provider_resolved` event.
World-config changes are effective only at the kernel boundary that activated
their epoch, so concurrent births in one committed tick see the same declared
epoch and are ordered by the kernel's birth request key. A restore preserves
the assignment source and opaque binding; a missing or incompatible binding
becomes `unbound` and rejects old-epoch responses until the owner rebinds it.

### Failure state machine

An individual request gets one bounded retry. It then enters `fallback` for
that inhabitant, records the failure class, and continues safe local behaviour
until the next eligible cognition boundary. A valid preflight/successful probe
can restore normal cognition at that boundary; all fallback-era or old-epoch
responses are rejected.

The runtime classifies failures as individual response, model, authentication,
quota/account, network, or provider-wide health failure. A confirmed
provider-wide outage is a configured quorum of failed independent health probes
within one versioned window; it requests the full-world pause defined in the
kernel contract and emits one deduplicated owner notification. Recovery needs a
successful probe quorum and an explicit owner resume; it never silently starts
spending provider resources again.

Failure scope is part of the event, not inferred from a human-readable error:

| Class | Scope and first response | World-pause effect |
| --- | --- | --- |
| malformed/invalid individual response | reject the response, perform the one bounded retry, then use that inhabitant's fallback | none |
| model- or capability-specific failure | mark the binding/model capability degraded and use fallback for assigned inhabitants | no full pause while an independent compatible binding remains healthy |
| authentication or quota/account rejection | stop retrying that binding, retain the opaque assignment, and require rebind/health recovery | pause only when the failed account/region scope covers every active world binding |
| network or endpoint failure | record an observation for the affected health domain and use individual fallback | pause only after the health quorum below |
| provider-wide health failure | enter the outage epoch and request the kernel full-world pause | full-world pause at the next atomic boundary |

The health domain is `(provider_binding_id, account-scope, region,
capability-fingerprint)`. An individual request failure never counts as an
independent health probe. The active `ProviderHealthConfig` records
`failure_window_ticks`, `failure_probe_count`, `failure_quorum`,
`recovery_window_ticks`, `recovery_probe_count`, `recovery_quorum`, and the
probe backoff schedule. These are versioned world/installation configuration,
not hidden constants. The first implementation must use a fixed configuration
in every replay fixture; it need not promise that the initial values are the
right production tuning.

Within a failure window, the resolver counts distinct probe IDs and health
domains, ordered by probe ID. A domain enters `suspect` after the configured
quorum of failed probes and enters `outage` only when that domain covers all
active bindings required by the world (or an installation-wide stop is
explicitly asserted). One notification is emitted per `(world_id,
provider_health_epoch, outage_scope)`; repeated failures update diagnostics but
cannot spam the owner or create extra pause requests. A model-only or
region-only failure therefore does not pause unrelated healthy assignments.

Recovery enters `recovering` only after the configured success quorum of
independent probes is observed across the recovery window. Probe successes are
ordered and deduplicated exactly like failures; a failed probe resets the
recovery streak according to the saved configuration rather than causing
pause/resume flapping. The runtime records `provider_recovery_ready`, but the
world remains paused until an authenticated owner `resume` event. Resume
creates the kernel run epoch, revalidates every queued request, and allows
provider calls only at the next normal boundary.

For an individual fallback, the inhabitant stores `fallback_entered_tick`,
failure class, `next_probe_tick`, and a versioned bounded backoff index. A
probe is a non-committing structured request made with the current binding and
capability epoch. A valid response that passes schema and capability checks
marks the inhabitant `recovery_ready`; it does not apply an action. At the
next eligible cognition boundary the runtime emits `fallback_recovered`,
increments the decision generation, builds a fresh observation, and resumes
normal dispatch. Any fallback action or authoritative event between probe and
handoff wins, and all earlier model responses remain `superseded`. While the
world is paused, no recovery probe is issued; recovery resumes only after the
owner's explicit resume.

## Cognition cost and privacy boundary

Provider/account limits remain the primary billing boundary. The first-world
contract deliberately has **no numeric game-side token, dollar, or per-world
cognition budget** and does not claim that an in-game counter can cap a hosted
provider's bill. Population and gameplay are therefore not made affordable by
an invented hidden ceiling. Provider usage/cost data supplied by the provider
may be observed and reported, but it is not authoritative admission state.

The runtime does have a configurable, non-budget **operational guard** so a
healthy provider cannot make the local worker exhaust its queue, memory, or
concurrency. `CognitionOperationalGuardConfig` is versioned and persisted with
the run; it contains only operational controls such as:

```text
max_inflight_calls, max_queued_requests, dispatch_window_ticks,
max_dispatches_per_window, per_inhabitant_cooldown_ticks,
max_context_bytes, max_output_bytes, retry_limit
```

These values are admission/backpressure and host-safety controls, not a claim
about money or tokens and not a population cap. The chosen configuration must
be included in the replay fixture and can be tuned through a versioned config
migration. A guard can trip while the provider account is healthy, and an
account can still charge for work already admitted or in flight.

The durable control signal is `cognition_stop_requested` with a stop ID,
scope (`installation`, `account`, `provider`, `world`, or `inhabitant`), reason,
config epoch, issuer, and effective tick. A non-emergency operational guard stops
starting new calls in its scope and lets already admitted calls finish; queued
requests remain identifiable and take deterministic fallback at their eligible
boundary while the guard is tripped. An owner emergency stop stops new calls,
cancels in-flight work best-effort, marks late replies ignored, and uses
fallback for queued work. Neither stop automatically pauses the world; an
explicit `pause_requested` or the provider-wide outage transition uses the
kernel's full pause semantics. Scope precedence is installation, account,
provider, world, then inhabitant, with the broadest active stop winning. Every
stop/trip/fallback/outage decision has a durable non-secret event.

The Phase 3/4 cost gates are therefore operational and measurable: with a
fixed scripted workload and saved guard configuration, no call starts after an
effective stop, in-flight/late responses follow the declared policy, every
suppressed request has a fallback/backpressure event, and the replayed state
and event digests match. The fixture reports provider-supplied usage separately
and does not assert a game-side currency/token ceiling.

Telemetry fields are classified before persistence:

- **public world facts:** observable state/events safe for normal viewers;
- **private inhabitant facts:** memories, relationship context, and message
  content visible only through an authorized observation projection;
- **restricted diagnostics:** filtered observation and validation metadata;
- **never persist:** credentials, authorization headers, and any secret found
  by a fail-closed scrubber.

Ordinary saves retain summaries, event references, and response hashes. Raw
structured model responses are optional local developer capture, encrypted or
access-controlled by the host, visible only to the owner/developer role, and
expire after the versioned retention policy (30 real days by default). Exports,
shared viewers, ordinary replay fixtures, and public issue attachments strip
raw/private material. Access, export, deletion, and retention expiry are
audited; a deletion tombstone preserves causal evidence without retaining the
content.

## Memory and communication

Memory records have immutable IDs, type, source/provenance, source tick,
subject scope, confidence, privacy class, expiry/retention policy, and optional
supersedes/tombstone reference. Perception and messages create proposals for
memory, not authoritative world facts. Exact duplicates merge deterministically;
new evidence can lower confidence or supersede a stale belief, while the old
record remains historical. Forgetting is a scheduled retention/tombstone event.
Authoritative state always wins when validating action; a false memory can still
explain a bad plan without corrupting reality.

The write boundary is explicit. The kernel may write authoritative world facts
and lifecycle evidence; an inhabitant/model may submit a
`MemoryWriteProposal`; a message reducer may propose a memory for each allowed
recipient; and a shared-culture rule may publish only through its declared
social mechanism. No model output, retrieved summary, owner directive, or
private message directly writes another inhabitant's personal memory. The
reducer validates the proposal's `memory_id`/idempotency key, source event,
subject scope, visibility, observed tick, confidence in basis points, and
retention policy before emitting `memory_accepted`, `memory_rejected`, or
`memory_tombstoned`.

The first-world memory reducer uses these deterministic operations:

- An exact proposition is keyed by `(owner_id, layer, type, subject_id,
  normalized_proposition_digest, privacy_class)`. A duplicate keeps the first
  memory ID, appends distinct evidence references in `(source_tick, source_id)`
  order, and sets confidence to the greater of the existing and supporting
  evidence values (capped at 10,000).
- A contradictory proposition does not overwrite the old one. It keeps a
  separate belief, records both IDs in a `memory_conflict` event, and applies
  the versioned first-world reducer `max-support-min-disconfirmation-v1`:
  disconfirmation with confidence `c` changes the old confidence to
  `floor(old * (10,000 - c) / 10,000)`. An explicit, authorized correction
  may instead supersede the old record; the superseded record remains
  historical and is never silently deleted.
- A `forget` proposal is valid only for the owning inhabitant's non-authority
  memory or for a scheduled expiry/retention event. It creates a tombstone with
  ID, cause, source reference, and effective tick; the content is absent from
  later observation projections but the causal tombstone remains replayable.

`personal`, `spatial`, and `working` records are visible only to their owner
and explicitly permitted recipients. A `shared` record is visible only after
the declared message/culture transition and retains the source visibility and
provenance. Private message content cannot become shared culture by being
summarized. World facts are public only when the authoritative world projection
allows them; an inhabitant's belief about a world fact is not itself authority.

Observation assembly filters records by visibility first, then applies the
versioned selection policy `(active, subject relevance, confidence, recency,
memory ID)` and `max_memory_records`/`max_context_bytes` limits. Summaries are
derived caches carrying source IDs and a source digest; they may help build a
new proposal but cannot create an authoritative fact or bypass a privacy
filter. Save/load persists accepted memory events, reducer/config versions,
and tombstones, so a stale belief, correction, forgetting, and cross-inhabitant
projection produce the same state and event digests on replay.

Direct/shared messages follow the kernel message lifecycle. Memories derived
from a message retain its visibility/provenance; they cannot become shared
culture or owner authority without an explicit, permitted social mechanism.

## Relationships, consent, and family lifecycle

A relationship edge is a typed, directed, immutable-ID record with endpoints,
proposal/acceptance references, effective tick, status, consent state,
household/caregiver role, privacy class, and history/tombstones. Edge changes
are typed transitions: `proposed`, `accepted`, `rejected`, `revoked`,
`dissolved`, or `ended_by_death`. Concurrent changes use the kernel ordering and
cannot resurrect a revoked/dissolved edge without a new proposal.

The first-world edge policy is:

| Edge type | Proposer/acceptor | Default cardinality and effect |
| --- | --- | --- |
| `partnership` | either capable participant proposes; the other participant must accept with current valid consent | at most one active partnership per participant under the first-world relation schema; it affects social/family eligibility only while active |
| `caregiver` | an adult caregiver proposes or a birth/dependency transaction assigns one; a capable dependent accepts, while an infant's protected lifecycle record supplies the acceptance boundary | many caregivers and dependents are allowed; active edges create care obligations but not ownership |
| `household_membership` | each member accepts, except the birth/death transaction's initial protected assignment | many members; it grants only the declared household privileges, never private-space access by itself |
| `biological_parentage` | created only by the committed birth record from its recorded parents | immutable historical edge; it is not a later access grant and cannot be revoked by a model response |
| `legal_guardian` | created or removed by the applicable protected lifecycle/rule transaction with the affected parties' valid consent where possible | versioned cardinality; it supplies care authority, not ownership of the dependent |

The relation schema is versioned with the world. A duplicate proposal or
acceptance for the same edge/revision is idempotent. Acceptance must name the
exact proposal revision and be received while both endpoints are alive,
eligible, and consenting; a stale, revoked, or superseded revision is rejected
without creating a replacement edge. A participant may revoke its own active
partnership, caregiver, or membership edge. Dissolution by a rule transaction
must name its protected reason and cannot silently rewrite parentage. The
proposer may cancel a pending proposal, the target may explicitly reject it,
and only the affected participant(s) or a declared lifecycle/rule transaction
may revoke or dissolve an active edge. An owner or must-do order may request
one of those transitions but cannot perform another inhabitant's acceptance.
If a cardinality or capability conflict remains after kernel ordering, the
later transition gets `relationship_rejected` with the conflicting edge IDs;
the runtime does not auto-dissolve an existing tie to make room.

An accepted transition records proposal ID, acceptance/consent evidence,
effective tick, relation-schema version, and the actor that requested it. The
transition becomes effective at the next lifecycle boundary after its ordered
commit. Active caregiver and household edges are the only relationship edges
used for current care/access queries; historical, revoked, dissolved, and
death-ended edges remain queryable but cannot authorize a new action. Death
creates `ended_by_death` transitions for affected edges and a deterministic
care-obligation review; survivors must use a new proposal for any replacement
relationship. A must-do instruction can make its ordered actor propose an edge,
but cannot accept it for another inhabitant or bypass consent.

Partnership, reproduction, property transfer, access to a private inventory or
space, and non-emergency care require the affected inhabitant's own valid
consent. Emergency care may be attempted by an actor, but cannot force a target
to provide consent or invent a relationship. A must-do order controls only the
ordered actor's attempt. Household and caregiver status are separate from
biological/legal parentage, so separation or death can update care obligations
without rewriting history.

Birth is a single idempotent lifecycle transaction. A `BirthRequest` has a
server-issued request ID/idempotency key, ordered parent IDs, caregiver and
household candidates, readiness observation digest, requested tick, rule/config
epochs, and a stable result reference. The kernel processes same-tick requests
by `(requested_tick, submission_sequence, birth_request_id)` and never validates
two requests against the same unreserved input.

The concrete society request may carry an optional nonblank display name of at
most 80 characters. It never replaces the world/request-derived child identity
or idempotency key; omitting it retains the legacy generated display name.

The transaction has these boundaries:

1. **Readiness:** validate the affected participants are alive and eligible,
   the required relationship/consent records are current, and the declared
   food, housing, care, safety, and rule inputs exist. This is a proposal and
   creates no child identity.
2. **Reservation:** reserve every declared lot/capacity with stable reservation
   IDs in sorted resource order. A competing birth sees the post-reservation
   state. If any reservation fails, emit one `birth_rejected` event with the
   reason and release all reservations made by this request.
3. **Commit:** allocate a child ID in the same atomic commit using the
   world/request identity namespace, record biological parentage, accepted
   caregivers, household, newborn provider resolution, age-zero state, and
   only the declared age-appropriate initial memories. Consume the reserved
   inputs, increment population accounting, and emit exactly one `birth_committed`
   event. No parent raw memory is copied.
4. **Failure/replay:** a crash before `birth_committed` leaves no visible child
   and releases/replays reservations from the last checkpoint; a duplicate
   request after commit returns the original child/result reference without
   consuming inputs again. A failed commit releases unconsumed quantities and
   records the failed boundary; it never leaves a partial family graph.

The child identity is the canonical tuple `(world_id, birth_request_id)` (or
its versioned equivalent), allocated from the request's stable namespace, so
retry cannot create a second identity. A required caregiver who dies or becomes
ineligible before commit invalidates readiness and rejects the request; an
optional caregiver is removed and the minimum-care precondition is rechecked.
Provider policy/config is snapshotted at the birth commit boundary, after
reservations and before the `birth_committed` event, using the newborn resolver
above. The first-world policy has no global population cap or default human
approval gate; material conditions and consent are the gate.

Birth fixtures save/replay immediately before readiness, after each reservation,
after a caregiver death, immediately before commit, after commit, and on a
duplicate retry. Same-tick competing births must produce the same accepted or
rejected set, reservation ledger, child IDs, provider assignment events, and
population/state digests in every replay.

Default age is calculated from birth tick and the versioned calendar. Lifecycle
contract 2 additionally permits an explicit pause-only owner choice of biological
pace: 1, 365 or 1460 biological ticks per world tick. A persisted world/life
anchor makes a change prospective: current ages and historical birth ticks never
jump. Newborns record their biological birth tick when this clock is active.
Weather, seasons, inventory deadlines, simulation cadence and model polling are
not accelerated. Switching back to rate 1 retains accumulated biological age.
The first-world protected bands are half-open intervals in biological years: infant
`[0,2)`, child `[2,12)`, adolescent `[12,18)`, and adult `[18,∞)`, with an
optional `elder` social band `[65,∞)` layered on adult. At exactly the first
tick whose calendar age reaches a boundary, the lifecycle phase emits one
`age_band_transition` before cognition and validated decisions for that tick;
the new band is used by those later phases. Pausing freezes the boundary rather
than skipping it, and resuming emits any one due transition in calendar/tick
order. A save/replay or migration recalculates from preserved birth dates,
biological anchors when enabled, and calendar version; it cannot advance age
from wall-clock elapsed time. Without an opt-in anchor, old saves retain their
original aging semantics and serialized representation.

The first-world natural mortality policy is explicit: there is no hard maximum
age. Natural-death risk is zero before elderhood and rises gradually from the
elder band, with the finite-age curve remaining below certainty. The current
tuning targets ordinary deaths around 80–100 world years and rare survivors
around 110–120; hazards, illness, and accidents may occur earlier. The `elder`
band is a social/lifecycle marker, not an automatic death trigger. The kernel
owns the death roll and commit, and provider output can neither choose nor
forge a death outcome.

Protected capability gates are versioned kernel rules, not cultural labels.
From birth a child has a full identity, private experience, and age-appropriate
local/model cognition, plus guardian/caregiver support rather than ownership.
Before adulthood the child cannot independently authorize reproduction or
partnership, bind a protected property transfer/private-space grant, publish or
export private world data, or alter the world constitution. The adult boundary
enables only those declared gates; it does not grant consent to any particular
person, erase caregiver history, or force a relationship. The exact protected
gate set and rule version are recorded in the transition event. Culture may add
ceremonies, apprenticeship, or recognition at any age, but an early, delayed,
or missing ceremony cannot change a kernel gate.

An age transition increments the inhabitant's decision generation so a response
assembled under the old age band cannot commit afterward. It does not silently
rebind the provider, alter the provider-config epoch, or copy memories. The
next cognition boundary uses the new age-specific action schema; if no model is
available, existing deterministic fallback remains safe. Transition records
include old/new band, birth tick, calendar/config digest, effective tick, gate
version, and the causally ordered ceremony events. Fixtures cover exact ticks
at 2, 12, 18, and 65 years, pause/resume at each boundary, delayed ceremony,
restore/migration, and rejection of an old-band model response.

## Death, estates, and dependent work

Death is terminal and historical. Its atomic transition freezes the actor,
cancels queued cognition/commands and active jobs, releases unconsumed
reservations, invalidates pending offers, marks relationship edges, updates
caregiver obligations, and records the precise cause/tick. It never erases
prior memories, ownership, debts, or events.

The death reducer performs the following ordered work in one lifecycle commit:

1. mark the inhabitant `dead` with an immutable death-event ID, cause, source,
   and tick; the identity remains readable but is no longer eligible to act,
   receive new cognition, accept commands, or provide consent;
2. cancel queued/in-flight cognition and fallback actions, active jobs, and
   scheduled callbacks. A late model result, command, or callback becomes an
   ignored historical event and cannot mutate the world. A message delivered
   before death remains in read-only history; an undelivered outbound message
   is canceled, and a post-death direct message is rejected as
   `recipient_dead`;
3. freeze contracts and offers, settle only already-valid atomic obligations,
   release unconsumed reservations, and route any remaining claim through the
   estate event. Completed work and consumed lots remain historical; an
   unfinished job never invents output or charges a dead actor;
4. move owned inventory/property claims into an estate escrow ledger, revoke
   access tokens granted by the actor, mark affected relationship edges
   `ended_by_death`, and emit the caregiver/household review. Parentage remains
   historical; current care queries use only surviving active caregivers and
   require a new valid transition for reassignment.

The settlement care review exposes a voluntary offer to eligible adults in the
same household when a minor has no surviving active caregiver. For an infant,
the protected transition records only the volunteering adult's acceptance; it
does not fabricate the infant's consent. Older dependents independently accept
or refuse the exact proposal. Pending proposals expire, participants can
withdraw, and parentage/birth records are never rewritten. A competing offer
revalidates against the already committed assignment. This bounded caregiving
path does not implement a separate legal-guardianship institution.

The estate is identified by the death event and cannot be created twice. At
death it snapshots the eligible active household/caregiver beneficiary IDs,
sorted by immutable ID, and holds the remaining lots for seven world days.
Valid reserved obligations settle before distribution. At escrow expiry,
surviving beneficiaries receive equal integer shares in sorted-ID order, with
indivisible remainders assigned from the beginning of that order; a beneficiary
who died or lost eligibility is removed and the shares are recalculated. With
no eligible beneficiary, the estate transfers to the settlement communal
inventory. Perishable lots decay only in committed ticks while escrowed. The
estate policy is versioned and may be replaced only by a declared constitution
migration, never by a model response.

Replay applies a death event and estate ID idempotently. Restoring a snapshot
before death and replaying its suffix reproduces the same terminal identity,
reservations, relationship tombstones, and estate digest. The first world has
no gameplay resurrection. A future corrective migration may reopen the same
identity only with an explicit versioned migration event proving the original
death was erroneous; it cannot mint a second identity or rewrite the original
death history. Otherwise, a pre-death restore is a new world lineage rather
than an in-place resurrection.

The first-world estate default is: settle valid reserved obligations, hold the
remaining estate in escrow for seven world days, then transfer it to the active
household/caregiver beneficiary set; if none exists, transfer it to the
settlement communal inventory. Perishable lots keep decaying in escrow. A
constitution/rule package may replace that policy only through a declared,
versioned migration. Contracts without a surviving explicitly accepted
counterparty freeze and route their claims through the same estate event;
nothing silently disappears or charges a dead actor.

## Society acceptance fixtures

Fixtures cover at minimum:

- **authority/privacy:** owner, operator, viewer, developer, and no-access
  projections; owner-only remote Phase 2 viewing; operator scope denial;
  private-message/memory exclusion; raw-capture expiry; access/export/revocation
  audit records; and a public causal event view that contains no forbidden
  private content;
- **queue:** emergency local fallback before model dispatch; must-do versus
  relationship versus routine ordering; repeated-trigger coalescing without
  dropping trigger IDs; one actor/source under load; age promotion and the
  configured starvation bound; saturated provider slots; and duplicate/late
  model replies;
- **cost/guard:** healthy-provider dispatch until a saved operational guard
  trips; no call after an effective scoped stop; in-flight completion versus
  emergency cancellation; queued fallback/backpressure records; provider usage
  reported separately from the guard; and replay of the same stop/event digest;
- **provider:** default change, explicit override removal, missing binding
  restore, mixed-provider newborn parents, unset/conflicting newborn policy,
  concurrent births, capability mismatch, and assignment audit/epoch replay;
- **health/recovery:** one malformed response, model-only failure, account or
  region failure with another healthy binding, transient timeout, sustained
  failed-probe quorum, deduplicated notification, flapping recovery, explicit
  owner resume, individual fallback backoff, safe handoff, and stale-response
  rejection;
- **memory:** duplicate merge, contradictory witnesses, explicit correction,
  expiry/tombstone, destroyed landmark, stale food/identity/relationship fact,
  private-message leakage attempt, shared-culture authorization, bounded
  summary assembly, save/load, and authoritative-state precedence;
- **relationships/family:** simultaneous proposals and revokes, stale
  acceptance, cardinality conflict, consent refusal under a must-do command,
  household split, caregiver removal, death-ended edges, historical queries,
  birth retry/replay, competing same-tick births, reservation rollback,
  caregiver death at each birth boundary, and child-provider resolution;
- **age/death:** exact age boundaries and delayed ceremonies, pause/resume,
  migration, old-band response rejection, death with a job/reservation/
  contract/caregiver tie, late result/message/callback rejection, estate expiry
  and beneficiary death, restore/replay idempotence, and the constrained
  corrective-migration rule for resurrection.

Every fixture records the versioned configuration/contract epochs and compares
canonical state and ordered event digests. A passing fixture must also assert
the absence of forbidden side effects (duplicate identity, hidden provider
call, private-field export, stale action, dangling active edge, or unaccounted
reservation), not merely that a preferred event was emitted.
