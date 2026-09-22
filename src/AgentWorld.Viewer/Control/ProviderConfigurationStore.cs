using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using AgentWorld.Simulation.Cognition;

namespace AgentWorld.Viewer.Control;

public static class PlayerDecisionProviders
{
    public const string RoutineRole = "routine";
    public const string PlanningRole = "planning";
    public const string Deterministic = "deterministic";
    public const string Jev = "jev";
    public const string OpenAi = "openai";
    public const string OllamaCloud = "ollama-cloud";

    public const string DefaultJevModel = "jev-1.13.0";
    public const string DefaultOpenAiModel = "gpt-5-mini";
    public const string DefaultOllamaCloudModel = "gpt-oss:120b-cloud";

    public static readonly Uri JevEndpoint = new("https://api.typesafe.ai/v1/systemone", UriKind.Absolute);
    public static readonly Uri OpenAiEndpoint = new("https://api.openai.com/v1/chat/completions", UriKind.Absolute);
    public static readonly Uri OllamaCloudEndpoint = new("https://ollama.com/v1/chat/completions", UriKind.Absolute);

    public static string Normalize(string? provider) => provider?.Trim().ToLowerInvariant() switch
    {
        Deterministic => Deterministic,
        Jev => Jev,
        OpenAi or "openai-compatible" => OpenAi,
        "ollama" or OllamaCloud => OllamaCloud,
        _ => throw new ArgumentException(
            "Provider must be deterministic, jev, openai, or ollama-cloud.",
            nameof(provider)),
    };

    public static string DefaultModel(string provider) => Normalize(provider) switch
    {
        Deterministic => string.Empty,
        Jev => DefaultJevModel,
        OpenAi => DefaultOpenAiModel,
        OllamaCloud => DefaultOllamaCloudModel,
        _ => throw new InvalidOperationException("Unsupported decision provider."),
    };

    public static string NormalizeRole(string? role) => role?.Trim().ToLowerInvariant() switch
    {
        RoutineRole => RoutineRole,
        PlanningRole => PlanningRole,
        _ => throw new ArgumentException("Provider role must be routine or planning.", nameof(role)),
    };

    public static void ValidateRoleProvider(string role, string provider)
    {
        var normalizedRole = NormalizeRole(role);
        var normalizedProvider = Normalize(provider);
        var valid = normalizedRole switch
        {
            RoutineRole => normalizedProvider is Deterministic or Jev,
            PlanningRole => normalizedProvider is Deterministic or OpenAi or OllamaCloud,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException(
                normalizedRole == RoutineRole
                    ? "Routine cognition must use deterministic or Jev."
                    : "Planning cognition must use deterministic, OpenAI, or Ollama Cloud.",
                nameof(provider));
        }
    }
}

public sealed record ProviderConfigurationSeed(
    string ActiveProvider,
    string? JevModel,
    string? JevApiKey,
    string? OpenAiModel,
    string? OpenAiApiKey,
    string? OllamaCloudModel,
    string? OllamaCloudApiKey);

public sealed record StoredProviderCredential(string Model, string? ApiKey);

public sealed record ProviderConfigurationState(
    int SchemaVersion,
    long Revision,
    string RoutineProvider,
    string PlanningProvider,
    StoredProviderCredential Jev,
    StoredProviderCredential OpenAi,
    StoredProviderCredential OllamaCloud,
    IReadOnlyList<InhabitantProviderAssignment>? Assignments = null);

public sealed record RuntimeProviderConfiguration(
    string RoutineProvider,
    string PlanningProvider,
    StoredProviderCredential Jev,
    StoredProviderCredential OpenAi,
    StoredProviderCredential OllamaCloud,
    long Revision,
    IReadOnlyList<InhabitantProviderAssignment>? Assignments = null);

/// <summary>
/// Keeps player-supplied hosted-provider credentials outside world saves and
/// owner-device authority state. The file is installation-local, atomically
/// replaced, and restricted to the service account on Unix hosts.
/// </summary>
public sealed class ProviderConfigurationStore
{
    public const int StateSchemaVersion = 2;
    private const int MaximumApiKeyLength = 4096;
    private const int MaximumModelLength = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly object gate = new();
    private ProviderConfigurationState state;

    public ProviderConfigurationStore(string path, ProviderConfigurationSeed seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(seed);
        Path = System.IO.Path.GetFullPath(path);
        state = LoadOrCreate(seed);
    }

    public string Path { get; }

    public RuntimeProviderConfiguration CaptureRuntimeConfiguration()
    {
        lock (gate)
        {
            return new RuntimeProviderConfiguration(
                state.RoutineProvider,
                state.PlanningProvider,
                state.Jev,
                state.OpenAi,
                state.OllamaCloud,
                state.Revision,
                state.Assignments);
        }
    }

    public OwnerProviderConfigurationStatus CaptureStatus()
    {
        lock (gate)
        {
            return ToStatus(state);
        }
    }

    public OwnerProviderConfigurationStatus Configure(OwnerProviderConfigurationAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (gate)
        {
            var role = PlayerDecisionProviders.NormalizeRole(action.Role);
            if (action.InhabitantId is not null)
            {
                return ConfigureInhabitant(action, role);
            }
            var provider = PlayerDecisionProviders.Normalize(action.Provider);
            PlayerDecisionProviders.ValidateRoleProvider(role, provider);
            var current = state;
            var next = action.ForgetCredential
                ? ForgetCredential(current, role, provider, action)
                : SelectProvider(current, role, provider, action);

            if (next == current)
            {
                return ToStatus(current);
            }

            next = next with { Revision = checked(current.Revision + 1) };
            SaveUnsafe(next);
            state = next;
            return ToStatus(next);
        }
    }

    private OwnerProviderConfigurationStatus ConfigureInhabitant(OwnerProviderConfigurationAction action, string role)
    {
        var id = action.InhabitantId!;
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id != id.Trim())
        {
            throw new ArgumentException("An assignment requires a valid inhabitant ID.", nameof(action));
        }
        if (action.ForgetCredential)
        {
            throw new ArgumentException("Remove shared keys from world settings, not an individual assignment.", nameof(action));
        }

        var assignments = (state.Assignments ?? [])
            .Where(item => item.InhabitantId != id || item.Role != role).ToList();
        var next = state;
        if (action.Provider == "inherit")
        {
            if (action.Model is not null || action.ApiKey is not null)
            {
                throw new ArgumentException("Inheritance does not accept a model or key.", nameof(action));
            }
        }
        else
        {
            var provider = PlayerDecisionProviders.Normalize(action.Provider);
            PlayerDecisionProviders.ValidateRoleProvider(role, provider);
            next = SelectProvider(state, role, provider, action);
            var model = provider == PlayerDecisionProviders.Deterministic ? null : CredentialFor(next, provider)!.Model;
            // Selection validates credentials, but a personal model must not change the world model.
            if (provider != PlayerDecisionProviders.Deterministic)
            {
                next = WithCredential(next, provider, CredentialFor(next, provider)! with { Model = CredentialFor(state, provider)!.Model });
            }
            assignments.Add(new InhabitantProviderAssignment(id, role, provider, model));
        }

        next = next with
        {
            RoutineProvider = state.RoutineProvider,
            PlanningProvider = state.PlanningProvider,
            Assignments = assignments.OrderBy(item => item.InhabitantId, StringComparer.Ordinal)
                .ThenBy(item => item.Role, StringComparer.Ordinal).ToArray(),
            Revision = checked(state.Revision + 1),
        };
        SaveUnsafe(next);
        state = next;
        return ToStatus(next);
    }

    private ProviderConfigurationState LoadOrCreate(ProviderConfigurationSeed seed)
    {
        lock (gate)
        {
            if (!File.Exists(Path))
            {
                var created = CreateInitial(seed);
                SaveUnsafe(created);
                return created;
            }

            var json = File.ReadAllText(Path);
            var loaded = JsonSerializer.Deserialize<ProviderConfigurationState>(json, JsonOptions) ??
                throw new InvalidDataException("The provider-configuration state file is empty.");
            ValidateState(loaded);
            RestrictPermissions(Path);
            return loaded;
        }
    }

    private static ProviderConfigurationState CreateInitial(ProviderConfigurationSeed seed)
    {
        var jev = new StoredProviderCredential(
            NormalizeModel(seed.JevModel, PlayerDecisionProviders.DefaultJevModel),
            NormalizeOptionalApiKey(seed.JevApiKey));
        var openAi = new StoredProviderCredential(
            NormalizeModel(seed.OpenAiModel, PlayerDecisionProviders.DefaultOpenAiModel),
            NormalizeOptionalApiKey(seed.OpenAiApiKey));
        var ollamaCloud = new StoredProviderCredential(
            NormalizeModel(seed.OllamaCloudModel, PlayerDecisionProviders.DefaultOllamaCloudModel),
            NormalizeOptionalApiKey(seed.OllamaCloudApiKey));
        var requestedProvider = PlayerDecisionProviders.Normalize(seed.ActiveProvider);
        var hasRequestedCredential = requestedProvider == PlayerDecisionProviders.Deterministic ||
            !string.IsNullOrWhiteSpace(CredentialFor(requestedProvider, jev, openAi, ollamaCloud).ApiKey);
        var routineProvider = requestedProvider == PlayerDecisionProviders.Jev && hasRequestedCredential
            ? PlayerDecisionProviders.Jev
            : PlayerDecisionProviders.Deterministic;
        var planningProvider = requestedProvider is PlayerDecisionProviders.OpenAi or PlayerDecisionProviders.OllamaCloud &&
            hasRequestedCredential
                ? requestedProvider
                : PlayerDecisionProviders.Deterministic;

        return new ProviderConfigurationState(
            StateSchemaVersion,
            0,
            routineProvider,
            planningProvider,
            jev,
            openAi,
            ollamaCloud);
    }

    private static ProviderConfigurationState SelectProvider(
        ProviderConfigurationState current,
        string role,
        string provider,
        OwnerProviderConfigurationAction action)
    {
        if (provider == PlayerDecisionProviders.Deterministic)
        {
            if (!string.IsNullOrWhiteSpace(action.ApiKey) || !string.IsNullOrWhiteSpace(action.Model))
            {
                throw new ArgumentException("Deterministic cognition does not accept a model or API key.", nameof(action));
            }

            return ActiveProviderFor(current, role) == provider
                ? current
                : WithActiveProvider(current, role, provider);
        }

        var oldCredential = CredentialFor(current, provider)!;
        var model = NormalizeModel(action.Model, oldCredential.Model);
        var apiKey = string.IsNullOrWhiteSpace(action.ApiKey)
            ? oldCredential.ApiKey
            : NormalizeRequiredApiKey(action.ApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException(
                $"{provider} requires an API key. Paste one before enabling the provider.",
                nameof(action));
        }

        return WithActiveProvider(
            WithCredential(current, provider, new StoredProviderCredential(model, apiKey)),
            role,
            provider);
    }

    private static ProviderConfigurationState ForgetCredential(
        ProviderConfigurationState current,
        string role,
        string provider,
        OwnerProviderConfigurationAction action)
    {
        if (provider == PlayerDecisionProviders.Deterministic)
        {
            throw new ArgumentException("Deterministic cognition has no credential to forget.", nameof(action));
        }

        if (!string.IsNullOrWhiteSpace(action.ApiKey))
        {
            throw new ArgumentException("A forget request cannot also contain an API key.", nameof(action));
        }

        var oldCredential = CredentialFor(current, provider)!;
        var model = NormalizeModel(action.Model, oldCredential.Model);
        var next = WithCredential(current, provider, new StoredProviderCredential(model, null));
        next = next with { Assignments = (next.Assignments ?? []).Where(item => item.Provider != provider).ToArray() };
        return string.Equals(ActiveProviderFor(current, role), provider, StringComparison.Ordinal)
            ? WithActiveProvider(next, role, PlayerDecisionProviders.Deterministic)
            : next;
    }

    private static string ActiveProviderFor(ProviderConfigurationState state, string role) => role switch
    {
        PlayerDecisionProviders.RoutineRole => state.RoutineProvider,
        PlayerDecisionProviders.PlanningRole => state.PlanningProvider,
        _ => throw new ArgumentException("Unsupported cognition provider role.", nameof(role)),
    };

    private static ProviderConfigurationState WithActiveProvider(
        ProviderConfigurationState state,
        string role,
        string provider) => role switch
        {
            PlayerDecisionProviders.RoutineRole => state with { RoutineProvider = provider },
            PlayerDecisionProviders.PlanningRole => state with { PlanningProvider = provider },
            _ => throw new ArgumentException("Unsupported cognition provider role.", nameof(role)),
        };

    private static ProviderConfigurationState WithCredential(
        ProviderConfigurationState state,
        string provider,
        StoredProviderCredential credential) => provider switch
        {
            PlayerDecisionProviders.Jev => state with { Jev = credential },
            PlayerDecisionProviders.OpenAi => state with { OpenAi = credential },
            PlayerDecisionProviders.OllamaCloud => state with { OllamaCloud = credential },
            _ => throw new ArgumentException("The selected provider does not store a credential.", nameof(provider)),
        };

    private static StoredProviderCredential? CredentialFor(ProviderConfigurationState state, string provider) =>
        CredentialFor(provider, state.Jev, state.OpenAi, state.OllamaCloud);

    private static StoredProviderCredential CredentialFor(
        string provider,
        StoredProviderCredential jev,
        StoredProviderCredential openAi,
        StoredProviderCredential ollamaCloud) => provider switch
        {
            PlayerDecisionProviders.Jev => jev,
            PlayerDecisionProviders.OpenAi => openAi,
            PlayerDecisionProviders.OllamaCloud => ollamaCloud,
            PlayerDecisionProviders.Deterministic => new StoredProviderCredential(string.Empty, null),
            _ => throw new ArgumentException("Unsupported decision provider.", nameof(provider)),
        };

    private static OwnerProviderConfigurationStatus ToStatus(ProviderConfigurationState state) => new(
        state.RoutineProvider,
        state.PlanningProvider,
        state.Revision,
        new ReadOnlyCollection<OwnerProviderOptionStatus>(
        [
            new(PlayerDecisionProviders.Deterministic, string.Empty, false),
            new(PlayerDecisionProviders.Jev, state.Jev.Model, !string.IsNullOrWhiteSpace(state.Jev.ApiKey)),
            new(PlayerDecisionProviders.OpenAi, state.OpenAi.Model, !string.IsNullOrWhiteSpace(state.OpenAi.ApiKey)),
            new(PlayerDecisionProviders.OllamaCloud, state.OllamaCloud.Model, !string.IsNullOrWhiteSpace(state.OllamaCloud.ApiKey)),
        ]), state.Assignments ?? []);

    private static void ValidateState(ProviderConfigurationState state)
    {
        if (state.SchemaVersion != StateSchemaVersion || state.Revision < 0)
        {
            throw new InvalidDataException("The provider-configuration state has an unsupported schema or revision.");
        }

        var routine = PlayerDecisionProviders.Normalize(state.RoutineProvider);
        var planning = PlayerDecisionProviders.Normalize(state.PlanningProvider);
        PlayerDecisionProviders.ValidateRoleProvider(PlayerDecisionProviders.RoutineRole, routine);
        PlayerDecisionProviders.ValidateRoleProvider(PlayerDecisionProviders.PlanningRole, planning);
        ValidateCredential(state.Jev, PlayerDecisionProviders.DefaultJevModel);
        ValidateCredential(state.OpenAi, PlayerDecisionProviders.DefaultOpenAiModel);
        ValidateCredential(state.OllamaCloud, PlayerDecisionProviders.DefaultOllamaCloudModel);
        var assignmentKeys = new HashSet<(string, string)>();
        foreach (var assignment in state.Assignments ?? [])
        {
            if (string.IsNullOrWhiteSpace(assignment.InhabitantId) ||
                !assignmentKeys.Add((assignment.InhabitantId, assignment.Role)))
            {
                throw new InvalidDataException("Provider assignments must have unique inhabitant/role identities.");
            }
            PlayerDecisionProviders.ValidateRoleProvider(assignment.Role, assignment.Provider);
            if (assignment.Provider != PlayerDecisionProviders.Deterministic &&
                string.IsNullOrWhiteSpace(CredentialFor(state, assignment.Provider)?.ApiKey))
            {
                throw new InvalidDataException("An assigned provider has no stored credential.");
            }
            if (assignment.Model is not null)
            {
                _ = NormalizeModel(assignment.Model, string.Empty);
            }
        }
        if ((routine != PlayerDecisionProviders.Deterministic &&
                string.IsNullOrWhiteSpace(CredentialFor(state, routine)?.ApiKey)) ||
            (planning != PlayerDecisionProviders.Deterministic &&
                string.IsNullOrWhiteSpace(CredentialFor(state, planning)?.ApiKey)))
        {
            throw new InvalidDataException("An active hosted cognition provider has no stored credential.");
        }
    }

    private static void ValidateCredential(StoredProviderCredential? credential, string fallbackModel)
    {
        if (credential is null)
        {
            throw new InvalidDataException("The provider-configuration state is missing a provider record.");
        }

        _ = NormalizeModel(credential.Model, fallbackModel);
        _ = NormalizeOptionalApiKey(credential.ApiKey);
    }

    private void SaveUnsafe(ProviderConfigurationState next)
    {
        ValidateState(next);
        var directory = System.IO.Path.GetDirectoryName(Path) ??
            throw new InvalidOperationException("The provider-configuration path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = System.IO.Path.Combine(
            directory,
            $".{System.IO.Path.GetFileName(Path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(next, JsonOptions));
            RestrictPermissions(temporaryPath);
            File.Move(temporaryPath, Path, overwrite: true);
            RestrictPermissions(Path);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string NormalizeModel(string? model, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(model) ? fallback.Trim() : model.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > MaximumModelLength ||
            normalized.Any(char.IsControl))
        {
            throw new ArgumentException($"A provider model must contain 1 to {MaximumModelLength} printable characters.", nameof(model));
        }

        return normalized;
    }

    private static string? NormalizeOptionalApiKey(string? apiKey) => string.IsNullOrWhiteSpace(apiKey)
        ? null
        : NormalizeRequiredApiKey(apiKey);

    private static string NormalizeRequiredApiKey(string? apiKey)
    {
        var normalized = apiKey?.Trim() ?? string.Empty;
        if (normalized.Length is < 8 or > MaximumApiKeyLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"An API key must contain 8 to {MaximumApiKeyLength} printable characters.",
                nameof(apiKey));
        }

        return normalized;
    }

    private static void RestrictPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}

/// <summary>
/// A stable provider object shared by every inhabitant. Player changes update
/// its kind and epoch dynamically; requests already issued against an older
/// configuration are rejected and fall back locally at the cognition boundary.
/// </summary>
public sealed partial class ConfigurableDecisionProvider(
    ProviderConfigurationStore configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<ConfigurableDecisionProvider>? logger = null) : IDecisionProvider
{
    private static readonly HashSet<string> RoutineCandidateIds = new(StringComparer.Ordinal)
    {
        "consume_food",
        "collect_shared_food",
        "harvest_food",
        "seek_food",
        "sleep",
        "safe_idle",
    };

    public DecisionProviderKind Kind
    {
        get
        {
            var selected = configuration.CaptureRuntimeConfiguration();
            var primary = selected.PlanningProvider != PlayerDecisionProviders.Deterministic
                ? selected.PlanningProvider
                : selected.RoutineProvider;
            return MapKind(primary);
        }
    }

    public long ProviderEpoch => configuration.CaptureRuntimeConfiguration().Revision;

    public DecisionProviderKind KindFor(InhabitantObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var selected = configuration.CaptureRuntimeConfiguration();
        return MapKind(ProviderFor(selected, observation));
    }

    public async ValueTask<CognitionDecisionResponse> DecideAsync(
        CognitionDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var selected = configuration.CaptureRuntimeConfiguration();
        if (request.ProviderEpoch != selected.Revision)
        {
            throw new InvalidOperationException("The cognition provider changed after this request was issued.");
        }

        var isRoutine = IsRoutine(request.Observation);
        var role = isRoutine ? PlayerDecisionProviders.RoutineRole : PlayerDecisionProviders.PlanningRole;
        var providerId = ProviderFor(selected, request.Observation);
        var credential = CredentialFor(selected, providerId);
        var assignment = AssignmentFor(selected, request.Observation);
        if (assignment?.Model is { } model)
        {
            credential = credential with { Model = model };
        }
        IDecisionProvider provider = providerId switch
        {
            PlayerDecisionProviders.Deterministic => new DeterministicDecisionProvider(),
            PlayerDecisionProviders.Jev => new JevDecisionProvider(
                httpClientFactory.CreateClient("typesafe"),
                () => credential.ApiKey,
                PlayerDecisionProviders.JevEndpoint,
                credential.Model,
                providerEpoch: selected.Revision),
            PlayerDecisionProviders.OpenAi => new OpenAiCompatibleDecisionProvider(
                httpClientFactory.CreateClient("model"),
                () => credential.ApiKey,
                PlayerDecisionProviders.OpenAiEndpoint,
                credential.Model,
                providerEpoch: selected.Revision),
            PlayerDecisionProviders.OllamaCloud => new OpenAiCompatibleDecisionProvider(
                httpClientFactory.CreateClient("model"),
                () => credential.ApiKey,
                PlayerDecisionProviders.OllamaCloudEndpoint,
                credential.Model,
                providerEpoch: selected.Revision),
            _ => throw new InvalidOperationException("Unsupported cognition provider configuration."),
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await provider.DecideAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            if (logger is not null)
            {
                LogProviderCallCompleted(
                    logger,
                    providerId,
                    role,
                    string.IsNullOrWhiteSpace(credential.Model) ? "local" : credential.Model,
                    request.Observation.InhabitantId,
                    request.Observation.WorldTick,
                    response.SelectedCandidateId,
                    response.Confidence,
                    response.Usage?.InputTokens ?? 0,
                    response.Usage?.OutputTokens ?? 0,
                    stopwatch.ElapsedMilliseconds);
            }
            return response with
            {
                Provider = MapKind(providerId),
                ProviderEpoch = selected.Revision,
                Usage = new CognitionUsage(
                    response.Usage?.ModelId ?? (string.IsNullOrWhiteSpace(credential.Model) ? null : credential.Model),
                    response.Usage?.InputTokens ?? 0, response.Usage?.OutputTokens ?? 0,
                    stopwatch.ElapsedMilliseconds, providerId, role),
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            if (logger is not null)
            {
                LogProviderCallCancelled(
                    logger,
                    providerId,
                    role,
                    string.IsNullOrWhiteSpace(credential.Model) ? "local" : credential.Model,
                    request.Observation.InhabitantId,
                    request.Observation.WorldTick,
                    stopwatch.ElapsedMilliseconds);
            }
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            stopwatch.Stop();
            if (logger is not null)
            {
                LogProviderCallFailed(
                    logger,
                    providerId,
                    role,
                    string.IsNullOrWhiteSpace(credential.Model) ? "local" : credential.Model,
                    request.Observation.InhabitantId,
                    request.Observation.WorldTick,
                    exception.GetType().Name,
                    stopwatch.ElapsedMilliseconds);
            }
            throw;
        }
    }

    private static string ProviderFor(
        RuntimeProviderConfiguration configuration,
        InhabitantObservation observation)
    {
        return AssignmentFor(configuration, observation)?.Provider ??
            (IsRoutine(observation) ? configuration.RoutineProvider : configuration.PlanningProvider);
    }

    private static InhabitantProviderAssignment? AssignmentFor(RuntimeProviderConfiguration configuration, InhabitantObservation observation) =>
        configuration.Assignments?.FirstOrDefault(item => item.InhabitantId == observation.InhabitantId &&
            item.Role == (IsRoutine(observation) ? PlayerDecisionProviders.RoutineRole : PlayerDecisionProviders.PlanningRole));

    private static bool IsRoutine(InhabitantObservation observation) =>
        observation.Candidates.All(candidate => RoutineCandidateIds.Contains(candidate.Id));

    private static StoredProviderCredential CredentialFor(
        RuntimeProviderConfiguration configuration,
        string provider) => provider switch
        {
            PlayerDecisionProviders.Jev => configuration.Jev,
            PlayerDecisionProviders.OpenAi => configuration.OpenAi,
            PlayerDecisionProviders.OllamaCloud => configuration.OllamaCloud,
            PlayerDecisionProviders.Deterministic => new StoredProviderCredential(string.Empty, null),
            _ => throw new InvalidOperationException("Unsupported cognition provider configuration."),
        };

    private static DecisionProviderKind MapKind(string provider) => provider switch
    {
        PlayerDecisionProviders.Deterministic => DecisionProviderKind.Deterministic,
        PlayerDecisionProviders.Jev => DecisionProviderKind.Jev,
        PlayerDecisionProviders.OpenAi or PlayerDecisionProviders.OllamaCloud => DecisionProviderKind.LargeLanguageModel,
        _ => throw new InvalidOperationException("Unsupported cognition provider configuration."),
    };

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "cognition_provider_call status=completed provider={Provider} role={Role} model={Model} inhabitant={InhabitantId} tick={WorldTick} candidate={CandidateId} confidence={Confidence} input_tokens={InputTokens} output_tokens={OutputTokens} latency_ms={LatencyMilliseconds}")]
    private static partial void LogProviderCallCompleted(
        ILogger logger,
        string provider,
        string role,
        string model,
        string inhabitantId,
        long worldTick,
        string candidateId,
        double confidence,
        int inputTokens,
        int outputTokens,
        long latencyMilliseconds);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Information,
        Message = "cognition_provider_call status=cancelled provider={Provider} role={Role} model={Model} inhabitant={InhabitantId} tick={WorldTick} latency_ms={LatencyMilliseconds}")]
    private static partial void LogProviderCallCancelled(
        ILogger logger,
        string provider,
        string role,
        string model,
        string inhabitantId,
        long worldTick,
        long latencyMilliseconds);

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Warning,
        Message = "cognition_provider_call status=failed provider={Provider} role={Role} model={Model} inhabitant={InhabitantId} tick={WorldTick} error_type={ErrorType} latency_ms={LatencyMilliseconds}")]
    private static partial void LogProviderCallFailed(
        ILogger logger,
        string provider,
        string role,
        string model,
        string inhabitantId,
        long worldTick,
        string errorType,
        long latencyMilliseconds);
}
