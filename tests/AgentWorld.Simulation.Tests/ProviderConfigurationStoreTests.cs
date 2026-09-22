using System.Net;
using AgentWorld.Simulation.Cognition;
using AgentWorld.Viewer.Control;

namespace AgentWorld.Simulation.Tests;

public sealed class ProviderConfigurationStoreTests
{
    [Fact]
    public async Task PlayerCanSwitchAmongEverySupportedProviderAndForgettingAnActiveKeyFallsBack()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"agentworld-provider-store-{Guid.NewGuid():N}");
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
            $"agentworld-provider-restart-{Guid.NewGuid():N}");
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
            $"agentworld-provider-log-{Guid.NewGuid():N}");
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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            LastAuthorization = request.Headers.Authorization?.ToString();
            var isJev = string.Equals(request.RequestUri?.Host, "api.typesafe.ai", StringComparison.Ordinal);
            var body = isJev
                ? """
                  {"model":"jev-test","answers":{"selected_candidate":{"type":"choice","choice":"safe_idle","probabilities":{"safe_idle":1.0},"confidence":1.0}}}
                  """
                : """
                  {"model":"hosted-test","choices":[{"message":{"role":"assistant","content":"{\"selected_candidate_id\":\"safe_idle\",\"confidence\":1.0,\"probabilities\":{\"safe_idle\":1.0}}"}}]}
                  """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            });
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
