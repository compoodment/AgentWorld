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

## Evidence

- `ContentPackageRules` validates canonical IDs, versions, ranges, and digests.
- `ContentPackageResolver` produces an immutable dependency-first lock.
- `ContentPackageRegistry` records the data-only lifecycle and event history.
- `PrivateWorldRuntime` persists the registry, applies next-tick activation,
  and exposes rollback through the same save boundary as society state.
- `AssetNormalizer` validates PNG/APNG structure and metadata without inflating
  or executing payloads; `AssetGovernanceTests` covers provenance, rights,
  previews, timing, budgets, and deterministic output.
- `WorldSystemsState` now persists the bounded first-world contracts for
  ecology, weather, factions/law, currency, culture, and chunk manifests.
- `ContentGovernanceTests` covers identity, highest-compatible resolution,
  missing optional dependencies, dependency cycles, lifecycle ordering, and
  disabled executable capabilities.

## Still required for the alpha gate

- package serialization/provenance and exact manifest-byte digests;
- aggregate package/world asset budgets and isolated preview/test-world
  execution;
- owner-facing content proposal/approval/activation routes in the new private
  runtime;
- definitions that materially add inhabitant-created buildings, recipes, or
  other first data-only content to the live world.

The current slice is intentionally a contract/runtime foundation, not a claim
that arbitrary packages can already be installed from the network.
