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
12. **Canonical manifest provenance:** package manifests have a stable UTF-8
    wire form with deterministic collection ordering, strict canonical-byte
    decoding, and a persisted `ManifestDigest`. Registry restore recomputes and
    verifies that digest, so tampered manifest metadata fails closed without
    creating a circular dependency between package identity and asset IDs.
13. **Deterministic cache accounting:** rebuildable asset residency has a
    value-only ledger keyed by normalized digest and decode profile. It applies
    decoded-cache/GPU ceilings, deterministic least-recently-used eviction,
    current-frame pinning, and atomic failure without opening files or touching
    a renderer.
14. **Portable asset provenance manifests:** normalized inert assets can be
    exported as strict canonical metadata containing identity, rights,
    provenance, preview references, PNG bounds, and byte reservations. Decode
    rejects non-canonical or tampered bytes and never carries asset payloads,
    paths, URLs, or executable content.
15. **Data-only package artifact envelope:** a package manifest and its complete
    asset provenance manifests can be handed off as one canonical nested JSON
    artifact. Artifact validation matches asset identities, digests, and byte
    reservations; export additionally rejects non-redistributable rights and
    disabled executable capabilities.

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
- `ContentPackageManifestCodecTests` covers order-independent canonical bytes,
  strict decoding, stable manifest digests, registry persistence, and tamper
  rejection.
- `WorldAssetCacheTests` covers deterministic eviction, current-frame pins,
  atomic over-budget rejection, expired-pin cleanup, and checkpoint-shaped
  cache-state round-trips.
- `AssetProvenanceManifestCodecTests` covers order-independent canonical bytes,
  normalized-asset round-trips, strict formatting/tamper rejection, and
  local-only unknown-rights manifests.
- `ContentPackageArtifactCodecTests` covers nested canonical round-trips,
  reservation/provenance matching, missing-manifest rejection, and export
  rights/capability gates.

## Still required for the alpha gate

- isolated process preview/test-world execution and actual renderer/decode
  binding on top of the value-only cache ledger;
- the remaining economy/world rules that consume declarative definitions.

The current slice is intentionally a contract/runtime foundation, not a claim
that arbitrary packages can already be installed from the network.
