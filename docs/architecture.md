---
title: ClankerWorld Current Architecture
type: architecture
status: active
updated: 2026-09-26
---

# Current architecture

This describes the **existing playable prototype** and the technical
boundaries to preserve while it evolves. It does not turn the
[finished-game vision](vision-interview.md) into an implementation claim. See
[current state](current-state.md) for capability status.

## Today: Godot client and private VPS

```text
Windows Godot desktop client
        ↕ paired, signed HTTPS requests
private headless .NET host on the VPS
        ├── authoritative simulation and world clock
        ├── saves, event history and recovery
        ├── cognition scheduler and provider adapters
        └── validation of owner, agent and content requests
```

The supported game client is Godot, not the legacy diagnostic web assets. The
client renders observations and sends requests; only the host commits world
state. The simulation library does not depend on Godot or a live model
provider. The host remains reachable, but world ticks and hosted-model calls
require an authenticated client presence lease. Closing the last client stops
them after a short grace period; reconnecting does not simulate missed time.

The current host schedules one world tick per real second. Newly created
private worlds start paused and save the accepted playtest pace: 360 ticks per day and a 40-day
year with four 10-day seasons. The same world setup saves day-based lifecycle
thresholds (3/15/45/60 days). A day is therefore nominally six real minutes,
subject to provider/host load. The former 1,440-tick/365-day development save
is archived, not silently reinterpreted as a new-world save.
Owner observations report the saved world-system ticks per day and days per
year; the client uses those values for clock/date presentation rather than
assuming one fixed tick length. The 365-day prototype and new 40-day calendar
have display mappings; profiles show age in years or days according to the
saved lifecycle. Matching society/world-system calendar values are validated
on restore.

In the repository's private-host path, hosted cognition is dispatched after a
committed tick and resolved at a later tick boundary. The saved scheduler queue
is the unresolved decision point; the external HTTP task itself is never save
authority. Admission checks request ID, provider epoch, run epoch and current
candidate legality. Other agents and world systems continue while it waits.
Pause, lost client presence and quit cancel the host task without inventing a
strategic answer; a restored world can retry its saved queue entry. Historical
fixture methods still support synchronous provider dispatch for isolated tests.
The private save also owns Jev availability. Changing it requires a paused
world, cancels pending hosted work and changes the provider epoch. If Jev is
off, decisions previously routed to Jev use the inhabitant's personal planning
provider, then the world planning provider, then deterministic safe action.
The provider credentials remain installation-local and are not erased by this
world switch. An older saved world retains its prior format and default-on
behavior until the setting changes; the first change writes private-world
schema 15 so older hosts cannot silently discard the world choice.
Jev-assisted memory compaction is not implemented yet.

## Authority and failure boundaries

- A model chooses among legal intentions; deterministic world code validates
  and executes movement, work, resources, occupancy, ownership and effects.
  Text from a model, mod, client or save file is never authority to change
  state directly.
- Hosted provider work runs between committed ticks. A rejected, cancelled
  or superseded answer cannot leave a half-applied action. Pausing or losing
  client presence invalidates in-flight work in the current host.
  Failure or low confidence selects only the explicit `safe_idle` fallback;
  it cannot execute a strategic candidate or complete an instruction. Other
  agents and world systems advance while a hosted request remains unresolved.
- API credentials are stored separately from world saves and must not be
  returned in observations or written to telemetry. Operational logs are
  bounded, structured outcome records, not raw prompts or secret-bearing
  responses.
  The installation-local provider store also owns named key slots for agent
  assignments. Signed owner actions bind the selected slot ID and new-key
  label; owner status returns only non-secret IDs and labels. One personal
  provider/model selection produces routine and planning assignments together.
- Agents have bounded personal knowledge. A fact in the world or visible to
  the player is not automatically known to every agent. Accepted personal-model
  decisions can include a short in-character private thought; the last eight
  are saved and projected only into that agent's owner-visible profile. They
  are not hidden model reasoning, public dialogue, or knowledge transferred to
  another agent. The owner can inspect up to 16 recent, non-tombstoned social
  memories per agent, including private records and deceased profiles; this is
  distinct from public dialogue and the authoritative event log. Belief
  uncertainty, broad episodic recall and Jev-assisted compaction are not yet
  implemented.
- Live physical actors remain separate from saved deceased records. On death,
  the runtime archives the last physical state and frozen age alongside the
  society death record, then removes the actor from active movement and work.
  Owner observations expose the archive for inspection without treating it as
  a living map entity. Earlier deaths with no physical archive cannot be
  reconstructed from an old save.
- Saves are versioned and atomically replaced after committed state changes.
  Restores must fail visibly on unsupported or mismatched state rather than
  silently substitute content or credentials. The current host keeps bounded
  hot history and hashed older event segments; backups must include the save
  and its referenced history. Named manual checkpoints live beside the active
  save in a private `.manual` directory; their snapshots reference the same
  history archive. Checkpoint metadata records per-agent provider/model and
  credential-slot IDs, never API-key bytes. A full backup must retain the
  active save, `.history`, `.manual`, pairing authority and global provider
  configuration together.
- The current content path accepts bounded, validated data-only designs.
  Arbitrary executable agent code is disabled. The vision includes a
  restricted script sandbox and explicit human-mod import; neither is a
  claim about today's playable path.

These are implementation boundaries, not a second list of product decisions.
The detailed past phase contracts were retired from the active documentation;
the corresponding code and tests remain the executable proof.

## Finished Windows distribution target

For development and computment's playtesting, the VPS arrangement remains
useful for live observation and debugging. The first finished release for
other players instead packages the **same authoritative simulation** on that
player's Windows PC, with local saves and local provider-credential storage.
It must not depend on computment's VPS or a hosted game account. Cloud model
providers still require network access and the player's own keys. Whether the
local host is embedded or bundled as a companion process is open.

The vision ledger owns future behavior, including the custom calendar,
in-world founder setup, private-memory inspection, optional AI-usage stop,
Jev toggle, safe agent inventions and external mods. [Current state](current-state.md)
reports what is connected to normal play.

## 2D geography prototype (not the live world)

`GeographyGenerator` is a separate, deterministic tile-map primitive; it does
not replace the current 6×5 playable map or its save format. It samples a
vendored [FastNoiseLite](../src/ClankerWorld.Simulation/ThirdParty/FastNoiseLite/README.md)
field for broad elevation and long-run rainfall. Wrapped worlds sample a circle
in noise-input space and use east/west-wrapped tile neighbors; the game map
itself remains two-dimensional. A water-coverage threshold, connected-water
classification and ocean-outward priority drainage then identify oceans,
lakes and upstream-accumulated river channels. The candidate size dimensions
and river threshold are tuning targets, not finished-game promises.

The design follows the separation between local noise and global hydrology in
[Red Blob's noise guide](https://www.redblobgames.com/maps/terrain-from-noise/),
the ocean/lake and river-flow methods in the original
[polygon guide](https://xenon.stanford.edu/~amitp/game-programming/polygon-map-generation/)
and its [Mapgen2 source](https://github.com/redblobgames/mapgen2), and the
ocean-outward drainage / rainfall accumulation in
[Mapgen4 source](https://github.com/redblobgames/mapgen4/blob/master/map.ts).
The [Voronoi tutorial's source](https://www.redblobgames.com/x/2022-voronoi-maps-tutorial/voronoi-maps-tutorial.js)
also makes the local-minimum failure mode explicit. ClankerWorld uses tile
neighbors rather than importing those projects' polygon meshes. This is an
implementation inference, not a locked terrain design: continent layout,
climate modes, biome/object placement, map preview, chunk streaming and river
appearance still need integration and playtesting.

## Deliberate pre-release identifier reset

The application, .NET projects, Godot title, Windows executable, save/content
headers, hash domains, built-in package IDs, owner-request proof domains,
Windows `user://` directory and CNG key name now use **ClankerWorld**. This is
an intentional pre-release compatibility break approved for a fresh development
world and new owner pairing, not an in-place migration of old saves or keys.
The [deployment template](../deploy/clankerworld-viewer.service) uses new
installation and state paths. Keep the previous private world and pairing
authority only in a root-only rollback backup; copy provider credentials to the
new protected state path without printing them. Ignored old export/build
artifacts are historical binaries, not current source names.
