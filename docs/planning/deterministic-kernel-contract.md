---
title: Deterministic Kernel and Recovery Contract
type: implementation-contract
status: active
updated: 2026-09-19
addresses:
  - 1
  - 4
  - 5
  - 11
  - 15
  - 19
  - 20
  - 21
  - 22
  - 23
  - 24
  - 50
  - 51
  - 52
  - 53
  - 54
  - 55
  - 57
  - 62
  - 66
  - 67
  - 68
  - 83
---

# Deterministic Kernel and Recovery Contract

This is the executable-design contract for Phase 1. It turns the protected
world model into fixtures that can be run without an LLM or a visual client.
It is authoritative for the details below; the [architecture direction](architecture.md)
remains the higher-level system shape.

## Core invariants

- A world advances only by integral `world_tick` values. A tick is never
  partially committed.
- The same genesis manifest, event inputs, installed-content lock, and contract
  version produce the same canonical state digest and ordered event log.
- A client, model response, save loader, asset, mod, or authoring tool requests
  a transition; only the kernel commits it.
- Wall-clock time never changes world state directly. A stopped or paused world
  has no unseen catch-up simulation.

## World identity, genesis, and saves

Every world save records a versioned `world_identity` containing:

```text
world_id, contract_version, simulation_version, schema_version,
clock_config_version, world_tick, world_seed,
generator_id, generator_version, canonical_generator_config_digest,
generation_attempt, initial_map_manifest_digest,
installed_content_lock_digest, asset_lock_digest
```

The initial map manifest records dimensions, tile/object placements, the
camp-start placement, and its canonical digest. A seed alone is not identity.
Changing a generator, generation configuration, clock configuration, content
lock, or asset lock is a versioned migration, never an implicit restore.

Persistence uses periodic snapshots plus an ordered append-only event log. A
snapshot records the exact tick immediately *after* its final committed event.
Replaying its suffix must produce the same state digest as a genesis replay.
Provider secrets are never part of a save; an unavailable opaque provider
binding restores as an explicit `unbound` reference and cannot silently resolve
to some other local credential.

### Migration rules

Migrations are directed, versioned, and preflighted on an isolated copy. The
preflight verifies every required schema, content, asset, and generator lock;
it emits either a compatible migration plan or a refusal with no live mutation.
Before activation the runtime checkpoints the source save, applies all migration
steps atomically, validates the resulting state, and records a migration event.
Interrupted work resumes from the prior checkpoint. Downgrades are refused
unless an explicit reverse migration exists. Missing optional provider bindings
may become `unbound`; missing required content/assets leave the world recoverable
for inspection but not runnable until a compatible migration or restoration is
supplied.

## Clock, pause, and resume

The first-world clock has **one simulation tick per in-world minute**. Its
default day therefore contains 1,440 ticks and is scheduled at six ticks per
real second for the current four-real-minute day. The scheduler may run slower
under load, but it must never skip or merge ticks. Calendar/daylight conversion
uses integer tick arithmetic from the saved clock configuration; host wall-clock
changes cannot move sunrise, season boundaries, deadlines, or needs.

`pause_requested`, `paused`, and `resumed` are durable control events. A pause
request received during a tick is ordered with other ingress, finishes that
already-started atomic tick, and enters `paused` before the next tick begins.
While paused:

- `world_tick`, all world timers, decay, deadlines, retries, and reservations
  are frozen;
- no new provider call is issued;
- in-flight provider work is best-effort cancelled and any late reply is stored
  only as an ignored diagnostic, never applied;
- queued actions and cognition requests remain as frozen records and are
  revalidated after resume;
- paused authoring is exclusive: it creates a separate validated authoring
  batch, never a hidden simulation tick.

Resume is idempotent. It creates a new run epoch, revalidates frozen work at the
next ingress phase, rejects stale replies, and then advances one normal tick.
A confirmed provider-wide outage uses this same full-world pause policy. An
individual provider failure can use local fallback before that threshold is
met; a provider-wide outage does **not** keep the rest of the simulation moving
in the background.

## Canonical tick order

Each tick uses the following total order. Every collection is ordered by its
immutable ID, never by map/dictionary iteration or arrival timing.

1. **Ingress:** append authenticated external requests and eligible prior-tick
   results; deduplicate by request ID and validate their run/config epochs.
2. **Clock and passive world effects:** advance one tick; apply calendar,
   weather, resource regeneration, item decay, and other declared passive
   transitions.
3. **Needs and health:** apply versioned integer transition functions, emit
   threshold events, then resolve injury, recovery, and terminal health checks.
4. **Lifecycle transitions:** commit due birth/death transitions. A terminal
   death makes the actor ineligible for later action phases in that tick.
5. **Reservations and movement:** validate reservations, calculate accepted
   moves as a batch, then commit occupancy changes atomically.
6. **Routine work and economy:** resolve production, consumption, transfers,
   offers, contracts, and release/complete reservations in stable request order.
7. **Communication and observation events:** deliver due messages and emit
   resulting observable events.
8. **Cognition queue:** coalesce triggers, enqueue requests, and record queue
   state. It cannot mutate the current tick's completed physical work.
9. **Validated decisions and control changes:** make newly accepted intentions,
   instructions, and configuration changes effective from the next tick.
10. **Commit:** assign monotonically increasing event IDs, write the state/event
    digest, and checkpoint when due.

Tie-break keys are `(priority, submitted_tick, submission_sequence, immutable_id)`.
An operation needing a different priority must declare it in its schema. Parallel
implementations must reduce to this same ordered result or reject the work.

## Randomness

The kernel uses `pcg32-xsh-rr-v1`: a portable 64-bit-state PCG32
implementation with serialized algorithm/version metadata. A stream's initial
state and odd increment are derived with HMAC-SHA-256 from the world seed and a
canonical stream name. Required names include:

```text
worldgen/attempt:<n>
weather
resource:<resource-id>
entity:<entity-id>
rule:<package-digest>:<rule-name>
```

Random draws belong to a committed operation. Rejected validation, preview,
retry, and inspection do not consume its live stream. Proposals/previews use an
isolated preview stream; packages have a declared random-draw quota. A change
to the algorithm, seed derivation, or stream namespace is a migration.

## Generated-map acceptance

A generator may try at most 32 deterministically derived attempts. The selected
attempt and manifest are saved. A valid first-world map must have:

- a bounded rectangular logical grid and explicit four-direction passability;
- one connected passable component containing every camp-start object;
- legal placements for shelter, bed/bedroll, storage, fire/cooking setup, and
  founders without overlapping impassable terrain;
- at least one reachable renewable food source and one reachable construction
  resource source; and
- no required route across an initially impassable river, mountain, or coast.

If no attempt meets the invariant set, world creation fails with the failed
attempt diagnostics rather than quietly accepting an unwinnable map. A fixed
seed corpus is a Phase 1 fixture.

## Movement and routing

The first prototype has one mobile inhabitant per ground tile; nonblocking
items may share a tile and buildings declare their occupied footprints. Routes
use four cardinal neighbors in `north, east, south, west` order. Costs are
integer travel quanta: base orthogonal movement is 100, declared terrain/road
and actor-profile modifiers are integer multipliers, and no diagonal movement
exists. A* uses Manhattan distance times the smallest legal move cost. Its open
set key is `(f, h, g, y, x, predecessor_y, predecessor_x)`; equal keys resolve
by the immutable route-node ID.

Movement is batch-resolved. An actor first claims its destination reservation;
claims are ordered by greatest accumulated `move_wait_ticks`, then immutable
actor ID. A direct two-actor swap is legal only when both reciprocal moves are
otherwise valid in the same batch. Longer cycles, pushes, and crossing through
occupied tiles are not legal in the first prototype. A losing actor remains in
place, increments its wait count, receives a `movement_blocked` event, and
revalidates/replans at its next action boundary. The batch atomically changes
occupancy, route progress, and reservations.

Route caches are non-authoritative and keyed by origin, destination, movement
profile, actor health/capability epoch, transport epoch, topology epoch, route
configuration version, and simulation version. Any cost-affecting change
invalidates the relevant cache. An inhabitant plans only through its known or
believed map; it learns a hidden change only by perception, communication, or a
failed authoritative route step. Caches never leak another inhabitant's
knowledge.

## Needs, resources, inventories, and work

Protected needs use unsigned integer basis points from `0` to `10,000` and a
versioned transition-function identifier saved in world configuration. Functions
take only committed state and integer modifiers, clamp to the range, and emit a
cause-tagged event on every threshold crossing. Eating, sleep, injury, recovery,
and death are ordered by the canonical tick phases above. Exact balance rates
are configuration data and may be tuned through a migration; units, order, and
clamping are not tunable by accident.

Resource nodes have explicit `available`, `depleted`, `regenerating`, and
`transformed` states. Extraction reserves and consumes a declared yield
atomically. The first-world baseline regenerates renewable plants/resources at
their recorded season/tick schedule; nonrenewable nodes remain depleted or
transform only through a declared world rule. There is no invisible respawn.

Inventory work uses stable reservation/job IDs and the state path
`available → reserved → partially_consumed_or_committed → completed_or_released`.
Reservations declare owner, quantity/lot, purpose, expiry tick, and exclusivity.
Reservations remain live **through** their expiry tick, supporting production
completion and immediate consumption at that boundary. They expire when the
world tick becomes strictly greater. Society clock advancement releases due
holds and cancels due open barter offers exactly once; expired offers cannot
gain even one-sided acceptance.
Cancellation, failure, death, or recovery releases only unconsumed quantities.
Access is never consent: a barter offer is immutable at a revision, each party
accepts that exact revision, and final availability/access/acceptance are
revalidated immediately before atomic settlement.

Perishable inventory is lot-based. Each lot has immutable identity, quantity,
condition, optional freshness, and the last processed tick. Condition and
freshness are basis points; passive decay occurs only in committed ticks, never
for offline wall time. Item schemas declare preservation modifiers and the
integer thresholds for usable, degraded, spoiled, toxic, or destroyed states.
Splits retain provenance and proportionate state; transfers and consumption
validate the exact lot. Spoilage/loss emits an event and cannot create negative
quantity, bypass a reservation, or be applied twice after restore.

## Messages, commands, and contracts

Every message has an immutable ID, sender authority/provenance, recipient
scope, creation/delivery tick, sequence number, visibility class, and retention
rule. Direct messages are reliable and private to their addressed recipients;
proximity messages may be overheard only through declared perception rules.
Sleeping or unavailable recipients retain delivered messages in their inbox;
duplicates are ignored by ID. Ordinary direct messages expire after the
versioned retention window (30 world days by default), while persistent
directives use their own lifecycle. All delivery, read, expiry, and rejection
events are replayable.

Human instructions/directives use server-minted IDs and authenticated issuer,
scope, precedence, and idempotency keys. Their lifecycle is
`queued → active → completed/rejected/superseded/cancelled`; a retry cannot
reapply an action. Only a verified control record can convey owner authority;
model output, summaries, signs, or forwarded messages cannot mint it.

Future contracts are versioned objects, not implicit promises. They bind exact
terms/revisions, parties, acceptance, world-tick deadlines, reservations,
partial settlement, cancellation/default, and terminal disposition. The first
world enables direct barter first; debt, wages, and contracts activate only when
their declared rule package supplies this lifecycle. The default after a party's
death is to freeze the contract, release unconsumed reservations, and route the
claim to the recorded estate process rather than silently charging or deleting
it.

## Client protocol and rendering boundary

The Phase 2 protocol starts with a major/minor version handshake, server and
client capability lists, request correlation IDs, idempotency keys, and typed
validation errors. A major mismatch rejects writes; unknown optional fields are
ignored only when their capability was not negotiated. Reconnect supplies a
snapshot plus ordered events from a known event ID.

Position snapshots are tick-indexed. Clients may interpolate only between the
last two authoritative positions; client prediction cannot reserve or commit a
tile. A rejected/blocked move corrects to the last committed tile, and late or
missing frames hold the last authoritative position until a newer snapshot or
reconnect baseline arrives. Visual events carry their causal tick, so animation
cannot display an impossible occupancy as canonical state.

## Phase 1 acceptance fixtures

The implementation gate includes fixtures for: identical genesis replay;
snapshot-plus-suffix replay; crash at each commit boundary; pause/resume at each
phase; equal-cost routes; movement contention/swaps; route invalidation after
injury/transport changes; a fixed seed corpus; exhausted/recovered needs;
harvest-to-zero and seasonal regeneration; lot split/spoilage/restore; exact
offer acceptance; duplicate commands/messages; migration interruption; and
provider-result rejection after pause or supersession. Each fixture compares a
canonical state digest and ordered event digest, not merely a pretty rendering.
