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
