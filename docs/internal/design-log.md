---
title: AgentWorld Design Log
type: working-notes
status: active
updated: 2026-09-18
---

# Design Log

This is a public working record. It captures current decisions without
pretending they are permanent.

## 2026-09-18 — Initial concept capture

### Current decisions

- The project name is **AgentWorld**.
- The project will be open source on GitHub.
- The project is in concept and foundation design, not implementation.
- The world should persist and continue without cron-triggered tasks or a human
  issuing every action.
- A default world protects its core survival laws: health, needs, mortality,
  time, validation, persistence, permissions, and sandbox boundaries.
- The first world begins with one to three founders.
- Inhabitants may self-name and have personalities, values, skills, roles, and
  aspirations.
- Roles should be emergent rather than permanent classes.
- Population should grow through limited family formation rather than
  arbitrary agent spawning.
- The first world is private; multiplayer is a later goal, but the server
  boundary should be authoritative enough to support it later.
- Godot is the leading visual/client candidate, not a final commitment.
- The first real LLM experiment should use one inhabitant and event-driven
  cognition, with local mock agents for development and testing.
- Agent-created world changes should begin as safe declarative proposals and
  only become executable code if a real sandbox and rollback model exists.

### Deliberately not decided

- exact founder composition and family model
- final need list and balance
- exact world size, tile scale, and chunking strategy
- model/provider name, call frequency, and API budget
- Godot versus a separate simulation service
- storage and networking implementation
- the full mod schema and sandbox technology
- the amount of direct human control
- whether dimensions, combat, trade, ecology, or weather belong in the first
  compelling world

### Design principle

The concept comes before the foundation. The foundation should implement a
reasonably complete concept, not force the concept to become whatever the
first framework makes convenient.
