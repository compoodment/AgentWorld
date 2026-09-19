async function loadDiscovery() {
  const status = document.querySelector("#connection-status");
  const details = document.querySelector("#protocol-details");
  try {
    const response = await fetch("/api/v1/handshake", { headers: { Accept: "application/json" } });
    if (!response.ok) throw new Error("handshake returned " + response.status);
    const handshake = await response.json();
    const values = [
      ["protocol", handshake.protocol.major + "." + handshake.protocol.minor],
      ["discovery capability", handshake.serverCapabilities.join(", ") || "none"],
      ["world observation", "paired owner device only"],
    ];
    details.replaceChildren(...values.flatMap(([name, value]) => {
      const term = document.createElement("dt"); term.textContent = name;
      const detail = document.createElement("dd"); detail.textContent = value;
      return [term, detail];
    }));
    status.textContent = "Protocol discovery available · paired observation required";
    status.classList.remove("error");
  } catch (error) {
    status.textContent = "Unable to load protocol discovery: " + error.message;
    status.classList.add("error");
  }
}

loadDiscovery();
