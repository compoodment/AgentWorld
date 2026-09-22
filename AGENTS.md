# AgentWorld — Delivery Rules

## GitHub sync

For user-authorized repository changes, completion means the verified commit is
on GitHub's `origin/main`, not merely committed locally. Before reporting work
done: commit the intended changes, push them, and verify `main` matches
`origin/main`. Report the remote commit hash or link.

## Release versioning

Follow [the release-version policy](docs/planning/versioning-and-releases.md)
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
