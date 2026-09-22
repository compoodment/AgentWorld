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

## Evidence

- `ContentPackageRules` validates canonical IDs, versions, ranges, and digests.
- `ContentPackageResolver` produces an immutable dependency-first lock.
- `ContentPackageRegistry` records the data-only lifecycle and event history.
- `ContentGovernanceTests` covers identity, highest-compatible resolution,
  missing optional dependencies, dependency cycles, lifecycle ordering, and
  disabled executable capabilities.

## Still required for the alpha gate

- package serialization/provenance and exact manifest-byte digests;
- inert PNG metadata normalization, rights, preview, and budget validation;
- isolated test-world preview and activation rollback;
- integration of active content into the private-world checkpoint;
- inhabitant-created buildings/recipes or other first data-only content.

The current slice is intentionally a contract/runtime foundation, not a claim
that arbitrary packages can already be installed from the network.
