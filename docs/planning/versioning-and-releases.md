---
title: AgentWorld Versioning and Releases
type: release-policy
status: active
updated: 2026-09-22
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

### Private-world save schema 12

Schema 12 adds optional bounded directed trust records to physical inhabitants.
Scores are earned only from committed material help, completed exchange or
finished teaching; refusal and disagreement do not create a negative record.
Schemas 1–11 remain readable. Existing cooperation memories project equivalent
trust for observation and eligibility without rewriting an older paused save;
the next new trust event materializes that pair's score under schema 12. Records
must reference another known inhabitant, remain within 1–10 and cannot
claim a future change tick. Rollback requires the matching pre-upgrade
application, save and adjacent history.

### Private-world save schema 11

Schema 11 adds optional bounded building, farming and crafting practice to each
inhabitant. Older inhabitants retain no practice until they successfully finish
new work; migration does not infer experience from roles, age or project
history. Schemas 1–10 remain readable, and schema-3-and-later paused checkpoints
retain their representation until a resumed mutation earns practice or otherwise
requires the current schema. Values outside 0–30 and practice stored under an
older schema fail closed. Rollback requires the matching pre-upgrade application,
save and adjacent history.

### Private-world save schema 10

Schema 10 adds a nullable society biological-clock anchor/rate and nullable
newborn biological birth ticks. They are omitted from unmodified older saves.
The default remains calendar aging; only a signed owner setting while paused
creates an anchor and upgrades the checkpoint. Society lifecycle contract 2
uses that clock for age boundaries and mortality, while world dates, seasons,
inventory expiry and simulation cadence keep using ordinary world ticks.
Rate changes preserve accumulated age, and newborns always start at age zero.
Schemas 1–9 remain readable; schema-3-and-later paused checkpoints retain their
representation until an explicit mutation. Rollback requires the matching
pre-upgrade application, save and adjacent history.

### Private-world save schema 9

Schema 9 adds optional per-inhabitant parenthood proposals, preparation and child
references. Schemas 1–8 remain readable; schema-3-and-later paused checkpoints
are preserved until a resumed mutation needs the current schema. Starting a
parenthood plan upgrades the checkpoint. Birth commits identity, relationships,
food consumption and physical state in the same proposed tick. Malformed stages,
participant IDs, duplicate active plans and future ticks fail closed. Preserve
the pre-upgrade application, save and history for rollback.

### Private-world save schema 8

Schema 8 adds optional per-inhabitant apprenticeship state: mentor, requested
role, stage, progress and tick bounds. Schemas 1–7 remain readable; paused saves
from schema 3 onward retain their checkpoint representation on restore. Schemas
1–2 normalize to the baseline schema-3 composition. Beginning training upgrades
the save; invalid mentors, stages,
progress or duplicate active mentor assignments fail closed. This migration
introduced schema 8. Preserve the pre-upgrade save/history and application for rollback.

### Private-world save schema 7

Schema 7 adds an optional household council, food-access policy, steward and
bounded ballot with separate votes. Schemas 1–6 remain readable; schema-3-and-later
paused checkpoints are preserved on restore. Council initialization occurs only on a resumed settlement
tick. This migration introduced schema 7. Unknown policies/inhabitants, duplicate or
contradictory votes, invalid electorates and deadlines fail closed. Rollback
requires the matching pre-upgrade checkpoint, history and application.

### Private-world save schema 6

Schema 6 adds optional per-inhabitant warmth/illness/diet and settlement fire-fuel
deadlines. Schemas 1–5 remain readable; schema-3-and-later paused checkpoints
are preserved on restore. Survival
initializes on a resumed tick after settlement content activation, advancing
legacy food processing timestamps without retroactive spoilage. This migration
introduced schema 6. Invalid condition ranges, duplicate/unknown fires and invalid
fuel deadlines fail closed. Preserve the matching application, checkpoint and
history when rolling back; older hosts cannot load schema-6 saves.

### Private-world save schema 5

Schema 5 adds optional persistent settlement projects and validated additive
resource nodes from the built-in settlement package. Schemas 1–4 remain
readable. Loading or observing an older paused schema-3-or-later save preserves its bytes;
creating a project, adding settlement resources or compacting history upgrades
the checkpoint to the current schema. Null project fields remain omitted.
Restore checks the seeded map plus only the registered resource additions and
rejects invalid project phases/work counters. Rollback requires the matching
pre-upgrade save and application, not loading a schema-5 save in an older host.

### Private-world save schema 4

Schema 4 adds event-history floors and a content-addressed archive head.
Schemas 1–3 remain readable; a schema-3 save is not rewritten to schema 4 merely
by loading it. Its first history compaction upgrades it to the current schema.
Default/zero history fields are omitted to preserve legacy checkpoint bytes.
Downgrading a compacted save to an older binary is unsupported: restore the
pre-upgrade application and matching save backup instead.

Back up `<save>.history/` with the checkpoint, pairing authority and provider
store. Verify the hash chain on restore; a missing/corrupt referenced segment
fails closed. See [persistence](architecture.md#persistence-and-replay).

### Public release gate

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

## Changelog maintenance

The changelog is maintained continuously, not reconstructed only when a release
is being prepared. Every commit that changes player-visible gameplay or UI,
world-runtime behavior, save compatibility, deployment or packaging behavior,
or a security boundary must update the `Unreleased` section in the same commit.

Entries describe what changed for a player or operator. Internal refactors,
test-only changes, and documentation-only edits do not need entries unless they
change a supported behavior or an explicit compatibility or operational
promise. When a release is cut, move the applicable entries beneath the dated
release heading and leave a fresh `Unreleased` section in place.
