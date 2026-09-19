using AgentWorld.Simulation.Kernel;

namespace AgentWorld.Simulation.Tests;

public sealed class InventoryFixtureTests
{
    [Fact]
    public void LotSplitSpoilageAndSaveRestoreRemainCanonicalAndDoNotDecayTwice()
    {
        var genesis = Genesis();
        var split = InventoryFixture.SplitLot(genesis, "alpha-berries", 2, "alpha-berries:split");
        var spoiled = InventoryFixture.ProcessSpoilage(split, 3, 100);
        var restored = InventoryCheckpointCodec.Decode(InventoryCheckpointCodec.Encode(spoiled));
        var processedAgain = InventoryFixture.ProcessSpoilage(restored, 3, 100);
        var fullySpoiled = InventoryFixture.ProcessSpoilage(processedAgain, 6, 100);

        Assert.Equal(2, split.GetLot("alpha-berries").Quantity);
        Assert.Equal("alpha-berries", split.GetLot("alpha-berries:split").ProvenanceLotId);
        Assert.All(spoiled.Lots.Where(lot => lot.ItemKind == "berries"), lot => Assert.Equal(300, lot.FreshnessBasisPoints));
        Assert.Equal(InventoryDigest.State(restored), InventoryDigest.State(processedAgain));
        Assert.Equal(InventoryDigest.Events(restored.Events), InventoryDigest.Events(processedAgain.Events));
        Assert.All(fullySpoiled.Lots.Where(lot => lot.ItemKind == "berries"), lot => Assert.Equal(0, lot.FreshnessBasisPoints));
        Assert.Equal(InventoryDigest.State(spoiled), InventoryDigest.State(restored));
    }

    [Fact]
    public void ReservationExpiryReleasesOnlyTheReservedQuantity()
    {
        var reserved = InventoryFixture.Reserve(Genesis(), "reserve-1", "alpha", "alpha-berries", 1, "meal", 2);
        var released = InventoryFixture.ReleaseExpiredReservations(reserved, 2);

        Assert.Equal(InventoryReservationState.Reserved, reserved.GetReservation("reserve-1").State);
        Assert.Equal(InventoryReservationState.Released, released.GetReservation("reserve-1").State);
        Assert.Contains(released.Events, item => item is { Kind: "reservation_released", Detail: "reserve-1" });
    }

    [Fact]
    public void ExactRevisionAcceptanceSettlesAtomicallyAndRejectsStaleAcceptanceWithoutMutation()
    {
        var proposal = new DirectBarterProposal("offer-1", 3, "alpha", "bravo", "alpha-wood", 2, "bravo-food", 1, 10);
        var offered = InventoryFixture.CreateDirectBarterOffer(Genesis(), proposal);
        var firstAccepted = InventoryFixture.AcceptDirectBarterOffer(offered, "offer-1", 3, "alpha");
        var beforeStaleState = InventoryDigest.State(firstAccepted);
        var beforeStaleEvents = InventoryDigest.Events(firstAccepted.Events);

        Assert.Throws<InvalidOperationException>(() => InventoryFixture.AcceptDirectBarterOffer(firstAccepted, "offer-1", 2, "bravo"));
        Assert.Equal(beforeStaleState, InventoryDigest.State(firstAccepted));
        Assert.Equal(beforeStaleEvents, InventoryDigest.Events(firstAccepted.Events));

        var settled = InventoryFixture.AcceptDirectBarterOffer(firstAccepted, "offer-1", 3, "bravo");
        Assert.Equal(DirectBarterState.Settled, settled.GetOffer("offer-1").State);
        Assert.Equal(InventoryReservationState.Completed, settled.GetReservation("offer-1:first").State);
        Assert.Equal(InventoryReservationState.Completed, settled.GetReservation("offer-1:second").State);
        Assert.Equal("bravo", settled.GetLot("alpha-wood").OwnerId);
        Assert.Equal("alpha", settled.GetLot("bravo-food").OwnerId);
        Assert.Contains(settled.Events, item => item is { Kind: "barter_settled", Detail: "offer-1:r3" });
    }

    private static InventoryCheckpoint Genesis() => InventoryFixture.CreateGenesis(
    [
        new InventoryLot("alpha-berries", "berries", "alpha", 4, 10_000, 600, 0),
        new InventoryLot("alpha-wood", "wood", "alpha", 2, 10_000, 10_000, 0),
        new InventoryLot("bravo-food", "food", "bravo", 1, 10_000, 10_000, 0),
    ]);
}
