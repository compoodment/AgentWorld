---
title: Batch 5 Decision Worksheet
type: design
status: proposed
updated: 2026-09-19
---

# Batch 5 Decision Worksheet

This worksheet turns the remaining Batch 5 questions into concrete proposals.
The suggested defaults are deliberately small enough to prototype and measure;
they are not closed decisions until the design interview accepts or changes
them.

Answer by number. “Accept the default” is a valid answer; only the exceptions
need explanation.

## 1. Fatigue, sleep, and recovery

### Suggested default

Use a `fatigue` value from 0 (rested) to 100 (exhausted), integrated per
in-world hour rather than tied to a particular engine tick:

- awake baseline: `+1.5`
- light activity: `+1.5` additional
- strenuous work: `+4.0` additional
- sleep recovery: `-8.0`
- safe bed bonus: recovery `×1.25`
- proper shelter bonus: recovery `×1.15`
- safe unsheltered sleep: recovery `×0.65`

Clamp fatigue to 0–100. At fatigue above 90, health damage begins at
`0.25/hour` and rises to `1.0/hour` at 100. Walking and work slow gradually:

```text
t = clamp((fatigue - 35) / 65, 0, 1)
walk_multiplier = 1 - 0.45t
work_multiplier = 1 - 0.60t
```

Wake naturally at fatigue 15 or after 12 in-world hours, whichever comes
first. Severe danger can wake an inhabitant earlier. An inhabitant may request
an earlier wake-up, but the kernel owns the final state transition.

### Example

An inhabitant reaches camp at fatigue 78. A sheltered bed recovers about 11.5
fatigue per hour, so it wakes after roughly 5.5–6 hours. Safe open ground
recovers about 5.2/hour, so it may sleep close to the 12-hour cap and still be
tired. At fatigue 70, walking is about 76% speed and work output about 68%.

### Question

Accept this curve, or should the first world be harsher/softer? In particular:
should exhaustion damage start at 90, or only at 100?

## 2. Exposure and unsafe sleep

### Suggested default

Compute an effective exposure score from weather, terrain, time of day, and
shelter protection. Keep the first prototype legible:

- 0–30: comfortable; no health effect
- 31–60: recovery penalty and rising illness chance
- 61–80: unsafe; roughly 1–3% injury/illness risk per sleep hour
- 81–100: severe; roughly 5–10% risk per sleep hour and possible wake-up

Use the seeded simulation RNG for outcomes so a replay can reproduce them.
Do not make one bad night instantly kill a healthy inhabitant. Death should
come from sustained exposure, a predator/hostile event, or an already critical
condition.

An active predator or hostile threat is not just another exposure percentage:
it produces an urgent danger event and normally wakes/flees the sleeper.

### Example

A bed inside a sealed shelter reduces a cold storm from effective exposure 75
to 20. Sleeping under a tree reduces it only to 55: poor recovery and illness
risk, but not immediate death. Sleeping in the open at 85 is an unsafe-sleep
decision with a meaningful chance of waking injured or sick.

### Question

Accept these four bands, or do you want unsafe sleep to be more lethal or more
forgiving?

## 3. Cognition timing and event priority

### Suggested default

Use an event queue with four urgency bands:

1. **Critical:** active danger, critical health, fire, or immediate survival
2. **Urgent:** blocked route, failed action, direct must-do instruction, food or
   shelter deadline
3. **Normal:** discoveries, project changes, relationship events, ordinary
   messages
4. **Background:** reflection, memory consolidation, and low-stakes planning

Recommended initial timing:

- critical: deterministic emergency behaviour immediately; model request may be
  queued without blocking survival
- urgent: coalesce for 2 real seconds / about 12 world minutes, then decide at
  the next action boundary
- normal: coalesce for 10 real seconds / about 1 world hour
- background: batch for 60 real seconds / about 6 world hours
- after a normal decision: 30 world-minute cooldown unless a higher-priority
  event arrives

If several events are present, sort by survival, human must-do, action failure,
deadline, relationship, project, then curiosity. Suppress a new model call if
the event cannot change the current plan; record the suppressed event in
telemetry instead.

### Example

An inhabitant receives three small messages while walking. They become one
normal cognition request. If the route then becomes blocked, the route failure
raises the request to urgent; it does not create a second simultaneous call.

### Question

Accept these initial windows, or should the prototype prefer fewer/cheaper calls
or faster/more reactive calls?

## 4. Attention and observation size

### Suggested default

The observation builder always includes current tile, body/urgent needs,
current intention and destination, immediate local perception, and active
instructions. Optional facts are ranked deterministically by:

```text
urgency + current-goal relevance + recency + relationship relevance + novelty
                 - redundancy
```

Start with a compact budget: up to 8 memories, 8 messages, 3 known-location
changes, and 1 weather summary. Prefer references to structured records over
repeating prose. Make the budget configurable per model capability, but keep a
small default for predictable cost.

### Example

For “hungry while travelling,” include the hunger state, known nearby food,
the current route, a blocked-path event, and dangerous weather. Omit an old
argument and a distant meadow that cannot affect the decision.

### Question

Accept deterministic attention ranking, or should inhabitants receive a larger
raw memory context and let the model choose what matters?

## 5. Developer telemetry and replay

### Suggested default

Persist one structured decision record per cognition attempt:

```json
{
  "schema_version": 1,
  "decision_id": "...",
  "world_tick": 1234,
  "sim_time": "year-1/day-4/07:20",
  "inhabitant_id": "...",
  "triggers": [{"kind": "route_blocked", "urgency": "urgent"}],
  "observation": {},
  "memory_refs": ["memory-17"],
  "provider": {"id": "ollama-cloud", "model": "..."},
  "response": {"status": "parsed", "latency_ms": 820},
  "validation": {"accepted": true, "errors": []},
  "action": {},
  "fallback": null,
  "result_event_ids": ["event-88"]
}
```

Developer mode stores the exact structured observation and raw model response
for inspection. It does not claim that hidden chain-of-thought is reliable or
necessary game state. A replay uses the saved observation, model response,
world seed, simulation version, and mod/asset manifest; it must not call the
live provider unless explicitly requested.

### Example

When an inhabitant refuses a must-do action, the viewer can answer: what it
saw, which memories were retrieved, which rule rejected the action, whether a
fallback occurred, and which state events actually committed.

### Question

Accept this schema direction? Should raw model responses be retained forever in
developer saves, or kept in a separate opt-in debug log?

## 6. Route-cache persistence and future traversal

### Suggested default

Route caches are a performance optimization, not authoritative world state.
Discard them on save/load by default and rebuild lazily. If a cache is persisted
for faster loading, key it by source, destination, movement profile, traversal
capabilities, map topology version, and simulation version; discard it on any
mismatch.

Keep the route contract extensible with traversal modes such as `walk`, `road`,
and later `boat`. Each mode supplies legal edges and movement costs; the first
prototype registers only walking and roads. Boats can later add water edges
without changing inhabitant intentions from “go to that destination.”

### Example

When a bridge is removed, its topology version changes and old walk routes are
invalid. When boats are added, a destination-level intention can choose a route
with a walk segment, a boat segment, and another walk segment.

### Question

Accept “rebuild caches after load,” or is faster save loading worth persisting
validated cache entries?

## 7. Relationships, consent, family, and death

### Suggested default

Represent relationships as a graph of typed, directed records rather than a
single family label. A record can carry participants, kind, status, affinity,
trust, consent state, obligations, and history. Keep household membership
separate from biological or legal parentage.

- partnership is opt-in, mutable, and dissolvable
- reproduction is a high-level, mutually consenting adult decision; the model
  cannot bypass consent or create a child directly
- children have caregivers, which may include parents and non-parents
- age bands are world-configurable: infant, child, adolescent, adult, elder
- death is a kernel state transition caused by health, injury, exposure, or
  age; an LLM can respond to death but cannot veto it

Avoid simulating sexual detail in the first prototype. Simulate consent,
partnership, pregnancy/birth, care, attachment, grief, and responsibility.

### Example

Two adults may choose partnership. Either can end it. A child may have two
parents and a separate caregiver network. If all caregivers become injured,
the care deficit becomes a deterministic need and a social event.

### Question

Accept this graph-and-care model, and should adulthood be a fixed age band or a
world-defined transition based on capability, culture, and ceremony?

## 8. Population growth without a fixed cap

### Suggested default

Use local sustainability conditions rather than a global population number.
Birth readiness requires consent and relationship conditions plus a rolling
food buffer, credible sleeping space, caregiver availability, and acceptable
health/safety. Overcrowding and shortages increase care burden, illness, and
conflict instead of triggering an invisible hard cap.

### Example

Two consenting adults with 14 days of food, one spare sleeping place, and a
reliable caregiver can plausibly support a birth. The same couple with two days
of food and no safe sleeping space receives a strong material reason to defer
it, but the world does not silently forbid the choice.

### Question

Accept material pressure as the main population control, or should the world
owner be able to require explicit approval for births?

## 9. First provider registry and capabilities

### Suggested default

Build three adapters first:

1. a deterministic mock provider for tests and offline development
2. an Ollama Cloud API adapter
3. a generic hosted HTTP adapter with configurable authentication and request
   mapping

Represent capabilities explicitly: structured JSON, tool calls, streaming,
vision, context size, rate limits, and model cost metadata. The first inhabitant
contract needs only structured JSON; tool calls, vision, and streaming can be
optional capabilities rather than assumptions.

### Example

The provider registry can reject a model that cannot reliably return the
required action schema, while allowing a cheaper model for background
reflection than for crisis decisions.

### Question

Accept mock + Ollama Cloud + generic hosted API as the first registry, or do you
want named additional providers in the first prototype?

## 10. Credential and model validation

### Suggested default

Credentials live outside world saves and are referenced by opaque provider
configuration IDs. Validation happens before a provider/model can be assigned:

- endpoint and authentication check
- model availability check
- minimal structured-output probe
- context and timeout check
- rate-limit/error-shape check
- capability declaration check

Never store API keys in event logs, prompts, screenshots, or exported saves.
If a new configuration fails validation, keep the last known-good assignment.

### Example

Selecting `ollama-cloud / model-X` runs a short probe in the provider settings
screen. A failed probe explains the missing capability and leaves the current
inhabitant on its previous working model.

### Question

Accept preflight probes, or should a provider be assignable first and validated
only when the first live decision is requested?

## 11. Changing an active inhabitant's provider/model

### Suggested default

Apply changes at the next cognition boundary. The current atomic action
continues. Give each inhabitant a provider configuration epoch; an in-flight
response from an older epoch is logged but discarded rather than applied.
Record the change as a world event with who changed it, why, and the old/new
provider references.

### Example

An inhabitant keeps walking while the human changes its model. The next valid
decision uses the new model. A late response from the old model cannot overwrite
the new intention.

### Question

Accept next-boundary switching, or should a human change cancel the current
action immediately?

## 12. Assets, performance, provenance, and preview

### Suggested default

Use an engine-neutral bundle for the first client:

- lossless PNG for pixel art and transparency
- JSON manifest for dimensions, anchors, layers, animation, collision, and
  interaction metadata
- optional OGG/WAV later for audio; no executable asset content

Choose one base tile scale for the first prototype, preferably 16×16 or 32×32,
and normalize assets into that grid while retaining the source candidate. Check
dimensions, alpha, palette/colour limits, frame count, file size, texture size,
collision footprint, draw cost, and naming. Preview every accepted asset in an
isolated scene before atomic installation.

Record source type, creator, license/permission, model/tool if generated,
prompt or transformation provenance when available, content version,
dependencies, and replacement history. Permit cultural variation in colour and
decoration; reject unreadability or broken interaction metadata, not merely an
unfamiliar aesthetic.

### Example

An inhabitant proposes a 40×40 crop sprite. The pipeline keeps the original,
normalizes it to the 32×32 world grid, checks the harvest footprint and four
animation frames, previews it beside existing crops, and records the proposing
inhabitant and source license.

### Question

Accept PNG + JSON at 16×16/32×32, and should the first world enforce one shared
palette or only a shared readability/style guide?

## Proposed order of implementation

1. fatigue, sleep, and exposure fixtures
2. cognition queue, attention budget, and telemetry/replay fixtures
3. route-cache invalidation and save/load behaviour
4. provider registry and preflight validation
5. provider switching
6. relationship/care and population fixtures
7. asset normalization and preview fixtures

This order keeps the deterministic survival and replay foundation ahead of
expensive model integration and social breadth.
