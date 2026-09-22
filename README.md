# AgentWorld

**AgentWorld** is a design-first open-source project about a persistent virtual
world inhabited by LLM-driven agents.

The inhabitants are not quest-givers waiting for a player. They live under
hardcoded survival laws, form relationships and families, gather resources,
own and exchange things, build settlements, and may eventually create new
systems for the world itself.
The long-term idea is a world that can become more complex because its
inhabitants choose to make it so.

> **Status: private single-world alpha integration is in progress.**
> The protected kernel, society/cognition runtime, paired-device authority,
> hosted model boundary, and a persistent four-inhabitant private world are in
> the repository. The normal viewer host now projects that private world; the
> older one-actor fixture remains available only as an explicit compatibility
> mode. The Godot UI and Windows export path are still prototype infrastructure,
> not the final game UI or a released desktop build.

This repository deliberately captured the concept before implementation. The
first-world rules are now settled enough to build and test, and the C# solution
foundation establishes that boundary without claiming a finished kernel. New
design changes should come from prototype evidence or a deliberate decision,
not an endless questionnaire.

## The core idea

```text
world simulation
        ↕
authoritative world state
        ↕
LLM inhabitant runtime
        ↕
validated creation and modding system
```

A first world should begin with a small group of multiple, unrelated founders.
They may name themselves, have distinct personalities and aspirations, and
develop social roles through what they actually do. They must satisfy needs
such as hunger, health, rest, and shelter. Population grows through limited
family formation, not arbitrary agent spawning.

The world itself continues in a long-running process. It does not depend on a
cron job or a human issuing every action. Routine movement and survival can be
handled by deterministic simulation; an LLM is consulted at meaningful
decision points such as projects, discoveries, relationships, crises, and
creative proposals.

The human is an observer-director: the full map is visible without fog of war,
while inhabitants only know what they perceive, remember, or learn. The player
can inspect an inhabitant and send either a suggestive instruction or a must-do
instruction, as well as persistent world directives and direct broadcasts.
The world uses a configurable 365-day calendar with simple seasons and a
day/night cycle; it continues spending provider resources while unpaused.

## Design boundaries

- **Protected kernel:** time, space, health, needs, mortality, persistence,
  authority, validation, and sandbox boundaries cannot be removed by an
  inhabitant.
- **Emergent society:** jobs, customs, factions, economies, buildings, games,
  and institutions should arise from inhabitants and their circumstances.
- **Agent-created world:** inhabitants may propose new content and systems,
  but proposals are validated, simulated, versioned, and rolled back safely.
- **Design first:** the concept and invariants come before a large Godot
  project or multiplayer surface.
- **Multiplayer later:** the first target is one private persistent world, but
  the simulation should be authoritative and network-friendly from the start.

## Documentation

Start with the [documentation map](docs/README.md). The shortest path into the
project is:

1. [Vision](docs/concept/vision.md)
2. [Feature scope](docs/planning/feature-scope.md)
3. [Roadmap](docs/planning/roadmap.md)
4. [Decision register](docs/decisions/decision-register.md)

The implementation-level boundaries for the next phases live in the
[deterministic kernel contract](docs/planning/deterministic-kernel-contract.md),
[cognition and society contract](docs/planning/cognition-and-society-contract.md),
and [content-governance contract](docs/planning/content-governance-contract.md).

The [Phase 1 implementation ledger](docs/implementation/phase-1.md) records
which deterministic-kernel capabilities have executable evidence. GitHub owns
active work; the [document-authority guide](docs/governance/document-authority.md)
defines the boundary between design, delivery tracking, and proof.

Phase 1's headless core uses [C#/.NET 10](docs/planning/csharp-toolchain.md).
The browser viewer is deliberately a separate, non-authoritative diagnostic
project. The Godot client is separately non-authoritative, consumes the
versioned owner-observation boundary, and is the intended player-facing
client. [Device pairing](docs/planning/device-pairing.md) defines why private
Tailnet transport is not itself owner permission.

## Browser protocol discovery

Run the seeded-world debugger locally with:

    dotnet run --project src/AgentWorld.Viewer/AgentWorld.Viewer.csproj

The dependency-free browser page is intentionally a **discovery-only**
diagnostic surface. It can show the public versioned handshake and explain that
a paired owner device is required; it does not receive world state, inhabitants,
or event history. The legacy unauthenticated world/event/reconnect routes
explicitly refuse observation, and an accidental write to the old world route
still receives `405`.

An unpaired device may start, poll, and activate its own short-lived pairing
record. Full observation is a signed `POST /api/v1/owner/reconnect` request
from an active paired device. Pause, resume, instructions, paused authoring,
and device management are likewise signed owner requests that the server
validates before it commits anything. A paired device can request the signed
registry of paired-device lifecycle records, approve a pending device, or
revoke another device; that registry contains public identifiers, fingerprints,
and lifecycle state, never a private key, pairing code, or reusable challenge.
See the
[pairing policy](docs/planning/device-pairing.md) for the bootstrap and recovery
boundary rather than treating the browser page as an owner console.

By default the host loads or creates the persistent four-inhabitant private
world and advances it once per second. Its state is stored at
`saves/private-world.json`; set `AgentWorld__Runtime__AdvanceScript=false` to
start paused. The older owner fixture can be selected explicitly with
`AgentWorld__Runtime__WorldMode=fixture`, which is useful for protocol
compatibility tests and diagnostics. Deterministic decisions are the default.
Set
`AgentWorld__Runtime__DecisionProvider=jev` to opt into the TypeSafe Jev
micro-decision adapter and provide `TYPESAFE_API_KEY` through the host
environment; the key is read at request time and never enters world state.
`AgentWorld__Runtime__JevModel` defaults to the pinned `jev-1.13.0`. Runtime
state and paired-device authority state are stored separately and atomically; a
configured seed checks the identity of an existing saved world rather than
silently replacing it.

The packaged systemd unit optionally reads
`/etc/agentworld-viewer/provider.env`. Keep that file owned by root with mode
`0600`; it may select `AgentWorld__Runtime__DecisionProvider=jev` and supply
`TYPESAFE_API_KEY` without placing credentials in the checked-in unit, world
save, or process arguments. If the file is absent, the host remains on the
deterministic provider.

For playtesting with hosted chat models, set
`AgentWorld__Runtime__DecisionProvider=openai` or
`AgentWorld__Runtime__DecisionProvider=ollama-cloud`, provide the model through
`AgentWorld__Runtime__Model`, and provide `OPENAI_API_KEY` or `OLLAMA_API_KEY`
to the host environment. Both use the same bounded OpenAI-compatible adapter;
the endpoint can be overridden with
`AgentWorld__Runtime__ModelEndpoint`. Credentials are read at request time and
never enter world state. See the
[private single-world alpha plan](docs/implementation/playtest-vertical-slice.md)
for the remaining path from the current owner inspector to a substantially
complete playable world.

Paused authoring can attach an asset reference only when its exact `assetId`
and lowercase `sha256:<64-hex>` digest appear in the host-owned approved-asset
catalog. The deployed service reads
`/etc/agentworld-viewer/approved-assets.json`; a missing catalog is an empty,
deny-all catalog, and an invalid existing catalog prevents startup rather than
silently trusting a partial file. Owner requests cannot add catalog entries or
upload asset bytes.

The decision register is the current authority; the
[design log](docs/decisions/design-log.md) is the chronological record of why
those decisions were made. The retired Batch 5 worksheet has been removed:
every one of its policy proposals was accepted or superseded.

## Godot owner-client prototype

`src/AgentWorld.GodotClient` is a deliberately plain 2D owner inspector. After
device pairing it renders the server-issued map, inhabitants, needs, inventory,
decision factors, route, spatial knowledge, authoring state, instructions, and
ordered event suffix. It keeps the last coherent server projection if refresh
fails. Its controls only submit one-use, device-key-signed requests; the
headless server validates and records pause/resume, instructions, and paused
authoring batches.

The app presents a world-server URL for first pairing and retains the
non-secret registration metadata locally; the corresponding Windows
current-user device key is non-exportable. Developers can supply an initial
endpoint with `--world-url=<https-url>` after the Godot command separator.
Once paired, the client pins that server origin; changing it requires
forgetting the local registration and pairing again, rather than silently
retargeting an owner key. Users do not need the Godot editor.

The pending pairing is pinned too: its poll and activation steps stay at the
canonical origin where the pairing began. The client accepts activation only
when the expected authority, device, and public-key fingerprint still match.
If a network response is lost after submitting an instruction or paused
authoring batch, the app retains one non-secret request locally, bound to that
same authority, device, fingerprint, and origin. It can explicitly retry the
same server idempotency key or batch ID with a fresh signed request; it is not
an offline command queue, and the user can explicitly forget the retained
request. Private keys, pairing codes, signatures, and challenges are never
kept in that recovery record.

Verify the pinned Godot engine and scene without installing it globally:

    bash scripts/verify-godot-client.sh

The first export target is an **unsigned Windows 11 x64 portable bundle**.
`bash scripts/verify-godot-windows-export.sh` checks the pinned Godot editor and
export templates, produces a manifest-checked PE bundle, and is configured as a
GitHub Actions artifact. That is an export-path check, not a signed release,
installer choice, or proof of final Windows playtesting. The Phase 2 Windows
smoke test and paired reconnect were completed; see the phase ledgers and
roadmap for evidence.

## What AgentWorld is not yet

It is not currently:

- a finished game
- a finished or final-art Godot game UI
- a signed Windows installer or public desktop release
- an OpenClaw plugin
- an MMO or public server
- a free-form code execution environment for agents
- a claim that every interesting system has already been designed

Those may become future work. None should be smuggled into the foundation
without a clear design and testable boundary.

## Contributing

The most useful contributions during this phase are design critiques,
alternative models, small feasibility experiments, and precise questions. See
[CONTRIBUTING.md](CONTRIBUTING.md).

## Project name

The name **AgentWorld** is used for this project. The concept and design in
this repository stand on their own.

## License

AgentWorld is released under the [MIT License](LICENSE).
