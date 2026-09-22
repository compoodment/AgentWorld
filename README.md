# AgentWorld

**AgentWorld** is a private, persistent simulation inhabited by autonomous
people. The player observes the whole world, inspects its inhabitants, gives
suggestive or mandatory instructions, and watches a deterministic simulation
execute every accepted action.

The long-term goal is a small society that survives, builds, trades, forms
relationships and institutions, and safely expands its own world. The current
build is an early private alpha: the technical foundation is substantial, but
the connected gameplay is still thin.

## Current state

The normal playable path is:

```text
Windows Godot client
        ↕ signed paired-owner HTTPS
headless .NET world host on the private VPS
        ↓
authoritative simulation, saves, cognition and provider adapters
```

Today the integrated private world has:

- four persistent inhabitants;
- movement, hunger, energy, harvesting, eating and sleep;
- a seeded map, renewable resources, calendar, seasons and weather;
- inspectable needs, intentions, inventories, relationships and events;
- pause/resume, paired-device authority and owner instructions;
- deterministic cognition plus optional Jev, OpenAI and Ollama Cloud roles;
- save/reload, restart recovery and secret-safe operational telemetry;
- governed data-only content, building and production machinery.

The important limitation is that many deeper systems exist as contracts,
fixtures or narrow runtime primitives rather than recurring player-visible
loops. A fresh private world does not yet activate a useful built-in content
pack, so inhabitants have few items, projects or economic reasons to interact.
Trade, family, factions, law, currency and culture are present in bounded state
or tests, but they do not yet feel like a living settlement.

See the canonical [current-state report](docs/status/current-state.md) for the
capability-by-capability truth, the [roadmap](docs/planning/roadmap.md) for what
comes next, and [known bugs](docs/bugs.md) for confirmed defects and gaps.

## Runtime rules

- The server is authoritative. Clients render observations and submit signed
  requests; they never commit world state directly.
- The host stays reachable, but simulation time and paid cognition advance only
  while at least one authenticated game client has a current presence lease.
- Closing the last client stops the world after a five-second grace period.
  Reconnecting resumes from the same tick with no offline catch-up.
- Manual pause remains paused after reconnecting.
- Models choose only among legal high-level intentions. Deterministic code
  validates and executes movement, costs, collisions, work and state changes.
- Provider credentials remain on the host in a separate restricted file. They
  are never returned to the client, stored in the world save or written to
  telemetry.

## Cognition roles

The paired owner configures two independent roles in the game settings:

- **Routine survival:** Deterministic or Jev
- **Planning and work:** Deterministic, OpenAI or Ollama Cloud

Jev and one large-model provider can be active at the same time. Critical
survival needs suppress strategic work. The current default world rarely
reaches meaningful planning decisions because it lacks an activated starter
content set; fixing that is the first roadmap milestone.

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/AgentWorld.Simulation` | Authoritative simulation, cognition, society, content and persistence |
| `src/AgentWorld.Viewer` | Headless HTTP host, pairing, signed owner API and live runtime |
| `src/AgentWorld.GodotClient` | Player-facing Godot client and Windows export |
| `tests/AgentWorld.Simulation.Tests` | Deterministic, protocol, persistence, security and client-contract tests |
| `docs/status/current-state.md` | Canonical implemented/playable status |
| `docs/planning/roadmap.md` | Canonical future sequence and acceptance gates |
| `docs/bugs.md` | Canonical confirmed bugs and product gaps |

The static web root retained by the host is legacy protocol-diagnostic
infrastructure. It is not a supported game client and is deliberately not part
of the normal player path.

## Build and verify

The authoritative projects use C# 14 on .NET 10. The client uses Godot 4.7.2
with C# and exports an unsigned Windows 11 x64 portable bundle.

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet test --configuration Release --no-restore
bash scripts/verify-godot-client.sh
bash scripts/verify-godot-windows-export.sh
```

The Windows bundle is uploaded by GitHub Actions. It is not yet a signed public
release or installer.

## Documentation

Start at the [documentation map](docs/README.md). It defines which document is
canonical for each kind of question and labels design, current status, future
work, implementation evidence and history separately.

The short version:

1. [Current state](docs/status/current-state.md)
2. [Roadmap](docs/planning/roadmap.md)
3. [Known bugs](docs/bugs.md)
4. [Vision](docs/concept/vision.md)
5. [Decision register](docs/decisions/decision-register.md)
6. [Architecture](docs/planning/architecture.md)

## What AgentWorld is not yet

- a finished game or final-art UI;
- a complete autonomous economy or society;
- a signed installer or public desktop release;
- a public server, MMO or multiplayer product;
- a safe host for arbitrary generated code.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Keep changes grounded in an observed
gameplay need, an accepted decision, or reproducible evidence. Do not confuse a
schema or fixture with a player-visible feature.

## License

AgentWorld is released under the [MIT License](LICENSE).
