---
title: Product Roadmap
type: roadmap
status: active
updated: 2026-09-22
---

# Roadmap

This is the canonical answer to **what AgentWorld should build next**. It is
ordered by playable outcomes, not by the number of schemas, contracts or test
fixtures completed.

For what works today, see [current state](../status/current-state.md). For
confirmed defects, see [known bugs](../bugs.md). GitHub issues may decompose an
active milestone, but they do not replace the outcome and acceptance gate here.

## Now — Living Settlement

Turn the existing simulation primitives into one coherent loop that gives the
four inhabitants useful reasons to move, plan, cooperate and disagree.

### Player outcome

The settlement starts with understandable resources and opportunities.
Inhabitants choose persistent projects, acquire and exchange materials, build
or cultivate something, use the result, and visibly react to success, scarcity
or obstruction.

### Workstreams

1. **Starter content pack**
   - versioned built-in materials, food, seeds, tools and fuel;
   - shelter, storage, fire/cooking and workshop building definitions;
   - crop, food and basic tool recipes;
   - safe activation/migration for both fresh and existing private worlds.
2. **Persistent projects and work**
   - goal → required inputs/tools/site → acquisition → construction or
     production → use;
   - deterministic execution, interruption and recovery;
   - planning-model selection only when a meaningful legal choice exists.
3. **Visible ownership and economy**
   - carried and stored items legible in the client;
   - requests, sharing, direct barter and household allocation;
   - scarcity and ownership affecting later choices and relationships.
4. **Social consequences**
   - gratitude, resentment, trust and cooperation from real events;
   - emergent work preferences and settlement roles;
   - inspectable reasons without exposing hidden model reasoning.
5. **Cognition cost and feedback**
   - per-inhabitant provider/model assignments with world-default inheritance;
   - suppress repeated paid `safe_idle` calls;
   - show compact provider decision/fallback/usage activity in-game;
   - retain secret-safe structured host telemetry.
6. **Alpha UI repair**
   - compact the cognition settings;
   - keep the menu stable when no inhabitant is selected;
   - improve item, project and blocker readability.

### Acceptance gate

In an ordinary private-world session, without operator-only content injection:

1. the four inhabitants can gather at least three materially different inputs;
2. ownership and carried/stored quantities are visible;
3. at least one inhabitant requests, shares or trades a needed item;
4. the planning role receives a meaningful choice between legal settlement
   projects;
5. the chosen project survives interruption/restart and completes through
   deterministic work;
6. inhabitants use the result and their needs, inventory or relationships
   visibly change;
7. idle model-call volume is bounded and provider activity is diagnosable in
   both the client and safe host logs.

This gate is deliberately concrete: the systems must reinforce each other in
the normal game, not merely pass separate fixture tests.

### Integration evidence and remaining depth

The ordinary seeded runtime now executes persistent acquisition/work projects,
gathers wood/stone/fiber, shares requested materials, produces and consumes food,
and exposes stores, project blockers and public gratitude in Godot. The
1,000-tick `SettlementProjectTests` scenario verifies that connected loop;
separate tests cover interrupted work and old-save migration. Provider/model
assignments, idle reuse and menu fixes are delivered foundations for this loop.
This is not evidence of richer negotiated barter, resentment/trust, emergent
roles or useful clothing/tool effects. Those workstreams remain open; the next
survival increment must make produced equipment and buildings matter.

## Next — Consequential survival

Once a settlement has useful work, make the world push back:

- weather and seasons affect exposure, crops, travel and storage;
- shelter, fire, clothing and rest quality matter;
- food variety, spoilage, illness and recovery create understandable pressure;
- ecology replenishment and overuse affect settlement plans;
- tuning keeps failure legible rather than turning the game into random death.

**Gate:** inhabitants anticipate and respond to one seasonal/weather crisis
using settlement resources, and the player can understand why they succeeded
or failed.

## Then — Social continuity

Deepen society only after daily life generates actual stakes:

- durable cooperation, conflict, teaching and role formation;
- households, caregiving, children, aging, death and inheritance in playable
  timescales;
- factions, laws, currency and culture emerging from demonstrated needs;
- institutions changing access, work or conflict rather than existing only as
  summaries.

**Gate:** a social decision changes who may use resources or perform work, and
its consequences remain visible across time, relationships and succession.

## Later — Creation and richer worlds

- player- and inhabitant-proposed data-only content through a usable approval
  workflow;
- preview/test-world execution, rollback and quarantine;
- production asset pipeline with provenance and cache accounting;
- larger/chunked worlds and distant-region simulation when the first settlement
  demonstrates the need;
- evaluate an executable-content sandbox only with a concrete capability that
  declarative content cannot express safely.

**Gate:** new content materially expands the live world while preserving save,
authority, rollback and host-isolation guarantees.

## Single-player release

Public packaging comes after the private world is coherent:

- signed client distribution, update/migration flow and recovery UX;
- additional desktop platforms as justified.

**Gate:** a stable private single-player game has a reproducible distribution,
update and recovery path. Multiplayer, shared-world roles and public discovery
are excluded by the owner's 2026-09-22 decision.

## Completed foundation

The earlier numbered phases remain valuable implementation history:

| Foundation | Evidence | Product interpretation |
| --- | --- | --- |
| Deterministic kernel | [Phase 1 ledger](../implementation/phase-1.md) | Core ordering, movement, survival, inventory and persistence semantics are verified |
| Owner observation and Godot boundary | [Phase 2 ledger](../implementation/phase-2.md) | Paired owner protocol, Windows export and private host path are verified |
| Bounded cognition | [Phase 3 ledger](../implementation/phase-3.md) | Provider-neutral legal choices, fallback and usage are verified |
| Society primitives | [Phase 4 ledger](../implementation/phase-4.md) | Relationships, exchange, lifecycle and multi-inhabitant fixtures are verified |
| Data-only content | [Phase 5 ledger](../implementation/phase-5.md) | Much of package/build/recipe governance is implemented, but the player loop is incomplete |

“Foundation complete” is not shorthand for “feature-rich game complete.” The
[current-state matrix](../status/current-state.md) is authoritative about that
difference.
