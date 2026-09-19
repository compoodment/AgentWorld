---
title: Content, Mod, and Constitutional Governance Contract
type: implementation-contract
status: active
updated: 2026-09-19
addresses:
  - 2
  - 3
  - 8
  - 10
  - 16
  - 17
  - 35
  - 36
  - 37
  - 38
  - 49
  - 71
  - 72
  - 73
  - 74
---

# Content, Mod, and Constitutional Governance Contract

This contract makes world creation extensible without turning ordinary content
into unreviewed host mutation. It refines the [world model](../concept/world-model.md)
and [creation and modding](../concept/creation-and-modding.md) for Phase 5
implementation work.

## Namespaces, identity, and locks

Every content definition has an immutable canonical ID:

```text
<package-digest>/<kind>/<local-id>@<version>
```

`kind` is one of `item`, `recipe`, `building`, `species`, `asset`, `rule`,
`event`, `organization`, or another registered schema type. Human-readable
labels are not identifiers. A world may add display aliases, but an alias maps
to exactly one immutable ID and is not usable when ambiguous. IDs are never
silently reused: removal creates a tombstone; rename/replacement requires an
explicit alias/migration record.

An installable package is identified by its canonical manifest bytes and
content digest. Its resolved dependency graph is stored as a sorted immutable
lock of package IDs, versions, digests, compatibility decisions, and declared
capabilities. Preview/test verifies those exact bytes; activation verifies the
same lock. A missing/mismatched artifact fails closed or enters the quarantine
state below. A human-readable version tag, folder name, archive name, or
repository ref is never enough to reproduce a world.

## Kernel extension surface

Packages cannot write kernel state directly. The extension surface has three
classes:

1. **Immutable kernel fields/transitions:** identity/lifecycle, clock/tick
   order, coordinates/occupancy, inventory conservation, permission checks,
   consent, terminal death, persistence, and security boundaries. No package
   can replace or delete these.
2. **Versioned tuning parameters:** declared bounded values such as a weather
   distribution or a recovery modifier. A parameter has range, unit, owner,
   default, conflict rule, and migration semantics; changing it is never an
   arbitrary overwrite.
3. **Additive package-owned state:** namespaced content state, visual data,
   recipes, events, and declared status effects. It can request a validated
   kernel effect (`apply_status`, `modify_need`, `spawn_declared_output`,
   `schedule_event`) but cannot set health/hunger/rest/inventory fields itself.

Multiple valid effects apply in the deterministic kernel order, then by package
digest and effect ID. An uninstall cannot delete referenced state; it requires a
validated migration/conversion or leaves the package definitions available in a
read-only quarantined state. A valid crop that declares inputs/output and
seasonal costs is additive. A package that sets `hunger = 0`, changes the
calendar outside a migration, or silently revives an inhabitant is rejected as
a kernel rewrite.

## Proposal, activation, and paused authoring

All content uses the lifecycle:

```text
proposed → validated → approved → staged → active
```

Validation produces a digest, dependency lock, schema result, preview/test
result, and required activation class. Approval is policy/constitution action;
it is not activation. Staging creates a checkpoint and a durable manifest-change
event. Activation either happens at a deterministic next-tick boundary or as a
paused migration:

- **data-only world-local content** (for example, an item, recipe, building,
  art, crop with existing schema) may activate at the next tick after validation
  and staging *only* when it requires no schema/content migration and the world
  policy allows automatic adoption;
- **declarative rules, package dependency changes, parameter changes, or any
  migration** activate only during an explicitly paused, previewed migration;
- **executable, network, filesystem, or host-integration capabilities** are
  disabled by default and need separate stronger review/isolation.

Thus an inhabitant's ordinary-world activity may create and stage a safe
data-only object without invisible world drift. The manifest change, activation
tick, checkpoint, and rollback route are always visible and replayable.

Paused authoring is a typed, all-or-nothing batch. A batch has an ID, ordered
operations, issuer/provenance, precondition digest, validation report, and
resulting event IDs. Direct authoring may place/replace validated terrain,
resources, objects, buildings, weather configuration, and approved appearance
data. It may not directly overwrite identity, alive/dead state, age, ownership,
inventory quantity, health/needs, consent, relationship history, permissions,
provider binding, or event history. Those require their normal kernel
transition. The batch validates all cross-edit dependencies before any live
mutation, commits atomically, and is retry-idempotent; crash/replay either sees
the entire batch or none of it.

## Dependencies and compatibility

Dependency resolution occurs before any activation. The resolver:

1. loads the graph by immutable ID/digest;
2. rejects missing required dependencies, version/digest mismatch, declared
   conflicts, and all cycles with a deterministic diagnostic path;
3. evaluates optional dependencies explicitly as present/absent features;
4. resolves a stable topological order by package digest; and
5. writes the final lock used for preview, activation, rollback, save, and
   restore.

Compatibility uses explicit dimensions rather than an ambiguous `compatible`
flag: simulation/kernel API, save schema, mod API, asset format, constitution
profile, declared capability profile, and any declared provider capability.
Ranges use SemVer comparison with an inclusive lower and exclusive upper bound
(`>=a.b.c <d.e.f`). Unknown versions, missing dimensions, and unsupported
pre-release semantics fail closed. Preflight evaluates the exact lock against a
checkpointed copy; a package/digest/configuration difference after preflight
requires a new preview. Migration order is dependencies first, dependents next;
rollback reverses that order.

## Runtime quotas and quarantine

Quota failure rolls back only the failing current transaction. It then enters
`quarantined` execution state: the offending callbacks cannot run, but the
versioned definitions needed to interpret already committed inventory, objects,
and history remain available. Dependent packages/jobs enter a recorded
`suspended` or `failed_dependency` state according to their declared recovery
policy; queued callbacks are cancelled; unconsumed reservations are released
only when their owner/job contract permits it.

Re-enable, uninstall, or state conversion is a separate paused compatible
migration. If the runtime cannot safely preserve dependent committed state, it
pauses the affected world for recovery rather than delete data or run dangling
behaviour. Saves record the quarantine/suspension state and never invoke
disabled executable behavior merely to load.

## Assets: deterministic identity, limits, and rights

The first asset format is inert PNG plus JSON metadata. Normalization is a
versioned deterministic algorithm that produces a canonical normalized file
and digest. Stable asset ID/version is separate from display name. Name/ID
collisions reject by default; replacement creates a new immutable version with
explicit references/aliases/tombstones. Normalization is idempotent and gets a
cross-platform fixture digest.

Initial acceptance limits are versioned policy, not renderer accidents:

- PNG only; at most 4 MiB compressed and 16 MiB decoded per asset;
- at most 2,048 × 2,048 pixels, 256 animation frames, and 60 frames/second;
- at most 512 assets / 64 MiB decoded data per activated package;
- isolated decode/preview with a two-second CPU timeout and no network/file
  access beyond the candidate; and
- rejection/quarantine plus a provenance event for a decode bomb, budget breach,
  malformed input, or aggregate load breach.

An export manifest additionally contains source kind, creator/proposer, rights
holder where known, upstream/derivative chain, original and normalized digests,
license identifier or bundled license reference, attribution/notices,
export/redistribution status, and dependencies. `unknown` rights block export
by default. Repository-authored base assets use the repository MIT license
unless their asset manifest explicitly says otherwise; imported/generated/
transformed material retains its own stated rights. Runtime capability
permissions are never confused with copyright/redistribution permissions.
Installation verifies exact digests and preserves notices through replacement
and derivative chains.

## Constitutional changes

Kernel safety and causality cannot be amended. A constitution may delegate
authority over a major declarative rule only through a versioned proposal,
notice, decision, paused validation, and migration record. The first-world
default is human-owner approval; an in-world process is opt-in rather than an
unstated democracy.

The standard delegated process has a proposal ID, eligible living-adult
electorate snapshot, seven-world-day notice/voting period, majority quorum of
eligible voters, and strict majority of cast valid votes. A tie, missed quorum,
or invalidated proposal expires with no change. An owner may override only if
the current constitution explicitly delegates that override; otherwise the
owner proposes through the same process. Death, revocation, or disconnection
does not rewrite the electorate snapshot. A passed change stages a paused,
tested, reversible migration; a failed test leaves the old constitution active.

## Acceptance fixtures

The Phase 5 gate includes fixtures for: ambiguous aliases; package-digest
mismatch after preview; dependency cycle/conflict/missing optional dependency;
compatibility range failure; data-only next-tick activation; paused rule
migration; forbidden authoring overwrite; authoring crash/retry; quota failure
with dependent inventory/job state; package uninstall migration; normalization
idempotence/name collision; decompression/aggregate budget rejection; asset
rights `unknown`; and constitutional quorum/tie/deadlock/rollback.
