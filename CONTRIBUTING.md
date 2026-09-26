# Contributing to ClankerWorld

ClankerWorld is an early private alpha with a substantial deterministic and
security foundation but incomplete connected gameplay. Start with the
[current-state matrix](docs/current-state.md), then read the
[roadmap](docs/roadmap.md), [vision ledger](docs/vision-interview.md), and
[known bugs](docs/bugs.md). Do not infer player-visible completeness from a
data type or isolated fixture.

## Useful contributions now

- connect existing primitives into the **Living Settlement** loop;
- fix a confirmed problem in the known-bug register;
- add reproducible evidence for a gameplay or architecture claim;
- simplify the UI or operator path without weakening server authority;
- improve documentation by removing duplication and stale claims;
- challenge an accepted implementation choice with concrete evidence.

## Change rules

1. Read the relevant current-state, roadmap, vision and architecture documents.
2. Separate owner intent, current implementation and historical evidence.
3. Keep the authoritative simulation independent of Godot and model providers.
4. Treat model output as untrusted input; legal actions and state transitions
   remain server-owned.
5. Never add API keys, private saves, pairing material or raw provider payloads
   to the repository, logs or bug reports.
6. Update the canonical documentation in the same change when behavior, UI,
   operations, compatibility, known bugs or scope changes.
7. Add reproducible tests and run the repository verification gate.

GitHub issues and pull requests own active execution details. Durable product
intent and current implementation truth stay in their separate canonical docs;
Git history retains retired implementation evidence.
