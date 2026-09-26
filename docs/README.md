---
title: ClankerWorld Documentation
type: documentation-index
status: active
updated: 2026-09-26
---

# Documentation

**ClankerWorld** is the product, repository and codebase name. Some old
internal save, content and pairing identifiers deliberately retain the former
name so existing worlds and paired devices continue to work.

| Question | Read |
| --- | --- |
| What does computment want the finished game to be? | [Vision interview and decision ledger](vision-interview.md) |
| What actually works in the Godot/private-VPS prototype today? | [Current state](current-state.md) |
| What is the next playable outcome? | [Roadmap](roadmap.md) |
| How are the current client, simulation, saves and model calls separated? | [Architecture](architecture.md) |
| Which confirmed problems remain? | [Known bugs](bugs.md) |

Development references: [build and test](building.md), [current private-host
pairing](pairing.md), and [release/version policy](releasing.md).

## Authority

- The **vision ledger** is the single source for owner intent. Its Decided,
  Preferred, Proposed and Open labels matter. It is not evidence that a feature
  exists. Earlier interview turns are retained in the
  [historical record](archive/vision-interview-history.md); the current ledger
  wins if they differ.
- **Current state** reports the normal playable Godot path. Code, tests and
  observed play are executable evidence; a contract or passing fixture alone
  does not make a feature playable.
- **Architecture** describes the present technical boundary and the protected
  invariants that current code must respect. It does not override the finished
  vision. **Roadmap** chooses sequence; **known bugs** tracks confirmed gaps.
- Keep these answers separate. Update the affected current document when
  behavior or policy changes; link to it instead of maintaining another copy.

Superseded phase ledgers, old concepts, decision registers and exploratory
contracts were removed from the active tree. Git history retains them; their
past claims do not supersede the current vision or implementation evidence.
