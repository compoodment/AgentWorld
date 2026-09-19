using System.Net;
using AgentWorld.GodotClient.Pairing;

namespace AgentWorld.Simulation.Tests;

public sealed class GodotOwnerPairingClientTests
{
    [Fact]
    public async Task PairingRefusesPlaintextNonLoopbackTransportBeforeItCanSendAKey()
    {
        using var client = new HttpClient(new FailingHandler());
        var pairingClient = new OwnerPairingClient(client);
        using var signer = new StubSigner();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => pairingClient.StartPairingAsync(
            new Uri("http://example.test:5188"),
            signer,
            CancellationToken.None));

        Assert.Contains("requires HTTPS", exception.Message, StringComparison.Ordinal);
    }

    private sealed class StubSigner : IOwnerDeviceSigner
    {
        public string PublicKeySpkiBase64 => "not-used-before-transport-validation";

        public string PublicKeyFingerprint => "not-used-before-transport-validation";

        public string SignCanonicalProof(string canonicalProof) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new Xunit.Sdk.XunitException("Transport must not be reached for an insecure remote URI.");
    }
}
