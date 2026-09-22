---
title: Document Authority and Maintenance
type: governance
status: active
updated: 2026-09-22
---

# Document authority and maintenance

AgentWorld separates current product truth, future work, accepted policy,
executable semantics and historical evidence. There is one canonical source
for each question; other documents link instead of copying volatile summaries.

## Sources of truth

| Question | Authority | What it owns |
| --- | --- | --- |
| What is playable or merely scaffolded now? | [Current state](../status/current-state.md) | Whole-product capability status and limitations |
| What is next? | [Roadmap](../planning/roadmap.md) | Ordered outcomes and acceptance gates |
| What confirmed problems remain? | [Known bugs](../bugs.md) | Reproduced defects/gaps and acceptance conditions |
| Why should the world exist and feel this way? | `docs/concept/` | Stable intent and long-horizon design pillars |
| Which product policy won? | [Decision register](../decisions/decision-register.md) | Current accepted decisions |
| Why was it chosen? | [Design log](../decisions/design-log.md) | Chronological rationale and historical context |
| What exact behavior must code preserve? | `docs/planning/*contract.md` | Invariants, semantics and acceptance fixtures |
| What remains undecided? | [Open questions](../decisions/open-questions.md) | Decisions that genuinely need evidence or owner choice |
| What did a bounded implementation prove? | `docs/implementation/` | Historical capability evidence and reproducible proof |
| What work is actively being executed? | GitHub issues/PRs | Task decomposition, ownership, discussion and CI |
| What truly runs? | Merged code, tests and exact-commit CI | Executable reality |

If code, docs and observed play conflict, do not choose the most flattering
version. Correct the defect or update the appropriate authority explicitly.

## Status vocabulary

Document front matter uses:

- **active** — maintained current authority;
- **frozen** — stable intent, changed only by an explicit decision or evidence;
- **proposal** — not accepted and not authoritative;
- **superseded** — retained for context and linked to its replacement;
- **history** — chronological evidence that does not override current truth;
- **complete** — a bounded ledger/gate closed; not a claim that the whole
  product area is rich or playable.

Capability status is defined by [current state](../status/current-state.md):
playable, integrated but thin, verified primitive and planned.

## Required change route

1. Identify which canonical source the change affects.
2. Update behavior and its tests.
3. Update current state for any capability-status or limitation change.
4. Update the roadmap if priority or an acceptance gate changed.
5. Add/remove a known-bug entry when a defect is reproduced or verified fixed.
6. Update decisions/contracts only when policy or required semantics changed.
7. Preserve old implementation evidence unless its claim was factually wrong;
   link to a superseding authority instead.
8. Run the normal test suite, including documentation metadata and local-link
   checks, before publishing.

Dates, version numbers, test counts and deployment state go only where they are
necessary evidence. Avoid copying volatile values into multiple overview docs.

## Staleness review

Every user-visible gameplay, UI, runtime, compatibility or operational change
must include a documentation-impact review. A reviewer should be able to answer:

- Did the player-visible capability matrix change?
- Did a known limitation or bug appear/disappear?
- Did the next milestone or its acceptance gate change?
- Did an accepted decision or invariant change?
- Does the README still describe the normal path without promoting legacy
  diagnostics?

This is a source-of-truth system, not miniature Jira in a trench coat.
