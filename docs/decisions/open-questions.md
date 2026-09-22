---
title: Open Questions
type: decision-backlog
status: active
updated: 2026-09-22
---

# Open questions

Only unresolved product or architecture decisions belong here. Confirmed bugs
belong in [known bugs](../bugs.md); scheduled implementation belongs in the
[roadmap](../planning/roadmap.md); current capability claims belong in
[current state](../status/current-state.md).

## Executable generated content

**Question:** Is there a demonstrated capability that cannot be expressed by
the governed data-only content system and justifies selecting an executable-mod
sandbox?

**Evidence required before deciding:**

- a concrete first-world behavior blocked by declarative composition;
- measured CPU, memory, I/O and persistence requirements;
- a capability and host-isolation model;
- deterministic/replay implications;
- failure, quarantine, migration and rollback behavior.

Until that evidence exists, arbitrary generated code remains disabled. This
does not block starter content, buildings, recipes, assets or other data-only
world expansion.

No other product-policy question currently blocks the **Living Settlement**
milestone.
