using System.Net;
using System.Text.Json;
using AgentWorld.Simulation.Cognition;
using AgentWorld.Simulation.Harness;

namespace AgentWorld.Simulation.Tests;

public sealed class PhaseThreeCognitionTests
{
    [Fact]
    public async Task DeterministicCognitionChoosesAndExecutesARealMovementIntention()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");

        var result = await runtime.AdvanceOneActionAsync();
        var snapshot = runtime.Capture().Snapshot;

        Assert.True(result.Advanced);
        Assert.False(result.PausedForProviderOutage);
        Assert.False(result.Cognition?.FellBack);
        Assert.Equal("seek_food", result.CandidateId);
        Assert.Equal("seek_food", snapshot.Cognition?.CurrentIntention?.CandidateId);
        Assert.NotEqual(new GridPoint(0, 0), snapshot.World.Actor.Position);
        Assert.Contains(snapshot.Cognition!.Events, worldEvent => worldEvent.Kind == "cognition_requested");
        Assert.Contains(snapshot.Cognition.Events, worldEvent => worldEvent.Kind == "cognition_decision_applied");
        Assert.Equal("moved", result.MovementEvents.Single().Kind);
    }

    [Fact]
    public async Task CognitionAndWorldStateSurviveAValidatedRestart()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha");
        _ = await runtime.AdvanceOneActionAsync();
        var state = JsonSerializer.Deserialize<PhaseTwoWorldRuntimeState>(
            JsonSerializer.Serialize(runtime.ExportState())) ?? throw new InvalidDataException();

        var restored = PhaseTwoWorldRuntime.Restore(state, "camp-alpha");

        Assert.Equal(
            runtime.Capture().Snapshot.Cognition?.CurrentIntention,
            restored.Capture().Snapshot.Cognition?.CurrentIntention);
        Assert.Equal(
            runtime.Capture().Snapshot.World.Actor,
            restored.Capture().Snapshot.World.Actor);
        Assert.Equal(
            runtime.Capture().Snapshot.World.Events,
            restored.Capture().Snapshot.World.Events);
        Assert.Equal(
            runtime.Capture().Snapshot.World.Map.ManifestDigest,
            restored.Capture().Snapshot.World.Map.ManifestDigest);
        Assert.True((await restored.AdvanceOneActionAsync()).Advanced);
        Assert.Equal(2, restored.Capture().Snapshot.World.Identity.WorldTick);
    }

    [Fact]
    public async Task RepeatedHostedProviderFailureFallsBackThenPausesTheWorld()
    {
        var runtime = new PhaseTwoWorldRuntime("camp-alpha", decisionProvider: new ThrowingProvider());

        var first = await runtime.AdvanceOneActionAsync();
        var second = await runtime.AdvanceOneActionAsync();
        var third = await runtime.AdvanceOneActionAsync();

        Assert.True(first.Advanced);
        Assert.True(first.Cognition?.FellBack);
        Assert.True(second.Advanced);
        Assert.True(third.Advanced);
        Assert.True(third.PausedForProviderOutage);
        Assert.True(runtime.Capture().Snapshot.IsPaused);
        Assert.Contains(runtime.Capture().Events, worldEvent => worldEvent.Kind == "provider_outage_paused");
        Assert.False((await runtime.AdvanceOneActionAsync()).Advanced);
    }

    [Fact]
    public async Task JevAdapterSendsOnlyTheCompactChoiceContractAndRecordsUsage()
    {
        var handler = new RecordingHandler(JsonResponse());
        using var client = new HttpClient(handler);
        var provider = new JevDecisionProvider(
            client,
            () => "test-secret",
            new Uri("https://typesafe.test/v1/systemone"));
        var observation = new InhabitantObservation(
            "actor-scout",
            4,
            0,
            2,
            "sha256:observation-4",
            2_000,
            7_000,
            [
                new CognitionCandidate("safe_idle", "Continue safely.", 0),
                new CognitionCandidate("seek_food", "Travel to food.", 10, "berry-patch"),
            ]);
        var request = new CognitionDecisionRequest("cognition-test", 1, observation);

        var response = await provider.DecideAsync(request);
        using var body = JsonDocument.Parse(handler.Body ?? throw new InvalidDataException());

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("Bearer test-secret", handler.Authorization);
        Assert.Equal("jev-1.13.0", body.RootElement.GetProperty("model").GetString());
        var question = body.RootElement.GetProperty("questions").GetProperty("selected_candidate");
        Assert.Equal("choice", question.GetProperty("type").GetString());
        Assert.Equal("seek_food", response.SelectedCandidateId);
        Assert.Equal(0.84, response.Confidence);
        Assert.Equal("jev-1.13.0", response.Usage?.ModelId);
        Assert.Equal(123, response.Usage?.InputTokens);
        Assert.Equal(7, response.Usage?.OutputTokens);
    }

    private static string JsonResponse() =>
        """
        {
          "model": "jev-1.13.0",
          "answers": {
            "selected_candidate": {
              "type": "choice",
              "choice": "seek_food",
              "probabilities": { "safe_idle": 0.16, "seek_food": 0.84 },
              "confidence": 0.84
            }
          },
          "usage": { "input_tokens": 123, "output_tokens": 7 }
        }
        """;

    private sealed class ThrowingProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Jev;

        public long ProviderEpoch => 1;

        public ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("provider unavailable");
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string? Authorization { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody),
            };
        }
    }
}
