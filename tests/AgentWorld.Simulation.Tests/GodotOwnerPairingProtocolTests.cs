using AgentWorld.GodotClient.Pairing;
using AgentWorld.Viewer.Control;

namespace AgentWorld.Simulation.Tests;

public sealed class GodotOwnerPairingProtocolTests
{
    [Fact]
    public void SignedActionBindingMatchesTheServerByteForByte()
    {
        const string method = "POST";
        const string path = "/api/v1/owner/reconnect";
        const string requestId = "request_Åß-test";
        const string payload = "agentworld.owner-reconnect.v1\nafter-event-id=42";

        var godotBinding = OwnerPairingProtocol.CreateHttpActionBinding(
            method,
            path,
            requestId,
            payload);
        var serverBinding = OwnerHttpBinding.Create(method, path, requestId, payload);

        Assert.Equal(serverBinding, godotBinding);
    }
}
