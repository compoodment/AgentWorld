---
title: Current Product State
type: product-status
status: active
updated: 2026-09-22
---

# Current product state

This is the canonical answer to **what AgentWorld actually does today**. It
describes the default private-world runtime and Godot client, not only schemas,
contracts or isolated fixtures.

## Status vocabulary

- **Playable** — connected to the default private world and observable or
  controllable through the normal Godot client.
- **Integrated but thin** — participates in the live save/runtime, but does not
  yet form a rich or recurring gameplay loop.
- **Verified primitive** — implemented and tested in a bounded fixture or API,
  but not meaningfully connected to normal play.
- **Planned** — accepted direction without the required implementation.

“Implemented” never means “the type exists.” The player must be able to
encounter the behavior through the normal game path before this document calls
it playable.

## Playable path

The supported product path is an unsigned Windows 11 x64 Godot client paired to
one private headless .NET host. The paired client observes the complete world
and submits signed requests. The server validates and commits all state.

The host remains reachable continuously, but simulation ticks and hosted-model
calls run only while at least one authenticated client has a current presence
lease. The last disconnect closes the gate after five seconds. Reconnect does
not simulate offline time or clear an explicit manual pause.

The static web assets retained by the HTTP host are legacy protocol-diagnostic
infrastructure. They are not a supported game client or an alternative owner
interface.

## Capability matrix

| Area | Status | What exists now | Important limitation |
| --- | --- | --- | --- |
| World host and persistence | **Playable** | Persistent private save, event stream, restart recovery, manual pause and client-presence gate | No player-facing save selection or migration UI |
| Owner security | **Playable** | Windows device key, host-approved pairing, signed/replay-resistant owner requests, revocation and origin pinning | Private single-owner model only |
| Map and movement | **Playable** | Seeded tile map, deterministic routing, occupancy, contention, adjacent resource interaction and animated client movement | Small fixed first-world presentation; no broad exploration loop |
| Survival | **Playable** | Hunger, energy, gathering, carried food, eating, sleep, resource depletion and regeneration | Shelter, exposure, varied nutrition and illness are not connected loops |
| Observation and control | **Playable** | World view, inhabitants, needs, intentions, inventories, relationships, events, pause/resume and suggestive/must-do instructions | UI remains early-alpha and some diagnostics are operator-only |
| Cognition | **Playable** | World defaults and per-inhabitant provider/model overrides; Jev for routine and OpenAI/Ollama Cloud for planning; validation, fallback, retry, safe logs and selection-card telemetry | Legal planning remains bounded to building/recipe choices, not free-form social reasoning |
| Inventory and ownership | **Integrated but thin** | Authoritative lots, reservations, household/personal inventory, transfer and production completion | Very few useful item kinds are present in normal play; ownership is not yet a visible economy |
| Buildings and production | **Integrated but thin** | Automatically staged starter shelter/storage/fire/workshop and crop/meal/tool recipes; placement, jobs, reservations, completion and household food pickup | Material acquisition and persistent multi-step projects are still incomplete |
| Trade and economy | **Verified primitive** | Atomic direct transfer, barter settlement, ownership and currency state are implemented and tested | Inhabitants do not autonomously request, negotiate or repeat trade in the live world |
| Relationships and households | **Integrated but thin** | Persistent relationship/household state, social interactions and owner projection | Few world events change relationships; cooperation and conflict are not yet lived loops |
| Family, aging and death | **Integrated but thin** | Lifecycle, caregiving, birth, aging, death, estates and inheritance exist in society runtime/tests | Timescale and default play do not yet make this a practical player experience |
| Ecology and weather | **Integrated but thin** | Renewable resources, seasons, deterministic weather and world summaries | Weather has little survival/economic consequence |
| Factions, law, currency and culture | **Integrated but thin** | Bounded persistent state and deterministic contracts | Mostly summaries/state containers; inhabitants do not create or contest institutions in normal play |
| Content governance | **Integrated but thin** | Canonical data-only packages and bundled starter content; validation, approval, staging, activation, rollback and quarantine | No friendly player proposal/approval workflow |
| Asset governance | **Verified primitive** | Provenance, rights metadata, quotas, cache/reservation accounting, preview contracts and artifact envelopes | No end-to-end creator/approval experience and no production art pipeline |
| Client presentation | **Playable** | World-first view, selection-card provider activity, compact per-inhabitant settings, centered menu and Windows export | Prototype visuals; project/blocker presentation remains thin |
| Multiplayer/public worlds | **Excluded** | Single-player only by owner decision | Multiple paired owner devices are not multiplayer |
| Executable generated mods | **Planned/disabled** | Data-only packages are fail-closed | No sandbox has been selected; arbitrary generated code does not run on the host |

## Current cognition behavior

The server generates a bounded observation and legal candidate list. Provider
output can choose one candidate; it cannot invent a world mutation.

| Role | Options | Typical current work |
| --- | --- | --- |
| Routine survival | Deterministic, Jev | Eat, sleep, gather, move or idle |
| Planning and work | Deterministic, OpenAI, Ollama Cloud | Choose a legal building or recipe project |

The host stages the built-in starter package through the validated content
registry on the first client-present, unpaused tick. It activates at the tick
boundary and supplies legal building/recipe choices. This also works for old
saves; already registered, rolled-back or quarantined starter packages are not
silently reinstalled. A manually paused save is not migrated merely by starting
the service.

Settings select either **World defaults** or a named inhabitant. Each role may
inherit its world default or override its provider/model. API keys remain in
the host credential store; removing a shared provider key also removes its
personal overrides. Personal assignments are signed, target-bound and durable.

Unchanged idle intentions are reused for up to 300 ticks, including across
reloads. Changed legal choices or urgent need bands trigger reconsideration;
an observation with only `safe_idle` does not need a provider call. The selected
inhabitant card shows the last accepted decision, with available usage/model/
latency details in a tooltip. Missing model/latency is shown as unavailable;
adapter token counts can be zero when the provider omits usage.

## Evidence boundary

The phase ledgers under `docs/implementation/` remain useful proof that
specific invariants and fixtures passed. They are not the current product
status. The repository's executable evidence is the merged code, automated
tests, Godot startup/export checks and exact-commit CI.

Future changes must update this document when a capability moves between
planned, verified primitive, integrated or playable status.
