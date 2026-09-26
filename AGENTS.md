# AgentWorld — Delivery Rules

## GitHub sync

For user-authorized repository changes, completion means the verified commit is
on GitHub's `origin/main`, not merely committed locally. Before reporting work
done: commit the intended changes, push them, and verify `main` matches
`origin/main`. Report the remote commit hash or link.

## Release versioning

Follow [the release-version policy](docs/releasing.md)
for every AgentWorld release. Keep the public game release version separate
from saved-world compatibility versions. Do not create a release tag merely
because a commit exists.

Before a release, update the authoritative runtime/package version and
`CHANGELOG.md`, run the applicable acceptance gate, check migration and replay
coverage for compatibility changes, push the release commit, then push and
verify the annotated tag on GitHub.

## Changelog discipline

Update `CHANGELOG.md` in the same commit as every user-visible gameplay, UI,
world-runtime, save-compatibility, deployment, packaging, or security change.
Keep new entries under `Unreleased` until a release is cut, and describe the
effect in player or operator language rather than commit or implementation
jargon.

Do not add changelog noise for refactors, test-only changes, or documentation
edits unless they change supported behavior or an explicit compatibility or
operational promise. Before pushing, compare the intended diff with the
`Unreleased` section and confirm the relevant capability is represented.

## Documentation discipline

Follow the [documentation map and authority rules](docs/README.md).
There is one canonical source per question:

- [vision ledger](docs/vision-interview.md) owns computment's intended finished
  game, with decided/preferred/open labels;
- [current state](docs/current-state.md) owns what is playable,
  integrated, fixture-only, or planned;
- [roadmap](docs/roadmap.md) owns future sequence and acceptance gates;
- [known bugs](docs/bugs.md) owns confirmed defects and product gaps;
- [architecture](docs/architecture.md) owns current technical boundaries.

Every user-visible gameplay, UI, world-runtime, compatibility, packaging,
security, or operational change must include a documentation-impact review.
Update the affected canonical document in the same commit; do not copy a
volatile current-status summary into other overview files. A schema, fixture,
or passing unit test is not a
player-visible feature. Call a capability playable only after it is connected
to the default private world and normal Godot path.

Before publishing documentation changes, run the normal test suite so required
metadata and local Markdown links are checked. Git history retains retired
phase and exploratory documents; they are not active product authority.

## Operational observability

Treat live diagnosis as part of every server-owned gameplay loop, provider
adapter, persistence boundary, and lifecycle gate. New or changed runtime work
must leave low-noise, structured logs at meaningful outcome boundaries so an
operator can tell what the world attempted, what was accepted, what fell back,
and why. Prefer stable event names and named fields such as world tick, entity
or request ID, provider role/model, legal intention, outcome, latency, and
bounded usage. Log state transitions and decisions, not render frames or idle
polls.

Never log API keys, authorization headers, signatures, credential-bearing
URLs, prompts, raw provider request/response bodies, hidden reasoning, or
unbounded player/model text. Operational logs are derived telemetry, never
simulation authority or required save state. Tests for observability changes
must prove both the useful signal and the absence of representative secrets.
