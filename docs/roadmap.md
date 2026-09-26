---
title: ClankerWorld Roadmap
type: roadmap
status: active
updated: 2026-09-26
---

# Roadmap

This is the implementation **sequence**, not a second vision or a list of
features already playable. [Vision](vision-interview.md) owns the intended
first complete game; [current state](current-state.md) owns today's connected
Godot/private-VPS path. Exact scheduling and balance follow measured play.

## 1. Make the present private world coherent

Keep the VPS-backed development/playtest path while turning existing pieces
into one understandable loop: useful starter resources, persistent work,
inventory and ownership, barter and cooperation, consequential survival, and
clear Godot feedback. Provider usage and failures must be visible without
exposing credentials or private raw traces.

**Gate:** in a normal Godot session, agents acquire distinct inputs, choose and
complete a useful project, exchange or share a needed item, use the result,
and react visibly. The result survives save/reload. Isolated fixtures alone do
not pass the gate.

## 2. Build the intended player/world foundation

- Main Menu, New World generation/preview, size/climate options, east–west
  wrapping, chunked map and map overview.
- Empty generated base camp; four in-world founder additions with per-agent
  provider/model/key choice; explicit Start World.
- Agreed world clock, day/night and regional weather; player-local display
  preferences, the proposed Game/World Settings split, autosaves and recovery.
- World-first pixel-art presentation, agent/event/conversation inspection,
  filters and usable navigation.

**Gate:** a new world can be generated, saved partway through founder setup,
started after four unrelated agents join two households, paused/reopened, and
observed through the agreed UI without operator-only preparation.

## 3. Make agents and society genuinely continuous

Personal cognition, bounded private memory, conversations, childhood,
caregiving, aging, deaths/wills, family-tree history, learning, ownership,
trade, settlements and breakable laws must form actual in-world loops. Expand
ecology, health, clothing, buildings, land, livestock and consequential but
not constant combat alongside them. Jev remains optional.

**Gate:** a representative world survives across generations with explainable
social and material consequences. Private knowledge does not teleport between
agents; provider failures do not freeze unrelated agents or fabricate choices.

## 4. Make creation and modding real

Use one governed package pipeline for agent inventions and explicitly
imported human mods. Support bounded content first, then restricted scripted
behavior where the vision requires it. Validate dependencies, resources,
rights, runtime limits, save compatibility, art and rollback before activation.
Build the in-world/global Mod Library and the approved pixel-art workflow.

**Gate:** useful new content is invented or imported, previewed, activated,
used by agents, saved/reloaded and safely rejected or quarantined on failure
without changing protected world rules or accessing player secrets.

## 5. Deliver the first finished Windows game

Package the same authoritative simulation with the Godot game on the
player's own PC. Their saves and keys remain local; their own cloud-model
accounts pay for calls. The installation, updates, recovery, performance,
AI-usage controls, and world/mod compatibility must be tested without
requiring computment's VPS. Linux/macOS are not launch promises.

**Gate:** a new Windows player can install, configure a provider when adding
an agent, play/save/restore a meaningful world, and understand failures and
cost controls without a developer operating the server.
