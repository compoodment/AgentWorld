let afterEventId = 0;
let eventHistory = [];
let isLoading = false;

async function fetchJson(url) {
  const response = await fetch(url, { headers: { Accept: "application/json" } });
  if (!response.ok) throw new Error(url + " returned " + response.status);
  return response.json();
}

function makeMarker(symbol, className, label) {
  const marker = document.createElement("span");
  marker.className = "marker " + className;
  marker.textContent = symbol;
  marker.title = label;
  marker.setAttribute("aria-label", label);
  return marker;
}

function renderMap(snapshot) {
  const map = document.querySelector("#world-map");
  map.replaceChildren();
  const width = Math.max(...snapshot.tiles.map(tile => tile.x)) + 1;
  map.style.gridTemplateColumns = "repeat(" + width + ", 1fr)";
  const objectsByPosition = new Map(snapshot.objects.map(item => [item.position.x + "," + item.position.y, item]));
  const resourcesByPosition = new Map(snapshot.resources.map(item => [item.position.x + "," + item.position.y, item]));
  const actorPosition = snapshot.actor.position.x + "," + snapshot.actor.position.y;

  snapshot.tiles.forEach(tile => {
    const cell = document.createElement("div");
    const key = tile.x + "," + tile.y;
    cell.className = "tile terrain-" + tile.terrain;
    cell.setAttribute("aria-label", tile.terrain + " at " + key);
    if (objectsByPosition.has(key)) {
      const item = objectsByPosition.get(key);
      cell.append(makeMarker("◆", "marker-object", item.kind + ": " + item.id));
    }
    if (resourcesByPosition.has(key)) {
      const item = resourcesByPosition.get(key);
      cell.append(makeMarker(item.kind === "food" ? "●" : "▲", "marker-food", item.id + ": " + item.state));
    }
    if (key === actorPosition) cell.append(makeMarker("✦", "marker-actor", "actor: " + snapshot.actor.id));
    map.append(cell);
  });
}

function renderActor(actor) {
  const values = [
    ["ID", actor.id], ["position", actor.position.x + ", " + actor.position.y],
    ["hunger", actor.hungerBasisPoints + " bp"], ["energy", actor.energyBasisPoints + " bp"],
    ["food", actor.foodItems], ["wood", actor.woodItems],
  ];
  document.querySelector("#actor-details").replaceChildren(...values.flatMap(([name, value]) => {
    const term = document.createElement("dt"); term.textContent = name;
    const detail = document.createElement("dd"); detail.textContent = value;
    return [term, detail];
  }));
}

function renderResources(resources) {
  const list = document.querySelector("#resource-list");
  list.replaceChildren(...resources.map(resource => {
    const item = document.createElement("li");
    const name = document.createElement("span"); name.textContent = resource.id;
    const state = document.createElement("small"); state.textContent = resource.state;
    item.append(name, state);
    return item;
  }));
}

function renderEvents(events) {
  const log = document.querySelector("#event-log");
  log.replaceChildren(...events.map(event => {
    const item = document.createElement("li");
    const time = document.createElement("time"); time.textContent = "#" + event.eventId + " · tick " + event.worldTick;
    const detail = document.createElement("span"); detail.textContent = event.kind + ": " + event.detail;
    item.append(time, detail);
    return item;
  }));
}

async function load() {
  if (isLoading) return;
  isLoading = true;
  const status = document.querySelector("#connection-status");
  try {
    const [handshake, baseline] = await Promise.all([
      fetchJson("/api/v1/handshake"),
      fetchJson("/api/v1/reconnect?afterEventId=" + afterEventId),
    ]);
    const snapshot = baseline.snapshot;
    const eventSlice = baseline.events;
    eventHistory = eventHistory.concat(eventSlice.events);
    afterEventId = snapshot.latestEventId;
    renderMap(snapshot);
    renderActor(snapshot.actor);
    renderResources(snapshot.resources);
    renderEvents(eventHistory);
    status.textContent = "Protocol " + handshake.protocol.major + "." + handshake.protocol.minor + " · tick " + snapshot.worldTick + " · read-only";
    status.classList.remove("error");
  } catch (error) {
    status.textContent = "Unable to load observation: " + error.message;
    status.classList.add("error");
  } finally {
    isLoading = false;
  }
}

load();
setInterval(load, 1000);
