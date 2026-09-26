using System.Net;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Viewer.Control;

namespace ClankerWorld.Simulation.Tests;

public sealed class ProviderConfigurationStoreTests
{
    [Fact]
    public void InhabitantInventionUsesPlanningProvider()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-invention-routing-");
        try
        {
            var store = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"), EmptySeed());
            _ = store.Configure(new("routine", "jev", "jev-test", "routine-test-secret", false));
            _ = store.Configure(new("planning", "ollama-cloud", "planning-test", "planning-test-secret", false));
            var router = new ConfigurableDecisionProvider(store, new FixedHttpClientFactory(new ProviderResponseHandler()));
            var observation = Request(router.ProviderEpoch).Observation with
            {
                Candidates = [new("invent:building:shelter", "Propose a shelter.", 35), new("safe_idle", "Wait safely.", 100)],
            };
            Assert.Equal(DecisionProviderKind.LargeLanguageModel, router.KindFor(observation));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("wear_clothing")]
    [InlineData("tend_fire")]
    [InlineData("seek_warmth")]
    [InlineData("care:dependent-child")]
    public async Task ExposureActionsUseRoutineProviderInsteadOfPlanning(string candidateId)
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-survival-routing-");
        try
        {
            var store = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"), EmptySeed());
            _ = store.Configure(new("routine", "jev", "jev-test", "routine-test-secret", false));
            _ = store.Configure(new("planning", "ollama-cloud", "planning-test", "planning-test-secret", false));
            var handler = new ProviderResponseHandler();
            var router = new ConfigurableDecisionProvider(store, new FixedHttpClientFactory(handler));
            var request = Request(router.ProviderEpoch);
            request = request with
            {
                Observation = request.Observation with
                {
                    Candidates = [new(candidateId, "Survive exposure.", 20), new("safe_idle", "Wait safely.", 100)],
                },
            };
            Assert.Equal(DecisionProviderKind.Jev, router.KindFor(request.Observation));
            var response = await router.DecideAsync(request);
            Assert.Equal(DecisionProviderKind.Jev, response.Provider);
            Assert.Equal("api.typesafe.ai", handler.LastUri!.Host);
            Assert.Equal("routine", response.Usage!.Role);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PersonalAssignmentsRouteIndependentlySurviveRestartAndCanInheritAgain()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-assignment-");
        try
        {
            var path = Path.Combine(directory.FullName, "providers.json");
            var store = new ProviderConfigurationStore(path, EmptySeed());
            _ = store.Configure(new("planning", "ollama-cloud", "world-model", "cloud-test-secret", false));
            _ = store.Configure(new("planning", "openai", "personal-model", "openai-test-secret", false, "inhabitant-test"));
            Assert.Equal("ollama-cloud", store.CaptureStatus().PlanningProvider);
            Assert.Single(store.CaptureStatus().Assignments!);
            Assert.DoesNotContain("test-secret", System.Text.Json.JsonSerializer.Serialize(store.CaptureStatus()), StringComparison.Ordinal);

            store = new ProviderConfigurationStore(path, EmptySeed());
            var handler = new ProviderResponseHandler();
            var router = new ConfigurableDecisionProvider(store, new FixedHttpClientFactory(handler));
            var response = await router.DecideAsync(Request(router.ProviderEpoch, strategic: true));
            Assert.Equal("api.openai.com", handler.LastUri!.Host);
            Assert.Equal("personal-model", handler.LastModel);
            Assert.Equal("openai", response.Usage!.ProviderId);
            Assert.Equal("planning", response.Usage.Role);
            Assert.True(response.Usage.LatencyMilliseconds >= 0);
            var other = Request(router.ProviderEpoch, strategic: true);
            _ = await router.DecideAsync(other with { Observation = other.Observation with { InhabitantId = "other" } });
            Assert.Equal("ollama.com", handler.LastUri!.Host);
            Assert.Equal("world-model", handler.LastModel);

            _ = store.Configure(new("planning", "inherit", null, null, false, "inhabitant-test"));
            Assert.Empty(store.CaptureStatus().Assignments!);
            _ = await router.DecideAsync(Request(router.ProviderEpoch, strategic: true));
            Assert.Equal("ollama.com", handler.LastUri!.Host);

            _ = store.Configure(new("planning", "openai", "personal-model", null, false, "inhabitant-test"));
            _ = store.Configure(new("planning", "openai", null, null, true));
            Assert.Empty(store.CaptureStatus().Assignments!);
            Assert.Equal("ollama-cloud", store.CaptureStatus().PlanningProvider);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void AssignmentTargetIsBoundByBothClientAndServerSignatures()
    {
        var action = new OwnerProviderConfigurationAction("planning", "deterministic", null, null, false, "mira");
        var clientAction = new ClankerWorld.GodotClient.UI.OwnerProviderConfigurationAction("planning", "deterministic", null, null, false, "mira");
        Assert.Equal(OwnerHttpBinding.ProviderConfigurationPayload(action),
            ClankerWorld.GodotClient.UI.OwnerWorldActionPayload.ProviderConfiguration(clientAction));
        Assert.NotEqual(OwnerHttpBinding.ProviderConfigurationPayload(action),
            OwnerHttpBinding.ProviderConfigurationPayload(action with { InhabitantId = "rowan" }));
        Assert.NotEqual(OwnerHttpBinding.ProviderConfigurationPayload(action),
            OwnerHttpBinding.ProviderConfigurationPayload(action with { InhabitantId = null }));
    }

    [Fact]
    public async Task PlayerCanSwitchAmongEverySupportedProviderAndForgettingAnActiveKeyFallsBack()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"clankerworld-provider-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = System.IO.Path.Combine(directory, "providers.json");
            var store = new ProviderConfigurationStore(path, EmptySeed());
            var handler = new ProviderResponseHandler();
            var logger = new RecordingLogger<ConfigurableDecisionProvider>();
            var router = new ConfigurableDecisionProvider(store, new FixedHttpClientFactory(handler), logger);

            Assert.Equal("deterministic", store.CaptureStatus().RoutineProvider);
            Assert.Equal("deterministic", store.CaptureStatus().PlanningProvider);
            Assert.Equal(DecisionProviderKind.Deterministic, router.Kind);

            var jevStatus = store.Configure(new OwnerProviderConfigurationAction(
                "routine",
                "jev",
                "jev-test",
                "jev-secret-123",
                false));
            var jevResponse = await router.DecideAsync(Request(store.CaptureRuntimeConfiguration().Revision));
            Assert.Equal("jev", jevStatus.RoutineProvider);
            Assert.Equal(DecisionProviderKind.Jev, jevResponse.Provider);
            Assert.Equal(new Uri("https://api.typesafe.ai/v1/systemone"), handler.LastUri);
            Assert.Equal("Bearer jev-secret-123", handler.LastAuthorization);

            var openAiStatus = store.Configure(new OwnerProviderConfigurationAction(
                "planning",
                "openai",
                "openai-test",
                "openai-secret-123",
                false));
            var openAiResponse = await router.DecideAsync(Request(store.CaptureRuntimeConfiguration().Revision, strategic: true));
            Assert.Equal("jev", openAiStatus.RoutineProvider);
            Assert.Equal("openai", openAiStatus.PlanningProvider);
            Assert.Equal(DecisionProviderKind.Jev, router.KindFor(RequestObservation(strategic: false)));
            Assert.Equal(DecisionProviderKind.LargeLanguageModel, router.KindFor(RequestObservation(strategic: true)));
            Assert.Equal(DecisionProviderKind.LargeLanguageModel, openAiResponse.Provider);
            Assert.Equal(new Uri("https://api.openai.com/v1/chat/completions"), handler.LastUri);
            Assert.Equal("Bearer openai-secret-123", handler.LastAuthorization);

            var ollamaStatus = store.Configure(new OwnerProviderConfigurationAction(
                "planning",
                "ollama-cloud",
                "ollama-test:cloud",
                "ollama-secret-123",
                false));
            var ollamaResponse = await router.DecideAsync(Request(store.CaptureRuntimeConfiguration().Revision, strategic: true));
            Assert.Equal("jev", ollamaStatus.RoutineProvider);
            Assert.Equal("ollama-cloud", ollamaStatus.PlanningProvider);
            Assert.Equal(DecisionProviderKind.LargeLanguageModel, ollamaResponse.Provider);
            Assert.Equal(new Uri("https://ollama.com/v1/chat/completions"), handler.LastUri);
            Assert.Equal("Bearer ollama-secret-123", handler.LastAuthorization);
            Assert.Contains(logger.Messages, message =>
                message.Contains("cognition_provider_call status=completed", StringComparison.Ordinal) &&
                message.Contains("provider=ollama-cloud", StringComparison.Ordinal) &&
                message.Contains("role=planning", StringComparison.Ordinal) &&
                message.Contains("candidate=safe_idle", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message =>
                message.Contains("jev-secret-123", StringComparison.Ordinal) ||
                message.Contains("openai-secret-123", StringComparison.Ordinal) ||
                message.Contains("ollama-secret-123", StringComparison.Ordinal));

            var forgotten = store.Configure(new OwnerProviderConfigurationAction(
                "planning",
                "ollama-cloud",
                "ollama-test:cloud",
                null,
                true));
            Assert.Equal("jev", forgotten.RoutineProvider);
            Assert.Equal("deterministic", forgotten.PlanningProvider);
            Assert.False(forgotten.Providers.Single(item => item.Provider == "ollama-cloud").HasCredential);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void StatePersistsButStatusNeverReturnsSecretsAndExistingStateWinsOverEnvironmentSeed()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"clankerworld-provider-restart-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        const string secret = "persisted-player-secret";
        try
        {
            var path = System.IO.Path.Combine(directory, "providers.json");
            var first = new ProviderConfigurationStore(path, EmptySeed());
            _ = first.Configure(new OwnerProviderConfigurationAction(
                "routine",
                "jev",
                "jev-player-model",
                secret,
                false));

            var restarted = new ProviderConfigurationStore(
                path,
                new ProviderConfigurationSeed(
                    "openai",
                    null,
                    "replacement-jev-secret",
                    "replacement-openai-model",
                    "replacement-openai-secret",
                    null,
                    null));
            var runtime = restarted.CaptureRuntimeConfiguration();
            var statusJson = System.Text.Json.JsonSerializer.Serialize(restarted.CaptureStatus());

            Assert.Equal("jev", runtime.RoutineProvider);
            Assert.Equal("deterministic", runtime.PlanningProvider);
            Assert.Equal("jev-player-model", runtime.Jev.Model);
            Assert.Equal(secret, runtime.Jev.ApiKey);
            Assert.DoesNotContain(secret, statusJson, StringComparison.Ordinal);
            Assert.DoesNotContain("replacement-openai-secret", File.ReadAllText(path), StringComparison.Ordinal);
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    File.GetUnixFileMode(path));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ProviderFailureLogKeepsExceptionAndCredentialDetailsOut()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"clankerworld-provider-log-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new ProviderConfigurationStore(
                System.IO.Path.Combine(directory, "providers.json"),
                EmptySeed());
            const string apiKey = "failure-path-api-secret";
            const string providerBody = "failure-path-provider-body-secret";
            _ = store.Configure(new OwnerProviderConfigurationAction(
                "routine",
                "jev",
                "jev-test",
                apiKey,
                false));
            var logger = new RecordingLogger<ConfigurableDecisionProvider>();
            var router = new ConfigurableDecisionProvider(
                store,
                new FixedHttpClientFactory(new ThrowingProviderHandler(providerBody)),
                logger);

            _ = await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await router.DecideAsync(Request(store.CaptureRuntimeConfiguration().Revision)));

            Assert.Contains(logger.Messages, message =>
                message.Contains("cognition_provider_call status=failed", StringComparison.Ordinal) &&
                message.Contains("error_type=HttpRequestException", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message =>
                message.Contains(apiKey, StringComparison.Ordinal) ||
                message.Contains(providerBody, StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static ProviderConfigurationSeed EmptySeed() => new(
        "deterministic",
        null,
        null,
        null,
        null,
        null,
        null);

    private static CognitionDecisionRequest Request(long epoch, bool strategic = false)
    {
        var observation = RequestObservation(strategic);
        return new CognitionDecisionRequest("request-provider-test", epoch, observation);
    }

    private static InhabitantObservation RequestObservation(bool strategic)
    {
        CognitionCandidate[] candidates = strategic
            ? [new CognitionCandidate("build:building:shelter", "Build a shelter.", 20), new CognitionCandidate("safe_idle", "Wait safely.", 100)]
            : [new CognitionCandidate("safe_idle", "Wait safely.", 100)];
        return new InhabitantObservation(
            "inhabitant-test",
            12,
            0,
            3,
            "sha256:provider-test",
            5_000,
            5_000,
            candidates);
    }

    private sealed class FixedHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class ProviderResponseHandler : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        public string? LastAuthorization { get; private set; }
        public string? LastModel { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            LastAuthorization = request.Headers.Authorization?.ToString();
            using var payload = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            LastModel = payload.RootElement.TryGetProperty("model", out var model) ? model.GetString() : null;
            var isJev = string.Equals(request.RequestUri?.Host, "api.typesafe.ai", StringComparison.Ordinal);
            var body = isJev
                ? """
                  {"model":"jev-test","answers":{"selected_candidate":{"type":"choice","choice":"safe_idle","probabilities":{"safe_idle":1.0},"confidence":1.0}}}
                  """
                : """
                  {"model":"hosted-test","choices":[{"message":{"role":"assistant","content":"{\"selected_candidate_id\":\"safe_idle\",\"confidence\":1.0,\"probabilities\":{\"safe_idle\":1.0}}"}}]}
                  """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            };
        }
    }

    private sealed class ThrowingProviderHandler(string message) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException(message);
    }
}
