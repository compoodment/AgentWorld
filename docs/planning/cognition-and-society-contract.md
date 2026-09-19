---
title: Cognition, Authority, and Society Contract
type: implementation-contract
status: active
updated: 2026-09-19
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
| owner | world configuration, all observable state, directives, paused authoring | grants/revokes roles; may enable developer capture |
| operator | only owner-granted world operations | no raw cognition by default |
| viewer | read-only observable state/event history | no private memory or raw trace |
| developer | explicitly enabled diagnostics for the local private world | redacted structured trace; raw capture only under the policy below |

Every access, grant, revocation, authoring batch, and authority-bearing command
is an audited server-issued record. Revocation takes effect at the next ingress
phase and invalidates pending permissions. The omniscient player view means
complete **world state**, not automatic access to an inhabitant's private
memories, raw prompt, raw model response, or credentials.

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

### Queue fairness and backpressure

The queue uses four declared bands: `critical`, `urgent`, `normal`, and
`background`. Critical survival behaviour is deterministic immediately; its
model request is optional and cannot block it. Within a band, requests sort by
oldest eligible tick then inhabitant ID. Each waiting request receives an
integer age boost after its configured maximum wait, so normal/background work
cannot starve indefinitely under a stream of newer requests. Per-inhabitant
in-flight concurrency is one; repeated triggers coalesce into the existing
eligible request.

Queue limits, coalescing intervals, provider parallelism, and maximum wait are
versioned configuration values, measured during prototypes rather than hidden
constants. At saturation the runtime coalesces first, then records a
backpressure event and uses deterministic fallback; it never drops a request
silently or exceeds the provider/account policy.

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

The first-world newborn rule is `hybrid`: inherit a shared valid parent binding
only when all recorded parents who have an active provider assignment agree on
the same capability-compatible binding; otherwise snapshot the world default.
If neither is valid, the child is born `unbound` rather than receiving a random
or secret configuration. Later world-default changes do not silently rewrite a
per-inhabitant override or a birth snapshot.

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

## Cognition cost and privacy boundary

Provider/account limits are the primary billing boundary. The runtime may stop
issuing new calls because of owner pause, provider outage, declared queue limits,
or an emergency stop, but it does not pretend to control a provider's billing.
Every stop/fallback/outage decision has a durable non-secret event.

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

Partnership, reproduction, property transfer, access to a private inventory or
space, and non-emergency care require the affected inhabitant's own valid
consent. Emergency care may be attempted by an actor, but cannot force a target
to provide consent or invent a relationship. A must-do order controls only the
ordered actor's attempt. Household and caregiver status are separate from
biological/legal parentage, so separation or death can update care obligations
without rewriting history.

Birth is a single idempotent lifecycle transaction. It snapshots readiness,
reserves declared food/housing/care/safety inputs, allocates a child identity,
records parent/caregiver/household/provider-policy bindings, creates
age-appropriate initial state, commits population accounting, and emits one
birth event. Failure releases all unconsumed reservations. The first-world
policy has no global population cap or default human approval gate; material
conditions and consent are the gate.

Age is calculated solely from birth tick and the versioned calendar. The
first-world protected thresholds are infant `0–2`, child `2–12`, adolescent
`12–18`, and adult `18+` in world years, with an optional `elder` social band
from 65. Transition events occur at the first tick of the new age year. Kernel
capability gates use these fixed bands; cultures may add ceremonies but cannot
override protected consent/safety rules. A child has full identity and private
experience from birth, guardian/caregiver support rather than ownership, and no
automatic power to publish/export/alter world constitution. At adulthood, only
the declared protected capability gates change; authority changes are audited
typed transitions.

## Death, estates, and dependent work

Death is terminal and historical. Its atomic transition freezes the actor,
cancels queued cognition/commands and active jobs, releases unconsumed
reservations, invalidates pending offers, marks relationship edges, updates
caregiver obligations, and records the precise cause/tick. It never erases
prior memories, ownership, debts, or events.

The first-world estate default is: settle valid reserved obligations, hold the
remaining estate in escrow for seven world days, then transfer it to the active
household/caregiver beneficiary set; if none exists, transfer it to the
settlement communal inventory. Perishable lots keep decaying in escrow. A
constitution/rule package may replace that policy only through a declared,
versioned migration. Contracts without a surviving explicitly accepted
counterparty freeze and route their claims through the same estate event;
nothing silently disappears or charges a dead actor.

## Society acceptance fixtures

Fixtures cover: duplicate/superseded model replies; starvation aging under
queue pressure; individual failure/recovery and provider-wide pause; missing
provider binding restore; child-provider conflict; secret redaction; memory
correction/forgetting; simultaneous relationship changes; consent refusal under
a must-do command; birth retry/replay; every age boundary; death with a job,
reservation, contract, and caregiver tie; estate expiry; and role revocation
during an active observation session.
