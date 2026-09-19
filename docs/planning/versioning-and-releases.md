---
title: AgentWorld Versioning and Releases
type: release-policy
status: active
updated: 2026-09-19
---

# Versioning and Releases

This policy distinguishes a player-facing AgentWorld release from the versions
that determine whether a saved world, replay, content package, or future client
can interoperate safely. The [decision register](../decisions/decision-register.md)
records the accepted policy; this document is the operational reference.

## Release state and public version

AgentWorld is currently **unreleased**. Do not tag an empty repository or an
unverified scaffold merely to have a version number.

Public game releases use [Semantic Versioning](https://semver.org/) with
annotated Git tags prefixed by `v`:

- First runnable, replay-verified vertical slice: `v0.1.0-alpha.1`
- Further candidate builds on that line: `v0.1.0-alpha.2`,
  `v0.1.0-alpha.3`, and so on
- A new prototype milestone or incompatible supported experimental contract:
  `v0.2.0-alpha.1`
- Later testing milestones: `v0.x.y-beta.n`
- `v1.0.0`: a deliberate stable public-product promise, not merely the first
  build that starts

Use a patch release for a compatible correction to a released line. Do not bump
the public version for every commit; the commit SHA already identifies the
exact build. An increment from `alpha.1` to `alpha.2` is an iteration within an
experimental milestone, not a promise that only a patch-sized change occurred.

## Runtime and package versions

Phase 1 uses the [C#/.NET 10 toolchain](csharp-toolchain.md). The shared
[`Directory.Build.props`](../../Directory.Build.props) file is the canonical
source for .NET package/runtime metadata: it defines `VersionPrefix` and
`VersionSuffix`, which currently evaluate to `0.1.0-alpha.1`. This metadata
does not create a public release or tag by itself.

When the first executable host exists, `agentworld --version` must print that
metadata and the source revision. Do not create competing hand-maintained
version constants.

## Compatibility is separate from release numbering

The public release version is diagnostic; it does not decide whether a world
can load. Saved worlds retain the compatibility versions defined by the
[deterministic kernel contract](deterministic-kernel-contract.md), including:

- `contract_version`
- `simulation_version`
- `schema_version`
- relevant generator, clock, content-lock, and asset versions

Bump a compatibility field only when its own semantics change. Supply the
corresponding migration and deterministic replay coverage. A public release can
leave every compatibility version unchanged, and a compatibility migration is
not justified by a cosmetic release bump.

Mod/content packages retain their own SemVer compatibility ranges under the
[content-governance contract](content-governance-contract.md). A future network
protocol version is likewise separate from game, save, and simulation versions.
Do not infer mod compatibility from an AgentWorld alpha label.

For diagnostics, saved worlds may record non-authoritative
`engine_release_version` and `source_revision`, but loaders must not use those
fields as a substitute for compatibility checks. Store them in diagnostic
envelope metadata outside canonical world-state and replay digests, so a
different build revision cannot falsely appear as a simulation change.

## Release checklist

Before creating a public release:

1. Select the next SemVer label according to the rules above.
2. Update the runtime/package metadata and `CHANGELOG.md`; preserve an
   `Unreleased` section between releases.
3. Run the release's acceptance gate and the relevant test suite. The first
   alpha requires the deterministic vertical-slice proof:
   `seed -> move -> harvest -> eat/sleep -> save -> reload/replay -> identical digest`.
4. If compatibility behavior changed, verify the migration, old-save handling,
   and replay fixtures before releasing.
5. Commit and push the release changes; verify `main` equals `origin/main`.
6. Create an annotated `vX.Y.Z[-pre]` tag, push it, and verify that the remote
   tag resolves to the intended commit. Publish a GitHub release when there is
   a distributable artifact or release note worth presenting publicly.

`CHANGELOG.md` begins with the first executable scaffold, not as a substitute
for Git history while the project remains documentation-only.
