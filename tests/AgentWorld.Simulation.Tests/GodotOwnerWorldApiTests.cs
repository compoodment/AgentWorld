using AgentWorld.GodotClient.UI;
using OwnerHttpBinding = AgentWorld.Viewer.Control.OwnerHttpBinding;
using ServerDeviceManagementAction = AgentWorld.Viewer.Control.OwnerDeviceManagementAction;
using ServerInstructionAction = AgentWorld.Viewer.Control.OwnerInstructionAction;
using ServerPairingApprovalAction = AgentWorld.Viewer.Control.OwnerPairingApprovalAction;

namespace AgentWorld.Simulation.Tests;

public sealed class GodotOwnerWorldApiTests
{
    [Fact]
    public void AcceptsCoherentPairedOwnerReconnectAndAdvancesCursor()
    {
        var session = new OwnerWorldObservationSession();
        var response = CreateCoherentReconnect();

        var accepted = session.TryAccept(response, requestedAfterEventId: 3, out var failure);

        Assert.True(accepted, failure);
        Assert.Same(response, session.Current);
        Assert.Equal(5, session.EventCursor);
    }

    [Fact]
    public void RejectsMissingOwnerCapabilityWithoutDiscardingLastGoodWorld()
    {
        var session = new OwnerWorldObservationSession();
        var accepted = CreateCoherentReconnect();
        Assert.True(session.TryAccept(accepted, requestedAfterEventId: 3, out _));

        var missingCapability = accepted with
        {
            Handshake = accepted.Handshake with
            {
                ServerCapabilities = accepted.Handshake.ServerCapabilities
                    .Where(capability => capability != "paused-authoring.request.v1")
                    .ToArray(),
            },
        };

        var wasAccepted = session.TryAccept(missingCapability, requestedAfterEventId: 3, out var failure);

        Assert.False(wasAccepted);
        Assert.Contains("required paired-owner capabilities", failure, StringComparison.Ordinal);
        Assert.Same(accepted, session.Current);
    }

    [Fact]
    public void RejectsIncoherentOwnerReconnectWithoutDiscardingLastGoodWorld()
    {
        var session = new OwnerWorldObservationSession();
        var accepted = CreateCoherentReconnect();
        Assert.True(session.TryAccept(accepted, requestedAfterEventId: 3, out _));

        var incomplete = accepted with
        {
            Baseline = accepted.Baseline with
            {
                Events = accepted.Baseline.Events with
                {
                    Events = [accepted.Baseline.Events.Events[0]],
                },
            },
        };

        var wasAccepted = session.TryAccept(incomplete, requestedAfterEventId: 3, out var failure);

        Assert.False(wasAccepted);
        Assert.Contains("incomplete", failure, StringComparison.Ordinal);
        Assert.Same(accepted, session.Current);
    }

    [Fact]
    public void InstructionPayloadMatchesViewerOwnerProtocolByteForByte()
    {
        var clientAction = new OwnerInstructionAction(
            IdempotencyKey: "instruction-01",
            TargetInhabitantId: "camp-alpha",
            Kind: "must-do",
            Text: "Gather wood before dusk.");
        var serverAction = new ServerInstructionAction(
            clientAction.IdempotencyKey,
            clientAction.TargetInhabitantId,
            clientAction.Kind,
            clientAction.Text);

        var clientPayload = OwnerWorldActionPayload.Instruction(clientAction);
        var serverPayload = OwnerHttpBinding.InstructionPayload(serverAction);

        Assert.Equal(serverPayload, clientPayload);
    }

    [Fact]
    public void PairingApprovalPayloadMatchesViewerOwnerProtocolByteForByte()
    {
        var clientAction = new OwnerPairingApprovalAction("pairing_01", "042069");
        var serverAction = new ServerPairingApprovalAction(clientAction.PairingId, clientAction.PairingCode);

        var clientPayload = OwnerWorldActionPayload.PairingApproval(clientAction);
        var serverPayload = OwnerHttpBinding.PairingApprovalPayload(serverAction);

        Assert.Equal(serverPayload, clientPayload);
    }

    [Fact]
    public void DeviceManagementPayloadMatchesViewerOwnerProtocolByteForByte()
    {
        var clientAction = new OwnerDeviceManagementAction("device_01");
        var serverAction = new ServerDeviceManagementAction(clientAction.DeviceId);

        var clientPayload = OwnerWorldActionPayload.DeviceManagement(clientAction);
        var serverPayload = OwnerHttpBinding.DeviceManagementPayload(serverAction);

        Assert.Equal(serverPayload, clientPayload);
    }

    [Fact]
    public void DeviceListPayloadMatchesViewerOwnerProtocolByteForByte()
    {
        var clientPayload = OwnerWorldActionPayload.DeviceList();
        var serverPayload = OwnerHttpBinding.DeviceListPayload();

        Assert.Equal(serverPayload, clientPayload);
    }

    [Fact]
    public void ControlPayloadMatchesViewerOwnerProtocolByteForByte()
    {
        var clientPayload = OwnerWorldActionPayload.Control("pause");
        var serverPayload = OwnerHttpBinding.EmptyPayload("pause");

        Assert.Equal(serverPayload, clientPayload);
    }

    private static OwnerWorldReconnect CreateCoherentReconnect()
    {
        var handshake = new OwnerWorldHandshake(
            Protocol: new OwnerWorldProtocolVersion(1, 1),
            ServerCapabilities:
            [
                "owner-observation.read.v1",
                "inhabitant-inspection.read.v1",
                "spatial-knowledge.read.v1",
                "owner-control.request.v1",
                "paused-authoring.request.v1",
            ],
            ClientCapabilities: []);
        var snapshot = new OwnerWorldSnapshot(
            WorldId: "fixture-world",
            WorldTick: 5,
            MapManifestDigest: "fixture-map",
            Tiles: [new OwnerWorldTile(0, 0, "meadow")],
            Objects: [],
            Resources: [],
            Actor: new OwnerWorldActor("camp-alpha", new OwnerWorldPosition(0, 0), 5000, 8000, 1, 0),
            LatestEventId: 5);
        var events = new OwnerWorldEventSlice(
            SnapshotTick: 5,
            AfterEventId: 3,
            Events:
            [
                new OwnerWorldEvent(4, 4, "move", "north"),
                new OwnerWorldEvent(5, 5, "sleep", "camp"),
            ]);

        return new OwnerWorldReconnect(handshake, new OwnerWorldReconnectBaseline(snapshot, events));
    }
}
