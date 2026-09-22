using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgentWorld.Simulation.Kernel;

public enum DurableCommandState { Queued, Active, Completed, Rejected, Superseded, Cancelled }
public enum DurableMessageState { Pending, Delivered, Expired, Rejected }
public enum ProviderRequestState { Active, Applied, Rejected }

public sealed record DurableCommand(
    string Id,
    string IdempotencyKey,
    string IssuerId,
    string Scope,
    long SubmittedTick,
    long SubmissionSequence,
    DurableCommandState State);

public sealed record DurableMessage(
    string Id,
    string SenderId,
    string RecipientId,
    long CreationTick,
    long DeliveryTick,
    long Sequence,
    string Visibility,
    long ExpiryTick,
    DurableMessageState State);

public sealed record ProviderRequest(
    string Id,
    long RunEpoch,
    long ConfigurationEpoch,
    long IssuedTick,
    ProviderRequestState State);

public sealed record ProviderResult(string RequestId, long RunEpoch, long ConfigurationEpoch, string Payload);
public sealed record ControlEvent(long EventId, long WorldTick, string Kind, string Detail);

public sealed record ControlCheckpoint(
    long WorldTick,
    long RunEpoch,
    long ConfigurationEpoch,
    bool IsPaused,
    IReadOnlyList<DurableCommand> Commands,
    IReadOnlyList<DurableMessage> Messages,
    IReadOnlyList<ProviderRequest> ProviderRequests,
    IReadOnlyList<ControlEvent> Events);

/// <summary>
/// Minimal durable ingress fixture. IDs are authoritative and a provider result
/// must match both the saved run epoch and configuration epoch before it can
/// mutate its request record.
/// </summary>
public static class ControlFixture
{
    public static ControlCheckpoint Genesis { get; } = new(0, 0, 0, false, [], [], [], []);

    public static ControlCheckpoint QueueCommand(ControlCheckpoint checkpoint, DurableCommand command)
    {
        Validate(checkpoint);
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommand(command, checkpoint.WorldTick);
        if (checkpoint.Commands.Any(item => item.Id == command.Id || item.IdempotencyKey == command.IdempotencyKey))
        {
            throw new InvalidOperationException("A command ID or idempotency key may be used only once.");
        }

        return Commit(checkpoint,
            commands: checkpoint.Commands.Append(command).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            kind: "command_queued", detail: command.Id);
    }

    public static ControlCheckpoint QueueMessage(ControlCheckpoint checkpoint, DurableMessage message)
    {
        Validate(checkpoint);
        ArgumentNullException.ThrowIfNull(message);
        ValidateMessage(message, checkpoint.WorldTick);
        if (checkpoint.Messages.Any(item => item.Id == message.Id))
        {
            throw new InvalidOperationException("A message ID may be delivered only once.");
        }

        return Commit(checkpoint,
            messages: checkpoint.Messages.Append(message).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            kind: "message_queued", detail: message.Id);
    }

    public static ControlCheckpoint AdvanceDelivery(ControlCheckpoint checkpoint, long targetTick)
    {
        Validate(checkpoint);
        if (checkpoint.IsPaused)
        {
            throw new InvalidOperationException("A paused control checkpoint cannot advance delivery.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(targetTick, checkpoint.WorldTick);
        var due = checkpoint.Messages.Where(message => message.State == DurableMessageState.Pending && message.DeliveryTick <= targetTick)
            .OrderBy(message => message.DeliveryTick).ThenBy(message => message.Sequence).ThenBy(message => message.Id, StringComparer.Ordinal).ToArray();
        var expired = checkpoint.Messages.Where(message => message.State == DurableMessageState.Pending && message.ExpiryTick <= targetTick)
            .Select(message => message.Id).ToHashSet(StringComparer.Ordinal);
        var delivered = due.Where(message => !expired.Contains(message.Id)).Select(message => message.Id).ToHashSet(StringComparer.Ordinal);
        var messages = checkpoint.Messages.Select(message => expired.Contains(message.Id)
                ? message with { State = DurableMessageState.Expired }
                : delivered.Contains(message.Id) ? message with { State = DurableMessageState.Delivered } : message)
            .OrderBy(message => message.Id, StringComparer.Ordinal).ToArray();
        var pending = due.Where(message => delivered.Contains(message.Id)).Select(message => ("message_delivered", message.Id))
            .Concat(expired.OrderBy(id => id, StringComparer.Ordinal).Select(id => ("message_expired", id)));
        return Commit(checkpoint, targetTick, messages: messages, pending: pending);
    }

    public static ControlCheckpoint Pause(ControlCheckpoint checkpoint)
    {
        Validate(checkpoint);
        return checkpoint.IsPaused ? checkpoint : Commit(checkpoint, isPaused: true, kind: "paused", detail: "requested");
    }

    public static ControlCheckpoint Resume(ControlCheckpoint checkpoint)
    {
        Validate(checkpoint);
        return !checkpoint.IsPaused ? checkpoint : Commit(checkpoint, runEpoch: checked(checkpoint.RunEpoch + 1), isPaused: false, kind: "resumed", detail: $"epoch:{checkpoint.RunEpoch + 1}");
    }

    public static ControlCheckpoint SupersedeConfiguration(ControlCheckpoint checkpoint)
    {
        Validate(checkpoint);
        return Commit(checkpoint, configurationEpoch: checked(checkpoint.ConfigurationEpoch + 1), kind: "configuration_superseded", detail: $"epoch:{checkpoint.ConfigurationEpoch + 1}");
    }

    public static ControlCheckpoint IssueProviderRequest(ControlCheckpoint checkpoint, string requestId)
    {
        Validate(checkpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if (checkpoint.IsPaused || checkpoint.ProviderRequests.Any(request => request.Id == requestId))
        {
            throw new InvalidOperationException("A paused world cannot issue a duplicate provider request.");
        }

        var request = new ProviderRequest(requestId, checkpoint.RunEpoch, checkpoint.ConfigurationEpoch, checkpoint.WorldTick, ProviderRequestState.Active);
        return Commit(checkpoint,
            requests: checkpoint.ProviderRequests.Append(request).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            kind: "provider_requested", detail: requestId);
    }

    public static ControlCheckpoint ApplyProviderResult(ControlCheckpoint checkpoint, ProviderResult result)
    {
        Validate(checkpoint);
        ArgumentNullException.ThrowIfNull(result);
        var request = checkpoint.ProviderRequests.SingleOrDefault(item => item.Id == result.RequestId);
        if (request is null)
        {
            return Commit(checkpoint, kind: "provider_result_rejected", detail: $"{result.RequestId}:unknown_request");
        }

        var rejection = checkpoint.IsPaused ? "paused"
            : request.State != ProviderRequestState.Active ? "not_active"
            : result.RunEpoch != checkpoint.RunEpoch || result.RunEpoch != request.RunEpoch ? "run_epoch"
            : result.ConfigurationEpoch != checkpoint.ConfigurationEpoch || result.ConfigurationEpoch != request.ConfigurationEpoch ? "configuration_epoch"
            : null;
        if (rejection is not null)
        {
            return Commit(checkpoint,
                requests: checkpoint.ProviderRequests.Select(item => item.Id == request.Id ? item with { State = ProviderRequestState.Rejected } : item)
                    .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
                kind: "provider_result_rejected", detail: $"{request.Id}:{rejection}");
        }

        return Commit(checkpoint,
            requests: checkpoint.ProviderRequests.Select(item => item.Id == request.Id ? item with { State = ProviderRequestState.Applied } : item)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            kind: "provider_result_applied", detail: request.Id);
    }

    public static void Validate(ControlCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (checkpoint.WorldTick < 0 || checkpoint.RunEpoch < 0 || checkpoint.ConfigurationEpoch < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        EnsureIds(checkpoint.Commands.Select(command => command.Id), "commands");
        EnsureIds(checkpoint.Messages.Select(message => message.Id), "messages");
        EnsureIds(checkpoint.ProviderRequests.Select(request => request.Id), "provider requests");
        var expected = 1L;
        foreach (var worldEvent in checkpoint.Events)
        {
            if (worldEvent.EventId != expected || worldEvent.WorldTick > checkpoint.WorldTick)
            {
                throw new InvalidDataException("Control events are not a committed sequence.");
            }

            expected++;
        }
    }

    private static ControlCheckpoint Commit(
        ControlCheckpoint checkpoint,
        long? targetTick = null,
        long? runEpoch = null,
        long? configurationEpoch = null,
        bool? isPaused = null,
        IReadOnlyList<DurableCommand>? commands = null,
        IReadOnlyList<DurableMessage>? messages = null,
        IReadOnlyList<ProviderRequest>? requests = null,
        string? kind = null,
        string? detail = null,
        IEnumerable<(string Kind, string Detail)>? pending = null)
    {
        var tick = targetTick ?? checkpoint.WorldTick;
        var events = checkpoint.Events.ToList();
        if (kind is not null)
        {
            events.Add(new ControlEvent(checked(events.Count + 1L), tick, kind, detail ?? string.Empty));
        }

        if (pending is not null)
        {
            foreach (var item in pending)
            {
                events.Add(new ControlEvent(checked(events.Count + 1L), tick, item.Kind, item.Detail));
            }
        }

        return new ControlCheckpoint(tick, runEpoch ?? checkpoint.RunEpoch, configurationEpoch ?? checkpoint.ConfigurationEpoch,
            isPaused ?? checkpoint.IsPaused, commands ?? checkpoint.Commands, messages ?? checkpoint.Messages, requests ?? checkpoint.ProviderRequests, events);
    }

    private static void ValidateCommand(DurableCommand command, long worldTick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.IdempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.IssuerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Scope);
        if (command.SubmittedTick > worldTick || command.SubmissionSequence < 0) throw new ArgumentOutOfRangeException(nameof(command));
    }

    private static void ValidateMessage(DurableMessage message, long worldTick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.SenderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.RecipientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Visibility);
        if (message.CreationTick > worldTick || message.DeliveryTick < message.CreationTick || message.ExpiryTick < message.DeliveryTick || message.Sequence < 0) throw new ArgumentOutOfRangeException(nameof(message));
    }

    private static void EnsureIds(IEnumerable<string> ids, string name)
    {
        var actual = ids.ToArray();
        if (actual.Any(string.IsNullOrWhiteSpace) || actual.Distinct(StringComparer.Ordinal).Count() != actual.Length || !actual.SequenceEqual(actual.OrderBy(item => item, StringComparer.Ordinal))) throw new InvalidDataException($"Control {name} must have canonical unique IDs.");
    }
}

public static class ControlCheckpointCodec
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };
    public static byte[] Encode(ControlCheckpoint checkpoint) => JsonSerializer.SerializeToUtf8Bytes(checkpoint, Options);
    public static ControlCheckpoint Decode(byte[] bytes)
    {
        var checkpoint = JsonSerializer.Deserialize<ControlCheckpoint>(bytes, Options) ?? throw new InvalidDataException("The control checkpoint is empty.");
        ControlFixture.Validate(checkpoint);
        return checkpoint;
    }
}

public static class ControlDigest
{
    public static string State(ControlCheckpoint checkpoint) => Digest(Encoding.UTF8.GetString(ControlCheckpointCodec.Encode(checkpoint with { Events = [] })));
    public static string Events(IEnumerable<ControlEvent> events) => Digest(string.Join('\n', events.OrderBy(item => item.EventId).Select(item => $"{item.EventId}|{item.WorldTick}|{item.Kind}|{item.Detail}")));
    private static string Digest(string canonical) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
}
