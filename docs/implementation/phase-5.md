---
title: Phase 5 Implementation Ledger
type: implementation-ledger
status: in-progress
updated: 2026-09-22
---

# Phase 5 Implementation Ledger

Phase 5 makes data-only content extensible without granting packages direct
authority over the kernel. Executable, network, filesystem, and host-integration
capabilities remain disabled until a separate sandbox contract exists.

## Implemented slices

1. **Immutable content identity:** stable `major.minor.patch` versions,
   canonical package IDs, lowercase SHA-256 package/payload digests, and
   `<package-digest>/<kind>/<local-id>@<version>` definition IDs.
2. **Dependency resolution:** deterministic highest-compatible version choice,
   digest tie-breaking, dependency-first locks, optional dependency omission,
   and explicit missing, conflict, digest-mismatch, compatibility, and cycle
   diagnostics.
3. **Data-only lifecycle:** package proposal, validation against a resolved
   lock, owner approval, staging, and activation at a world tick. Every
   transition emits a durable governance event; forbidden capabilities are
   rejected at proposal time.
4. **Private-world persistence:** the content registry is now part of the
   private-world checkpoint. Staged packages activate on the next unpaused
   world tick, active/staged packages can be quarantined through an explicit
   rollback transition, and activation/rollback events survive save/reload.
5. **Inert asset governance:** PNG/APNG candidates have deterministic identity,
   provenance, rights/redistribution status, preview metadata, dimensions,
   animation timing, and per-asset budget diagnostics. Asset normalization is
   intentionally inert: it does not execute, fetch, or rewrite arbitrary
   payloads.
6. **Aggregate asset governance:** package-level asset counts, candidate/
   decoded/durable-byte reservations, frame and dimension ceilings, duplicate
   identity checks, deterministic diagnostics, export-rights policy, and
   metadata-only preview contracts are validated as one atomic collection.
7. **Private owner content path:** signed propose/validate/approve/stage/
   rollback routes now target the integrated private runtime. Typed `building/v1`
   and `recipe/v1` payloads enter the live declarative world only on next-tick
   activation, persist through checkpoints, appear in owner observations, and
   are removed from the live projection when quarantined.
8. **Mutation-free preview:** `ContentPackagePreview` resolves the same
   dependency lock and materializes typed definitions against an immutable base
   projection, returning the base unchanged on malformed or conflicting data.
9. **Material world interactions:** active building definitions can be placed
   on passable map tiles after deterministic footprint and build-cost checks.
   Placement is persisted, owner-routed, and removed from the live projection
   when its package is quarantined.
10. **Recipe production:** recipes can reserve household inputs at a placed
    workstation, complete on a deterministic world tick, consume the inputs,
    and create output lots. Running/completed jobs, reservation IDs, and
    production events survive checkpoint round-trips.
11. **World-wide asset reservations:** activation preflight atomically reserves
    package asset charges against durable-storage, decoded-cache, GPU, and
    render-work ceilings. Shared normalized digests are charged once for byte
    budgets while package references still count render work; rollback releases
    the package reservation.

## Evidence

- `ContentPackageRules` validates canonical IDs, versions, ranges, and digests.
- `ContentPackageResolver` produces an immutable dependency-first lock.
- `ContentPackageRegistry` records the data-only lifecycle and event history.
- `PrivateWorldRuntime` persists the registry and typed declarative content,
  applies next-tick activation atomically, and exposes rollback through the
  same save boundary as society state.
- `AssetNormalizer` validates PNG/APNG structure and metadata without inflating
  or executing payloads; `AssetGovernanceTests` covers provenance, rights,
  previews, timing, budgets, and deterministic output.
- `WorldSystemsState` now persists the bounded first-world contracts for
  ecology, weather, factions/law, currency, culture, and chunk manifests.
- `ContentGovernanceTests` covers identity, highest-compatible resolution,
  missing optional dependencies, dependency cycles, lifecycle ordering, and
  disabled executable capabilities.
- `AssetPackageGovernanceTests` covers aggregate budgets, duplicate IDs and
  digests, rights policy, metadata-only previews, and order-independent
  diagnostics.
- `WorldAssetReservationTests` covers shared-digest accounting, atomic budget
  rejection, canonical checkpoint round-trips, and package release.
- `PrivateWorldRuntimeTests` and `ViewerHttpTests` cover typed building/recipe
  activation, deterministic placement and production, checkpoint round-trip,
  signed lifecycle/action routing, and rollback.
- `ContentDefinitionTests` covers mutation-free preview success and failure
  isolation.

## Still required for the alpha gate

- package serialization/provenance and exact manifest-byte digests;
- isolated process preview/test-world execution and renderer cache integration
  on top of the persisted world reservation ledger;
- the remaining economy/world rules that consume declarative definitions.

The current slice is intentionally a contract/runtime foundation, not a claim
that arbitrary packages can already be installed from the network.
