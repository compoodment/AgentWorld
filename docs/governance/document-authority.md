---
title: Document Authority and Delivery Evidence
type: governance
status: active
updated: 2026-09-19
---

# Document Authority and Delivery Evidence

AgentWorld separates product intent, accepted policy, executable semantics,
live work, and proof of what actually runs. A document may link to another
authority, but should not restate its status in several places.

## Sources of truth

| Question | Authority | What it owns |
| --- | --- | --- |
| Why should the world exist and feel this way? | `docs/concept/` | frozen intent, pillars, and long-horizon direction |
| Which product policy won? | [Decision register](../decisions/decision-register.md) | accepted product decisions |
| Why was it chosen? | [Design log](../decisions/design-log.md) | chronological rationale and history |
| What exact behavior must implementation preserve? | [Planning contracts](../planning/) | testable semantics, invariants, and acceptance fixtures |
| What remains genuinely undecided? | [Open questions](../decisions/open-questions.md) | bounded decisions and required prototype evidence |
| Which phase and gate are current? | [Roadmap](../planning/roadmap.md) | phase sequence and gate definitions |
| What capability is specified, implemented, or verified? | [Implementation ledger](../implementation/phase-1.md) | scoped capability evidence, not daily task state |
| What work is active right now? | GitHub milestone and issues | live workflow, ownership, dependencies, and discussion |
| What truly exists and works? | merged code, tests, CI, and reproducible commands | current executable reality |

If executable evidence conflicts with an accepted contract, the code is a
defect or the contract needs an explicit decision update. A successful demo is
not enough to silently amend either one.

## Status vocabulary

Use these front-matter statuses consistently:

- **frozen** — stable concept material; change only with an explicit decision
  or prototype evidence.
- **active** — maintained current authority or operational plan.
- **proposal** — not accepted and not authoritative.
- **superseded** — retained for context; link to its replacement.
- **history** — chronology and rationale; it records decisions but does not
  override their current authority.

Implementation capability status has a separate, evidence-based meaning:

- **specified** — accepted contract exists; no merged evidence yet.
- **spiking** — a bounded prototype is collecting evidence.
- **implemented** — merged code exists, but the required gate has not passed.
- **verified** — documented tests or CI prove the contract behavior.
- **deferred** — explicitly outside the current milestone or release.

GitHub owns the changing workflow state. Update the implementation ledger only
when a capability's evidence status changes; do not mirror assignees, comments,
or daily progress into Markdown.

## Change route

1. Put active work in a GitHub issue and attach it to the appropriate
   milestone.
2. Link the issue or pull request to the contract and decision it implements.
3. Put trade-offs or prototype findings in the issue; promote a product-policy
   change only through the decision register and design log.
4. Merge code with reproducible verification evidence.
5. Update the implementation ledger when that evidence moves a capability to
   `implemented`, `verified`, or `deferred`.

This is deliberately a delivery system, not miniature Jira in a trench coat.
