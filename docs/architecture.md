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

The current host schedules one world tick per real second, and a tick is one
in-world minute. Its nominal 24-real-minute day is **prototype behavior**, not
the chosen finished-game calendar or pace.

In the repository's private-host path, hosted cognition is dispatched after a
committed tick and resolved at a later tick boundary. The saved scheduler queue
is the unresolved decision point; the external HTTP task itself is never save
authority. Admission checks request ID, provider epoch, run epoch and current
candidate legality. Other agents and world systems continue while it waits.
Pause, lost client presence and quit cancel the host task without inventing a
strategic answer; a restored world can retry its saved queue entry. Historical
fixture methods still support synchronous provider dispatch for isolated tests.

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
- Agents have bounded personal knowledge. A fact in the world or visible to
  the player is not automatically known to every agent. Accepted personal-model
  decisions can include a short in-character private thought; the last eight
  are saved and projected only into that agent's owner-visible profile. They
  are not hidden model reasoning, public dialogue, or knowledge transferred to
  another agent. The broader inspectable Memories UI is not implemented yet.
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
  and its referenced history.
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
