---
title: ClankerWorld Vision Interview and Decision Ledger
type: product-vision
status: active
updated: 2026-09-26
---

# ClankerWorld Vision Interview — Current Working Picture

> Last reconciled: 2026-09-26. This is the **current** interview notebook, not a
> finished specification, implementation claim, or approval to modify the game.
> It separates decisions from proposals and unresolved questions. The
> [historical interview record](archive/vision-interview-history.md)
> preserves earlier answers and Clanker's proposals, including old “Open” labels
> that have since been resolved. **When they disagree, this file wins.**

This file is the **living ledger of computment's intended finished-game
experience** during the interview. A decision here is not a claim that the
current ClankerWorld prototype implements it, nor an instruction to build every
accepted feature immediately. The repository's current-state and architecture
documents must describe what actually runs; its roadmap chooses implementation
order. This ledger is versioned in the game repository as the single authority
for owner intent, with the former workspace path retained as a pointer for
continuity.
Historical interview material remains separate and subordinate.
Obsolete phase-by-phase implementation documents were removed from the active
documentation during repository cleanup; Git retains their history. Operative
build, pairing and release rules remain in focused references. This ledger
still distinguishes the finished-game vision from the present prototype.

## How to read this

- **Decided** means computment chose or accepted the direction. Exact numbers or
  implementation details may still need playtesting.
- **Preferred** means a strong current leaning, but not yet a final choice.
- **Proposed** means Clanker's suggestion, not yet accepted as canon.
- **Open** means we really have not settled the question.
- The finished-game vision is distinct from what the existing ClankerWorld
  prototype currently implements. Repository code and documents are evidence,
  not automatic authority over ClankerWorld's future design.

## What ClankerWorld is

**Decided:** ClankerWorld is a pixel-art world simulation about AI agents
surviving, socializing, building civilization, and eventually inventing things
that can change their own world. The simulated people are called **agents**. The
personal LLM is integral to each agent's thinking and social identity, not a
cosmetic narrator. Deterministic world systems enforce what can physically and
legally happen. Jev may be enabled as a per-world support layer; it does not
replace agents' personal models.

The game should allow observation and player intervention without making
survival relentlessly harsh or conversations rare. There is no arbitrary
fictional population cap. Whether to add an explicit player-configurable limit
on automatic births is **deferred until playtesting**, not currently decided.
Hardware, provider capacity, and cost still impose practical limits.

## Where the game runs

### Decided direction

- **During development and computment's own playtesting:** keep the current
  Godot desktop client connected to the private VPS-hosted simulation. This
  allows Clanker to inspect server logs, observe the running world, debug, and
  tune behavior. There is no need to move the current development world just
  to settle the eventual distribution architecture.
- **For the first finished game distributed to other players:** provide a
  simple PC install that runs the game and its authoritative simulation on
  that player's own computer. Their saves and provider credentials belong on
  their computer; playing must not depend on access to computment's VPS or a
  mandatory hosted game account. If they choose cloud-hosted LLMs, those
  providers still require an internet connection and applicable credentials;
  local game hosting does not mean all model inference is offline.
- **The first finished release supports Windows only.** Computment has a
  Windows laptop for actual desktop playtesting, but no Linux or macOS desktop
  testing setup. Linux/macOS support is not a launch obligation; interested
  contributors could help add and verify those platforms later. A Linux
  server machine does not count as testing a Linux desktop game.
- **Players bring their own model-provider API keys and pay their own provider
  charges.** The game does not include developer-funded AI usage for other
  players. Provider credentials are stored per installation, separate from
  shareable world saves; they are not bundled with the game. Opening the game
  does **not** require a key or a first-launch provider setup gate. Credentials
  are supplied or selected when assigning a provider/model to an agent.
- Keep one simulation/game-rules implementation across the private-VPS and
  local-PC deployments. The player-facing Godot client should not become the
  authority merely because the host runs locally. Closing the game still
  freezes the world, without offline catch-up.

### Current prototype evidence (not a finished-game constraint)

The supported playable path is an unsigned Windows 11 x64 **Godot desktop
client** communicating by signed HTTPS with a private headless .NET world host
on the VPS. The host owns simulation, saves, cognition, and provider adapters.
The existing static web assets are legacy diagnostics, **not a browser game**.
The host stops ticks and model calls shortly after the last authenticated
client disconnects. This arrangement is useful development scaffolding, not
the intended required infrastructure for other players.

### Open implementation details

Whether the local host is embedded in the installed game or bundled as a
background companion process; installer and update design; exact Windows
version/architecture support; local credential storage and diagnostics export;
save migration between deployments; performance requirements and packaging
tests; on-demand credential validation and the new-world founder setup when no
key exists yet. These are implementation choices to prove, not reasons to
reopen the decided player-local distribution goal.

## Main Menu, world view, and controls

### Decided

- Launch into the **Main Menu** with **New World**, **Load World** when worlds
  exist, **Settings**, **Quit Game**, and a separate **Continue** action for the
  most recently played world.
- **New World** offers world size and climate choices, advanced generation
  controls, preview/reroll, wrapping, and an optional early survival grace
  period. The ordinary game has **one supported starter-camp mode**. The
  nothing-start challenge mode was removed from the plan for now. Generating
  the world creates the map and **empty base camp**, then opens that world so
  the player can add its four founders there; API keys are not a prerequisite
  for reaching the world view.
- Enter a visually coherent generated world. The player can see its whole
  geography from the start; there is no player fog of war. Individual agents
  may know only what they have experienced or learned.
- One continuous pixel-art world view supports mouse-wheel zoom and WASD
  panning. There is **no separate simplified regional view or second regional
  texture set**. Zoom-out stops at a readability/performance limit.
- An always-accessible **top-left Map button** expands a world overview. It
  shows the current camera rectangle; dragging it or clicking the overview
  moves the main camera. The overview can be drawn from world data rather than
  a second library of game textures.
- The top bar also shows a **pause/resume-time control**, in-world date and
  time, agent population, a **World Info** button, a **Filters** button, an
  **Event Log** button, an **Add Agent** button near the menu button, and the
  menu button at top-right. Date display format and 24-hour/AM-PM time format
  belong in UI Settings.
- Important events appear in the **Event Log**; out-of-view events can also
  produce small notifications, while routine activity stays quiet. Examples
  include births, deaths, inventions, and newly founded settlements. Clicking
  the top-bar Event Log button opens the log; clicking a located event moves
  the camera to where it happened. Opening the log does not pause the world.
  Players can choose which event types show pop-up notifications; suppressing
  a pop-up does **not** remove that event from the full log.
- World Info should let the player inspect discovered capabilities and other
  world information. Filters should reveal established household property
  borders, settlement borders, and similar world facts. The UI must not invent
  ownership or borders that agents have not established.
- The top-right menu button opens the **Pause Menu** and pauses the world. It
  includes **Save World**, **Settings**, and **Quit to Menu**. Settings use
  categories on the left and selected controls on the right. Quit to Menu and
  Quit Game ask for confirmation.
- Adding an adult agent opens a flow to select a provider, one of its stored
  API credentials or a newly entered one, and a model, then place the agent in
  the world. Existing credentials can be reused by multiple agents. A player
  may also add another key for the **same provider** and choose which key that
  agent uses; there is no single-key-per-provider restriction. The agent
  chooses its own name after placement; the player can rename it later.
- Selecting an agent opens an **interactive info popup near that agent**. The
  player can change that agent's provider and model there, including choosing
  an appropriate stored/new credential when needed. Agent inspection does not
  pause the simulation. This is a selected-agent panel, not a fleeting
  mouse-hover tooltip that disappears when reaching for its controls. Its core
  view shows name, age/life stage, household, current activity, needs, and the
  agent's latest **private** thought or intention. A **small scrollable history
  of recent private thoughts** is available there too. The player can inspect
  them, but other agents do not automatically know them. A separate
  **Memories** section lets the player inspect what this agent remembers or
  believes happened, including mistaken beliefs; it is not the authoritative
  world event log. Inventory, relationships, and model settings can expand
  from the same popup. A **Family Tree** action opens a larger interactive
  graphical view; selecting a person in the tree opens that person's agent
  info popup, including for deceased relatives. The tree distinguishes
  parent-child ancestry and partnerships; household membership is displayed
  separately, never as proof of biological family. Unrelated starter
  housemates must not be drawn as relatives.

### Preferred, pending confirmation

Split the Pause Menu's generic **Settings** entry into **Game Settings** and
**World Settings**. Game Settings apply across worlds/on this installation:
UI date and time display formats, graphics/display preferences, and stored
provider credentials. Event pop-up preferences likely belong here too, but
their scope is not yet confirmed. The Main Menu's Settings entry would open
Game Settings. World Settings belong to the current save: autosave on/off,
interval and rotation; the optional AI-usage meter/limit; and Jev's per-world
configuration. Opening either from the Pause Menu keeps the world paused.
World generation choices such as size, climate and wrapping are chosen before
creation and should be inspectable afterward, not silently mutable settings.
Jev's on/off switch for an existing world is accepted; the exact settings
categories and transition behavior for an in-flight Jev task remain open.

In the **Add Agent** placement view, show existing household property and
settlement borders so computment can see the new agent's initial affiliation.
Suggested precedence: placement on household-owned tiles joins that household
(and its enclosing settlement, if any); placement elsewhere within a
settlement joins the settlement but no household; placement on unclaimed land
outside both starts an independent agent. Location establishes **starting
social membership**, not biological ancestry or permanent membership based on
where the agent later walks. A player-added adult could be a new unrelated
founder line even when placed inside an existing household. Exact overlap,
capacity, consent, and invalid-placement rules are still open. The agreed
starter-world household bootstrap below is separate from this later Add Agent
placement rule.

### Open

Exact top-bar layout on small screens; the final zoom-out/visible-tile cap;
which overview and filter layers ship first; display of disputed or overlapping
claims; the precise event categories, default pop-up choices, filter UI,
event retention, and handling of events without a single map location; custom
month/season names and date presentation; and the detailed player-control/
observer boundary beyond adding and renaming agents. Computment may provide a
UI drawing.

The agent info popup's exact layout, pin/expand behavior, thought-history
retention count, how memories are grouped/searched/labeled, family-tree
line styles/navigation and any future guardianship/adoption links, and what
happens to a pending model call when its provider/model is changed are open.
A proposed low-cost implementation is to show a short, timestamped,
in-character thought/intent supplied alongside an agent's ordinary decision,
rather than generating a continuous stream or
presenting inaccessible model-internal reasoning as the agent's thoughts.
The recent history should persist with the world save but remain bounded.

## World time, pausing, and slow models

### Decided

- **The world freezes when the game is closed.** It resumes from the saved
  state when reopened, without unattended catch-up simulation or provider
  spending. The supported current private-world prototype already stops ticks
  after the last authenticated client disconnects; a much older design-log
  direction for unattended simulation was superseded in the repository.
- While the game is open, the **pause button and Pause Menu are the only
  ordinary player-opened controls that pause the simulation**. Opening the map,
  World Info, agent inspection, or a conversation panel leaves time running.
  Reaching an optional AI-usage hard limit is a separate automatic pause.
- **Fast-forward/speed controls are not on the current roadmap.** They may be
  reconsidered later, but faster clock progression does not make model calls
  return faster.
- If one agent's model is slow or unavailable, the rest of the world and other
  agents continue. The affected agent may wait or do safe routine activity;
  the game must not fabricate that agent's important choices. If its provider
  fails or its key runs out of credit, the game shows the problem and **does
  not automatically switch that agent to another paid model**. The player may
  repair the credential or explicitly choose a new provider/model from the
  agent popup. Jev cannot impersonate the affected agent's personal model.
- When the player closes the world/game while a model request is in flight,
  save and exit promptly rather than waiting for that answer. Cancel or discard
  the unfinished request, commit **no partial agent action**, and retain the
  unresolved decision point. On reopening, the agent re-evaluates it if still
  relevant, which may require another provider call. The provider might charge
  for both the abandoned call and its later retry; this is an accepted
  trade-off for responsive quitting and no background model work.
- The finished game offers an **optional AI-usage limit** alongside
  a visible usage meter. When the limit is reached, it pauses the world and
  asks before making further paid model calls; it does not cap the fictional
  population. Whether the limit applies per world or across the installation,
  its accounting unit, period/reset behavior, warning levels, and
  provider-bill accuracy remain to be designed.
- **The current prototype's 24-real-minute day is rejected as ClankerWorld's
  finished-game pace.** It was an implementation choice, not a user-approved
  design decision.
- **Accepted starting pace for playtesting:** a custom **40-day year** with
  four **10-day seasons**, initially one six-real-minute day, one real hour per
  season, and four real hours per year. Four 10-day months can support the
  chosen numeric date display. A six-hour maximum life from birth would then
  span at most 60 world days, 1.5 years, or six seasons. These numbers may be
  changed after playtesting; they are not final performance or pacing promises.
  This custom calendar supersedes the earlier 365-day preference.
- **Preferred:** biological age corresponds to elapsed world/calendar time,
  but its display and life-stage milestones need rethinking for short lives.
  Night should occupy more of each cycle relative to daylight than in the
  earlier proposed split; its exact share is not decided.

### Current prototype evidence (not a finished-game decision)

The supported **integrated private-world host** schedules one world tick per
real second. Newly created worlds now save the accepted 360-tick day and
40-day year; the existing paused development save still carries its older
1,440-tick/365-day calendar. Neither pace is guaranteed under load. The
repository build dispatches hosted cognition outside the tick so a slow
provider does not hold unrelated agents or the clock; the VPS server has been
updated, but a resumed paired-client playtest remains. It gates ticks and provider calls on authenticated
client presence; the last disconnect closes that gate after about five
seconds, with no offline catch-up. The separate small fixture/kernel schedules
six ticks per second (**four real minutes per day**) and must **not** be
mistaken for the playable private-world pace. Older design documents propose
**2:40 daylight / 1:20 night** for that four-minute target. A source search
found no integrated sunrise/sunset or daylight/night cutoff, so that split is
not verified as current game behavior.

### Open

Playtest the accepted starting pace and revise it if days, seasons, or agent
lives feel rushed or slow. Still open: month/season names, the daylight/night
split, detailed stage effects, sunrise/sunset and seasonal variation, provider
work at pause/quit boundaries, safe routine activity for a stalled agent, and
how to communicate provider delays without freezing the world.

Provider cost is unknown until agent call rates, token use, model choices, and
population are measured. Representative tests can measure this before the
entire game is complete; nominal calendar speed alone does not determine
model spend. The usage meter and optional stop are accepted, but their
accounting details need design and playtesting.

## World generation, geography, and ecology

### Decided

- World-size presets are **Small, Medium, Large, Huge, Mega**. They should feel
  roughly like one town, one city/region, one major continent, two or three
  continents, and a planet respectively. The proposed logical dimensions are
  **256×128, 512×256, 1024×512, 2048×1024, 4096×2048**—initial benchmark
  targets, **not locked constants**.
- Use **64×64 logical tiles per chunk** as the initial organization target.
  Chunks help storage, loading, and rendering without forcing an entire chunk
  into one giant texture. Rendering only what the camera sees is distinct from
  simulating what happens elsewhere. Agents, crops, and settlements do not stop
  existing or progressing outside the camera. Distant work may be event-driven
  or coarser only if outcomes remain credible.
- A world may enable **east/west wrapping** with real northern and southern
  polar regions, or choose no wrapping. The default climate has a warmer
  equator and colder poles; an Advanced Setting can disable latitude cooling
  for unusual worlds. North/south wrapping into a torus is not intended.
- Climate choices include **uniform**, **dominant**, and **balanced** modes,
  with advanced controls such as water percentage, continent count, and
  resource abundance. Players need not manage raw rainfall/temperature sliders.
- **Simple regional weather is accepted.** Weather is not synchronized across
  the planet. Different regions can experience different conditions, with the
  local climate influencing how likely rain, snow, and other conditions are.
  Initial effects are visible clouds/rain/snow; rain affecting soil moisture
  and crops; cold, heat, and wetness making shelter/clothing useful without
  sudden lethal exposure; and heavy rain/snow mildly affecting outdoor work
  and travel. Severe storms, floods, disasters, and elaborate weather physics
  are not assumed for the first complete game.
- Model **climate zone**, **elevation**, **surface**, **hydrology**, **vegetation
  cover**, and **objects** separately. The generator makes plausible forests,
  cacti, grass, stone, snow, water depths, and transitions for their locations.
  A dense tree stand can be one resource-bearing object with art depicting
  several trees; cutting leaves a stump/trunk and regrowth can occur.
- Agents can reshape some terrain through activity. They can cross water with
  crafted boats and shore-connected ports and can later invent improvements.
  Roads exist and influence travel and building placement.

### Open

The complete terrain/vegetation/object catalogue; exact climate-generation
formulas, map topology at polar edges, biome transitions, water and elevation
rules, resource distributions, travel times, world-size performance, and limits
on agent-caused terrain changes. **Regional weather details remain open:**
how large/coherent weather regions are, how events move or change, how long
they last, and the exact strength/mechanics of the agreed initial effects.
Climate's long-run
rainfall/moisture is distinct from any individual rain event. The 64×64 chunk
and preset dimensions need
benchmarks before becoming implementation promises.

Regional weather can be updated by world systems rather than requiring an LLM
call for each weather change.

## Pixel-art assets and generated art

### Decided

- **Pixel art** is the visual style. **32×32-pixel ground tiles** and **PNG
  runtime assets** are the initial standard. Taller agents/trees and multi-tile
  buildings can use larger transparent images anchored to logical tiles.
- Most production textures will likely be created with AI help, including work
  with Clanker, then adapted to a coherent game style. Aseprite is an optional
  editor/source format, not a requirement for computment or for playing the game.
- Agent inventions can include generated art. An exported mod carries its
  approved assets with it; art must not become executable authority.
- Visual/content variety must be **bounded and supportable** across the game,
  not only for buildings. Do not imply that every theoretical combination of
  shape, material, style, state, and invention needs bespoke art or a bespoke
  simulation rule. This is a production constraint, not a ban on agents making
  genuinely new things.
- For the **first complete game**, each supported design can have **one
  standard appearance**. Two agents building the same size/type of house need
  not get different roof colors, decorations, or whole new sprites. Extra
  visual variants across buildings and other categories are optional work for
  after that complete game, not part of its required art workload.
- Agent inventions may introduce **genuinely new designs** rather than being
  permanently limited to the original art catalogue. Each new design still
  needs a valid visual/gameplay representation; this does not authorize every
  theoretical combination.

### Proposed, not yet settled

Use editable `.aseprite` or layered PNG sources where useful; export PNG
spritesheets/atlases plus metadata for asset ID, footprint, anchor, layer,
collision, frames/timing, style/biome, creator, rights, and version. Build a small
visual reference scene before locking a palette or mass-producing textures.
For autonomous inventions, first try approved component assembly/recoloring;
optionally request new AI imagery under player-controlled cost limits. Normalize
it to the pixel grid, check technical/provenance/performance rules, preview it,
and use a legible fallback sprite if generation fails. Aesthetically odd art
should not automatically erase a mechanically valid invention.

Use a small set of supported visual families for built-in content, then add a
new family when an invention truly needs a new silhouette. Reusing an image
need not mean two creations behave identically. The exact reuse/generation
method remains Clanker's proposal, not an accepted implementation rule.

### Open

Camera/art perspective; exact palette and style guide; animation standards;
AI-generation provider and spending controls; how much visual cleanup can be
automated; quality criteria; asset size budgets; and the final art/content
metadata contract. Also open: the minimum distinct designs each category
needs, and how an agent invention technically expands the supported visual
vocabulary. The first complete game does **not** require extra cosmetic
variants just for variety. Existing code has partial PNG validation, **not**
the full finished-game autonomous art pipeline.

## Audio and dialogue presentation

### Decided for the first complete game

- Agent conversations remain readable as text in their chat bubbles and
  expandable conversation view; generated voices are not required.
- **Audio is deliberately outside the first complete game's scope.** This is
  not only a resource constraint: computment does not expect sound in general
  to add much to ClankerWorld. They also have no resources to produce music or
  effects. Important information and interaction must remain fully
  understandable without sound.

There is no active audio plan. Music, environmental effects, or voices can be
revisited later only if the design case changes; none is on the required
first-complete-game roadmap.

## Starting world, agents, and family continuity

### Decided

- A civilization-oriented new world is generated with an **empty base camp**.
  Once inside the world, the player adds and configures **four biologically
  unrelated founder agents**, using the per-agent provider/model/credential
  flow. The simulation cannot begin until all four are added. They are grouped
  2+2 into two starter households, not forced couples; family lines develop
  later through relationships and children. The households exist even before
  any formal land claims. The generated camp contains two small houses, a
  shared storehouse, fire/cooking area, basic workshop, nearby fertile land,
  starter food/seeds/hand tools/clothing, and a connecting path. Furniture can
  be abstracted as inspectable building contents/capabilities rather than
  every chair being drawn.
- Once all four founders are configured and placed, the player explicitly
  presses **Start World**; the simulation must not begin automatically on the
  fourth placement. An incomplete founder setup is saved so the player can
  quit and finish it later. Before Start World, the game should clearly show
  progress toward the required four founders.
- Each agent independently has a chosen provider/model, private memory and
  context, goals/personality, call schedule, usage, and failure state. Different
  agents may use the same stored key. Agents choose their own personality,
  aspirations, skills, and initial identity. The player can add adults freely.
- Agents have no genders. Children have **two parents**. Parents choose the
  child's name and provider/model. Children inherit tendencies, abilities,
  culture, and provider/model settings; baby appearance uses baby art. Close
  biological relatives cannot pair.
- **A child uses their own selected personal LLM once they leave infancy.**
  Infants do not make calls to their personal model. The parents' provider/model
  choice can be stored at birth, then used when that agent enters the child
  stage. This resolves the earlier open question about *whether* children use
  their own models; initial age-up thresholds are below. Jev must not be
  required for infant care, because Jev is optional per world.
- **Children are real social agents, not silent placeholders.** Their personal
  models can converse, play, learn, form friendships, and choose age-appropriate
  simple helping tasks. Adult-only decisions such as land deals and parenthood
  wait until adulthood. The world system, not merely a model prompt, enforces
  those age-based permissions.
- **Current lifespan anchor:** an agent can live no more than **six hours of
  unpaused world time from birth**. They may die earlier. This is a maximum,
  not a promise that everyone dies at exactly six hours or that adding an
  already-adult agent grants six further hours. Detailed stage effects and
  variation in age at death remain open; pacing must be playtested.
- **Death and inheritance:** by default, a deceased agent's personal belongings
  pass to their household. Computment wants the deceased agent's own model to
  be explicitly told that the agent has died and then produce a final will or
  inheritance instruction **after death**. This is part of the intended first
  complete game, not a post-launch feature. It is a final estate decision, not
  the dead agent resuming ordinary physical actions. A valid will or later
  established inheritance rule can change the default household distribution.
- Deceased agents remain **inspectable to the player**, including through the
  interactive family tree. Their agent popup becomes a historical profile with
  their saved thoughts and memories, age/circumstances of death, and final will
  when available. It does not generate ongoing new thoughts or offer live
  provider/model controls; the exceptional post-death will turn above is not
  ordinary continued life. **Memories do not transfer to descendants at death.**
  Preserving them for player inspection does not make them known to living
  agents. A child learns only what they were told, taught, read, witnessed, or
  later discovered. For example, a hidden tool's location remains unknown to
  the child if the parent never shared or recorded it.
- Parenthood normally requires consent and is optional when civilization is
  secure. At low population, this world has an explicit **continuity rule**:
  refusal is not fully autonomous, preventing civilization from ending solely
  because models decline reproduction. The game must communicate that rule
  honestly rather than claiming unrestricted consent. A simple population
  threshold may be the first implementation, but the finished system should
  assess **continuity risk**—eligible unrelated adults, family lines, children,
  expected deaths, and care/resources—not just count heads.

### Open

Expected/variable lifespan below the six-hour cap, founders' starting ages,
the exact UI for assigning the first four founders to the two starter
households, the detailed limits on child tasks and elder capabilities, whether
older childhood needs a separate phase, age display, relationship and
inheritance mechanics, continuity threshold and exit conditions,
pregnancy/birth and childcare rules, care/resource eligibility, Jev's
optional role in childhood, and the identity implications of player renaming.
The optional survival grace period still needs a duration and
precise effects. Also open: whether unrelated newcomers can arrive without
player action or are only introduced through Add Agent. A configurable
automatic-birth limit was considered, briefly accepted, then explicitly
reopened: computment will decide **after playtesting actual birth frequency,
population growth, and model cost** whether a cap belongs in the game. Do not
assume a cap or its precedence over the continuity safeguard before that
decision.
Computment briefly considered removing the close-relative pairing ban, then
retracted that thought; the ban still stands. A model-usage meter and optional
AI-usage limit have since been accepted; neither is a population cap.

For inheritance, still open: the exact final-model-turn contract; which assets
are personal versus already household-owned; conflicts between a will and
agent-made law; minors, multiple heirs, debts, and no-household cases; and
whether a final message beyond the will is part of the death event. Clanker's
proposed safety rule is to freeze the estate at death, validate the model's
instructions against real ownership/law, and apply the household default if
the model is unavailable or gives no valid instruction. Computment said this
flow feels right; treat it as accepted direction, while timeouts, conflicts,
crash recovery, and exact transfer rules still need design.

### Accepted age-up chart for playtesting

Using the current six-real-minute day and 60-world-day maximum lifespan:

| Stage | World age | Unpaused time since birth | Own LLM? |
|---|---:|---:|---|
| Infant | day 0 to before day 3 | 0–18 minutes | No |
| Child | day 3 to before day 15 | 18–90 minutes | Yes, age-appropriate options |
| Adult | day 15 to before day 45 | 1½–4½ hours | Yes |
| Elder | day 45 to before day 60 | 4½–6 hours | Yes |

An agent may die earlier; these are stage thresholds, not scheduled death
times, and the thresholds can change after playtesting. An adult added to the
world starts at a nonzero age. Because adulthood precedes the first 40-day
birthday, Clanker recommends displaying age in world days plus life stage,
not only whole years; this UI choice is not yet settled. A separate adolescent
stage and exact capabilities are still open, but are not assumed to require
another sprite family at first.

## Social life and agent cognition

### Decided or accepted for playtesting

- Early conversation is face-to-face and proximity-bound; agents may later
  invent long-distance communication. Agents should chat **regularly in
  context** without socializing to the exclusion of work and life.
- Personal models generate meaningful dialogue and decisions about trade,
  buying/selling, organizing, land, invention, relationships, and other goals.
  Agents can lie, misunderstand, gossip, keep secrets, and have differing
  knowledge. **Private thoughts and spoken dialogue are distinct:** another
  agent learns something only if it is said, observed, or otherwise conveyed
  in-world; inspecting a thought as the player does not broadcast it to anyone.
- A conversation is a real joint activity with a reason, turns, and a chance to
  conclude, disagree, withdraw, or postpone. Ordinary job scheduling should
  not cut it off mid-sentence. Danger or urgent needs may interrupt; a bounded
  final wrap-up round can avoid endless looping. If unresolved, say so rather
  than fabricate agreement, and allow later resumption.
- Socializing agents display a chat bubble above their sprites. Clicking opens
  a nearby popup with a summary; expanding reveals the complete conversation.
- Jev may notice social moments, retrieve memories, summarize, or route cheap
  routine decisions while each agent retains its personal LLM. Jev is optional
  per world, not individually toggled per agent. The player can **turn Jev on
  or off in an existing world**; the choice is not locked at world creation.
  Disabling it must not erase existing agent memories or make the world depend
  on Jev to continue functioning.
- Each agent remembers what they experienced or were told, not everything the
  player can see. Minor details may fade or be misremembered; major life
  events, relationships, learned skills, and unresolved commitments should
  remain dependable. An agent's belief can be wrong without changing the
  simulation's record of what actually happened. Neither family relation nor
  another agent's death grants access to that person's private memories.
- **Preferred memory architecture:** when enabled, Jev can help compact many
  experiences into shorter memories and retrieve relevant ones for that
  agent's next decision. It must preserve who witnessed or said something,
  distinguish firsthand events from rumors and uncertain beliefs, and never
  expose one agent's private memories to another. Jev does not replace the
  agent's personal model or become mandatory for memory to work. With Jev off,
  the game still needs a functioning memory/retrieval path.
- Fully generated languages/dialects are **deferred**, not planned now, due to
  uncertain gameplay value and token cost.

### Open

Conversation frequency and token/turn budgets, group-planning mechanics,
interrupt/resume behavior, memory importance/retention rules, compaction
triggers and fallback method without Jev, and Jev's exact role
need implementation and playtesting. The proposed lifecycle is accepted as a
direction, not proof that it will feel right in the finished game.

## Buildings, land, settlements, and animals

### Decided

- Each building **type has a few supported shapes of its own**, rather than
  choosing freely from every shape/material/floor/state combination. There is
  initially **one standard appearance per supported design**; duplicate houses
  of the same type and size may look alike. **3×3** remains a sensible early
  upper-size candidate, but the exact catalogue has not been chosen. Agents
  may invent new designs, including unusual shapes if the art and world rules
  can represent them; larger, irregular, or multi-floor buildings are therefore
  possibilities, not automatic unlocks. Homes have households, storage,
  sleeping/comfort capacity, and inspectable occupants and
  contents. An entire household may shelter together even if crowded, but
  overcrowding has consequences rather than making house size meaningless.
  Guests may request permission to stay.
- Non-residential buildings have distinct physical occupancy, workstation,
  storage, and other type-specific limits. Agents can reserve space when
  practical, queue or choose alternatives when full, and retain the blocked
  goal for later retry. The simulation owns that memory; Jev can help choose an
  alternative but is not responsible for remembering the task.
- A **workshop** holds tools/materials and supports crafting, repair,
  prototypes, machines, and specialized production. Farms include outdoor field
  work zones and separate occupiable structures such as farmhouse, barn, or
  silo. Warehouses, markets, town halls, and other buildings need distinct
  useful functions.
- Agents choose building sites from understandable legal options considering
  access, terrain, resources, ownership, and settlement context. Land can first
  be claimed, shared, granted, or disputed. Monetary land values and purchase
  prices become meaningful after currencies exist. Agents can later buy/sell
  property. Roads help travel and influence site choice.
- Agents formally establish settlements and borders. Village/town/city classes
  are population-based. The player can inspect actual property and settlement
  boundaries through map filters.
- **Livestock and mounts** belong in the finished game. Hostile predators do
  **not** belong in the current plan, though they may be revisited later.

### Open

Structure catalogue and effects; exact permitted configurations, footprint,
floor, access, reservation, and queue rules; comfortable-capacity numbers;
land-claim and dispute law;
mayors and settlement governance; currency/land pricing; village/town/city
thresholds; transport progression; livestock uses and care; and whether other
non-hostile wildlife is wanted.

## Settlement laws and governance

### Decided direction

- Agents may establish settlement laws and later change them. These are
  **in-world social rules**, not unbreakable physics. An agent can violate a
  rule—for example, cut a tree in a protected grove—and the world can record
  the act for discovery, dispute, and consequences. The game should not simply
  refuse every illegal action, because crime and enforcement belong to the
  simulation.
- The authoritative simulation still protects fixed world facts and action
  rules: agents cannot create goods, erase physical constraints, or silently
  rewrite formal ownership by declaring a new law. Unlawful use or occupation
  can be represented as an action/dispute without automatically changing the
  underlying ownership record. Laws may guide or contest transfers through
  validated mechanisms, but cannot bypass those mechanisms.
- A newly founded settlement starts with a **simple council** rather than a
  single founder automatically ruling everyone. **Every adult resident** sits
  on this initial council and can propose laws and vote. Settlements may later
  change their governing arrangement through in-world decisions; the council
  is the starting form, not a universal permanent government.

### Preferred starting election trigger

Once a settlement reaches **eight adult residents**—double the four-founder
starting population—it begins electing a smaller representative council
instead of keeping every adult as a council member. Count adults in that
settlement, not across the world. Eight is an initial threshold to playtest,
not a claim that every settlement must forever follow this exact rule. All
adult residents should retain a vote in those elections.

### Open

What qualifies an adult as a settlement resident, elected council size,
election timing/terms, vote threshold/quorum, proposal/repeal procedure, and
how governments may change; which laws apply to whom and where; how a
violation is witnessed, investigated, enforced, or punished; claims and land
disputes;
taxes, inheritance, and interaction between conflicting settlements. Agents
should know only laws or violations they have learned about in-world.

## Combat

### Decided

- Interpersonal combat belongs in the finished game for **self-defense, crime,
  personal feuds, and organized war**. Hunting and sport/duels were not chosen
  in that discussion.
- Combat can turn lethal, but agents should not be dying constantly. Personal
  models choose intentions; authoritative simulation resolves reach, movement,
  timing, protection, injury, and outcomes. Health already exists in the
  prototype, but the integrated game **does not yet have a combat loop,
  weapons, armor, combat injuries, or combat AI**.

### Preferred, not finalized

Weapons and armor should fit the general item/equipment/crafting/invention
system rather than be a disconnected special system. An axe could be both a
work tool and a weapon; armor could be wearable protective equipment. Combat
still needs its own actions and balancing. Computment's agreement here was
qualified (“if I get what you mean”).

### Open

Injuries, medicine, escape/surrender, law enforcement, war declarations and
peace, lethality balance, combat equipment progression, and whether animal
slaughter or hunting ever becomes part of the game.

## Inventions, mods, and technology

### Decided

- Agents can eventually create buildings, tools, crops, machines, art, laws,
  currencies, and other economic/cultural systems. They cannot invent or alter
  natural biomes. Needs, curiosity, aspirations, social standing, requests,
  and expertise may motivate invention. Agents may use, teach, hide, sell,
  improve, or compete with creations.
- An invention's novel behavior does not automatically imply a limitless new
  shape/material/animation combination. Its art must fit a supported visual
  configuration or pass a new-design path. Most inventions should combine
  familiar world actions—such as storing, crafting, cooking, transporting, or
  protecting—in new ways. Truly new classes of behavior are possible but rarer
  and require extra game-system support. The exact boundary remains open.
- The design direction combines **declarative content** with **sandboxed
  executable scripts**. Valid inventions can activate in the running world at
  a safe boundary. Agent-generated code must not run as arbitrary trusted code
  in the main game process, access credentials/files/network freely, or change
  protected physical and ownership laws. A test/validation stage, runtime
  limits, quarantine/rollback, failure feedback, and cooldown are part of the
  accepted direction.
- An **invention** originates in a particular world's fiction. A **mod** is a
  portable technical package. Inventions and active mods belong to their
  world save; the player may explicitly export an invention into a personal
  library and import it into another world. Nothing transfers silently.
- Human-authored or community-made mods can also be **explicitly imported** in
  the first complete game. They use the same package validation, content
  permissions, and restricted script sandbox as agent-made creations; there
  is no separate trusted-code shortcut for downloaded mods. A package can
  declare content and assets and, where supported, bounded scripted behavior.
  Import is a deliberate player action into a chosen world, not an automatic
  download or silent global installation. The exact file format and package
  user interface remain open.
- Both the Pause Menu and Main Menu have a **Mod Library** surface. The in-world
  view shows active/developing/failed creations, authors, and dependencies. The
  global view browses the latest save for each world and handles the personal
  library and imports/exports. Tabs such as This World, Personal Library,
  Import/Export, and History are accepted as an initial organization.
- The player wants **discovered capabilities inspectable** through World Info.
  This must not falsely imply that every agent knows every discovery.

### Preferred, not finalized

Use a **capability/prerequisite graph**, not a rigid visible technology tree or
unrestricted “invent a car out of sticks” system. Computment likes this
recommendation, but did not explicitly answer the separate final yes/no
question. The graph should show discoveries, not spoil a fixed future list.

### Open

Exact sandbox/runtime and APIs; immutable world-law boundary; validation and
activation/rollback semantics; art generation/cost; model invention prompts;
materials/prerequisite logic; who knows which capabilities; package
compatibility/dependencies/rights; external-mod import UX; and how the
library handles revisions and conflicting imports.

## Saves and persistence

### Decided

- Every world has its own save state. **Autosave is on by default**, initially
  every **5 minutes**, with **5 rotating autosaves** by default. Settings offer
  autosave off/on, intervals of 1, 2, 5, 10, 15, or 30 minutes, and rotation
  off/3/5/10. These are initial choices subject to performance playtesting;
  their proposed home is the current world's **World Settings**.
- Named manual saves are unlimited. Emergency recovery is automatic in the
  background. Saving on Quit to Menu/Game is the accepted direction, while
  quit confirmation remains.
- World-created inventions and active mods travel with that world's save;
  provider credentials and graphical/device settings are global. The global
  library reads the latest save for each world rather than silently merging
  their creations.

### Open

Whether manual saves are checkpoints or divergent branches; restore/rewind
behavior; disk-space warnings and retention; exactly when autosave occurs
relative to model/conversation work; crash-recovery guarantees; and save
compatibility across game/mod versions.

## Design tensions and next architecture decisions

These are **open questions**, not changes to the decisions above:

1. **Population continuity and ancestry.** Four unrelated founders in two
   starter households, a close-kin pairing ban, and a low-population safeguard may
   eventually leave no eligible unrelated adults even if everyone wants
   children. Computment now prefers player-controlled addition of unrelated
   adults, with placement seeding household/settlement membership. Decide
   explicitly whether those additions are the **only** source of unrelated
   newcomers or whether autonomous arrivals also exist; define exactly how
   close is too close. Whether automatic births need a configurable limit is
   deferred until measured playtesting, rather than part of the current design.
2. **Law-making and enforcement details.** The core distinction is settled:
   agent-created laws can be broken, while the simulation protects physical
   facts and validated ownership changes. Define how laws are adopted,
   discovered, enforced, and disputed—including conflicting inheritance rules
   and illegal occupation—without granting models authority over engine facts.
3. **Pending model work versus continuous simulation/saves.** One slow model
   must not freeze the world, but conversations, inventions, and post-death
   wills can remain unresolved while ticks and autosaves continue. Quit now
   has an accepted cancel/discard-and-reconsider rule; still define durable
   pending states, autosave/manual-pause behavior, deadlines/fallbacks, and
   idempotent completion so crashes cannot duplicate or lose estate transfers
   or other important actions.
4. **No fictional population cap versus finite provider cost.** Each agent's
   personal model is central, including child agents; frequent socializing
   and unlimited player-added adults increase call volume. Measure calls,
   tokens, latency, and cost in representative play before choosing decision
   cadence, budgets, and how resource pressure is shown to the player. This is
   a scalability constraint, not permission to replace agents with Jev.
5. **Local packaging and VPS parity.** The finished distribution target is
   player-local and Windows-only at first, while development stays VPS-backed.
   Prove the same simulation and save behavior in both; choose the local
   process/installer shape without creating a second, divergent game.

Other substantive open topics: normal-session/player-intervention boundaries;
memory and knowledge across generations; settlement government, economy,
medicine and injuries; art reference/style and autonomous invention assets;
save branches and mod compatibility. Resolve the finished-game experience
before choosing an implementation sequence or narrowing it to the current
prototype.
