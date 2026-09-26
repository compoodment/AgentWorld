---
title: Versioning and Releases
type: release-policy
status: active
updated: 2026-09-26
---

# Versioning and Releases

ClankerWorld is **unreleased**. A successful build or commit is not a public
release. Keep the game version separate from the compatibility versions used
by saves, replay, content packages and network contracts.

## Game version

Public releases use [Semantic Versioning](https://semver.org/) and annotated Git
tags beginning with `v`. The first experimental line is `v0.1.0-alpha.n`;
`v1.0.0` is a deliberate stable-product promise, not simply the first build
that starts. Compatible corrections to a released line use patch versions.
Individual commits do not need version bumps because their source revisions
already identify exact builds.

The current .NET version metadata comes from
[`Directory.Build.props`](../Directory.Build.props), not a second hand-maintained
constant. See the [build reference](building.md) for the selected toolchain.
A tagged release must report both its game version and source revision.

## Save and content compatibility

The public version does **not** decide whether a saved world loads. The
existing save/replay envelopes carry their own contract, simulation and schema
versions; generator, clock, content-lock and asset versions are separate where
relevant. Change each field only when its semantics change, and cover old-save
handling, migration and replay in tests. A cosmetic game-version bump is not a
save migration.

Content packages have their own versions and compatibility/dependency rules.
Do not infer package compatibility from an alpha game label. Build revision and
engine-release information may be recorded for diagnostics, but must not alter
canonical world state or replay digests. The [current-state report](current-state.md)
describes today's supported save behavior; historical schema-specific release
notes remain in Git history rather than a growing checklist here.

The September 2026 AgentWorld-to-ClankerWorld internal-identifier reset is an
explicit **pre-release exception**: the prior development save and Windows
owner registration are not migrated. Retain a rollback backup, start a fresh
world, and pair a new device. This does not set a precedent for discarding
players' saves after a public release.

When rolling back an incompatible host upgrade, restore a **matching older
application and pre-upgrade world save/history**. An older host is not expected
to read a newer schema. Back up the checkpoint and its adjacent history,
pairing authority and provider store together; missing or corrupt referenced
history must fail closed.

## Release gate

Before publishing a release:

1. Choose the version, update runtime/package metadata and move relevant
   `CHANGELOG.md` entries into a dated release section, leaving `Unreleased`.
2. Run the applicable build, test, Godot-export and Windows playtest gates.
   Check a real player path, not only isolated simulation fixtures.
3. If compatibility changed, verify migration, old-save handling, replay and
   rollback from a matching backup.
4. Commit and push the release changes; verify local `main` and GitHub
   `origin/main` match.
5. Create and push the annotated tag, then verify that GitHub resolves it to
   the intended commit. Publish a GitHub release when there is a distributable
   artifact or useful release note.

Update `CHANGELOG.md` under `Unreleased` in the same commit as player-visible
gameplay/UI, world-runtime, save-compatibility, deployment, packaging or
security changes. Documentation-only and test-only edits need no changelog
entry unless they alter an explicit supported promise.
