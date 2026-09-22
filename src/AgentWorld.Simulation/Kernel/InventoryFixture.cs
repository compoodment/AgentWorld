using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AgentWorld.Simulation.Kernel;

public enum InventoryLotState
{
    Usable,
    Degraded,
    Spoiled,
    Destroyed,
}

public enum InventoryReservationState
{
    Reserved,
    PartiallyConsumed,
    Committed,
    Completed,
    Released,
}

public enum DirectBarterState
{
    Open,
    Settled,
    Cancelled,
}

/// <summary>
/// Immutable inventory identity. A split retains the source lot as provenance;
/// quantities, condition and freshness are never fabricated by a transfer.
/// </summary>
public sealed record InventoryLot(
    string Id,
    string ItemKind,
    string OwnerId,
    int Quantity,
    int ConditionBasisPoints,
    int FreshnessBasisPoints,
    long LastProcessedTick,
    string? ProvenanceLotId = null);

public sealed record InventoryReservation(
    string Id,
    string OwnerId,
    string LotId,
    int Quantity,
    string Purpose,
    long ExpiryTick,
    bool IsExclusive,
    InventoryReservationState State);

public sealed record DirectBarterOffer(
    string Id,
    int Revision,
    string FirstPartyId,
    string SecondPartyId,
    string FirstLotId,
    int FirstQuantity,
    string SecondLotId,
    int SecondQuantity,
    long ExpiryTick,
    DirectBarterState State,
    IReadOnlyList<string> AcceptedBy);

public sealed record InventoryEvent(long EventId, long WorldTick, string Kind, string Detail);

public sealed record InventoryCheckpoint(
    long WorldTick,
    IReadOnlyList<InventoryLot> Lots,
    IReadOnlyList<InventoryReservation> Reservations,
    IReadOnlyList<DirectBarterOffer> Offers,
    IReadOnlyList<InventoryEvent> Events)
{
    public InventoryLot GetLot(string id) => Lots.Single(lot => string.Equals(lot.Id, id, StringComparison.Ordinal));
    public InventoryReservation GetReservation(string id) => Reservations.Single(reservation => string.Equals(reservation.Id, id, StringComparison.Ordinal));
    public DirectBarterOffer GetOffer(string id) => Offers.Single(offer => string.Equals(offer.Id, id, StringComparison.Ordinal));
}

public sealed record DirectBarterProposal(
    string Id,
    int Revision,
    string FirstPartyId,
    string SecondPartyId,
    string FirstLotId,
    int FirstQuantity,
    string SecondLotId,
    int SecondQuantity,
    long ExpiryTick);

/// <summary>
/// Compact authoritative inventory fixture. Every public transition validates
/// before it creates replacement records, so a rejection leaves its supplied
/// checkpoint untouched.
/// </summary>
public static class InventoryFixture
{
    public static InventoryCheckpoint CreateGenesis(IEnumerable<InventoryLot> lots)
    {
        ArgumentNullException.ThrowIfNull(lots);
        var orderedLots = lots.OrderBy(lot => lot.Id, StringComparer.Ordinal).ToArray();
        ValidateLots(orderedLots);
        return new InventoryCheckpoint(0, orderedLots, [], [], []);
    }

    public static InventoryCheckpoint SplitLot(
        InventoryCheckpoint checkpoint,
        string lotId,
        int splitQuantity,
        string splitLotId)
    {
        ValidateCheckpoint(checkpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(splitLotId);
        var source = checkpoint.GetLot(lotId);
        if (splitQuantity <= 0 || splitQuantity >= source.Quantity ||
            checkpoint.Lots.Any(lot => string.Equals(lot.Id, splitLotId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("A lot split requires a unique ID and a strict positive subquantity.");
        }

        var split = source with { Id = splitLotId, Quantity = splitQuantity, ProvenanceLotId = source.Id };
        var lots = checkpoint.Lots.Select(lot => string.Equals(lot.Id, source.Id, StringComparison.Ordinal)
                ? lot with { Quantity = lot.Quantity - splitQuantity }
                : lot)
            .Append(split)
            .OrderBy(lot => lot.Id, StringComparer.Ordinal)
            .ToArray();
        return Commit(checkpoint, lots: lots, eventKind: "lot_split", detail: $"{source.Id}:{splitLotId}:{splitQuantity}");
    }

    public static InventoryCheckpoint ProcessSpoilage(
        InventoryCheckpoint checkpoint,
        long targetTick,
        int freshnessLossPerTick)
    {
        ValidateCheckpoint(checkpoint);
        if (targetTick < checkpoint.WorldTick || freshnessLossPerTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetTick));
        }

        var pendingEvents = new List<(string Kind, string Detail)>();
        var lots = checkpoint.Lots.Select(lot =>
        {
            var elapsed = targetTick - lot.LastProcessedTick;
            if (elapsed <= 0 || lot.Quantity == 0)
            {
                return lot;
            }

            var freshnessLoss = checked(elapsed * freshnessLossPerTick);
            var freshness = freshnessLoss >= lot.FreshnessBasisPoints
                ? 0
                : lot.FreshnessBasisPoints - checked((int)freshnessLoss);
            if (lot.FreshnessBasisPoints > 0 && freshness == 0)
            {
                pendingEvents.Add(("lot_spoiled", lot.Id));
            }

            return lot with { FreshnessBasisPoints = freshness, LastProcessedTick = targetTick };
        }).OrderBy(lot => lot.Id, StringComparer.Ordinal).ToArray();

        return Commit(checkpoint, targetTick, lots, pendingEvents: pendingEvents);
    }

    public static InventoryCheckpoint Reserve(
        InventoryCheckpoint checkpoint,
        string reservationId,
        string ownerId,
        string lotId,
        int quantity,
        string purpose,
        long expiryTick,
        bool isExclusive = true)
    {
        ValidateCheckpoint(checkpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(reservationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (quantity <= 0 || expiryTick < checkpoint.WorldTick ||
            checkpoint.Reservations.Any(reservation => string.Equals(reservation.Id, reservationId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Reservation ID, quantity, and expiry must be valid.");
        }

        var lot = checkpoint.GetLot(lotId);
        EnsureOwnerAndAvailableQuantity(checkpoint, lot, ownerId, quantity);
        var reservation = new InventoryReservation(
            reservationId, ownerId, lotId, quantity, purpose, expiryTick, isExclusive, InventoryReservationState.Reserved);
        return Commit(
            checkpoint,
            reservations: checkpoint.Reservations.Append(reservation).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            eventKind: "inventory_reserved",
            detail: reservationId);
    }

    public static InventoryCheckpoint ConsumeReservation(
        InventoryCheckpoint checkpoint,
        string reservationId)
    {
        ValidateCheckpoint(checkpoint);
        var reservation = checkpoint.GetReservation(reservationId);
        if (reservation.State is not (InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed) ||
            checkpoint.WorldTick > reservation.ExpiryTick)
        {
            throw new InvalidOperationException("Only a live reservation can be consumed.");
        }

        var lot = checkpoint.GetLot(reservation.LotId);
        EnsureOwnerAndExactQuantity(lot, reservation.OwnerId, reservation.Quantity);
        var lots = lot.Quantity == reservation.Quantity
            ? checkpoint.Lots.Where(candidate => candidate.Id != lot.Id).ToArray()
            : checkpoint.Lots.Select(candidate => candidate.Id == lot.Id
                    ? candidate with { Quantity = candidate.Quantity - reservation.Quantity }
                    : candidate)
                .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
                .ToArray();
        var reservations = checkpoint.Reservations.Select(candidate => candidate.Id == reservation.Id
                ? candidate with { State = InventoryReservationState.Completed }
                : candidate)
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
        return Commit(
            checkpoint,
            lots: lots,
            reservations: reservations,
            eventKind: "reservation_consumed",
            detail: reservationId);
    }

    public static InventoryCheckpoint Transfer(
        InventoryCheckpoint checkpoint,
        string transferId,
        string senderId,
        string recipientId,
        string lotId,
        int quantity,
        string purpose)
    {
        ValidateCheckpoint(checkpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(transferId);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (senderId == recipientId || quantity <= 0 ||
            checkpoint.Lots.Any(lot => string.Equals(lot.Id, $"{lotId}#transfer:{transferId}", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("A transfer requires distinct parties, a positive quantity, and a unique ID.");
        }

        var source = checkpoint.GetLot(lotId);
        EnsureOwnerAndAvailableQuantity(checkpoint, source, senderId, quantity);
        var lots = quantity == source.Quantity
            ? checkpoint.Lots.Select(lot => lot.Id == source.Id ? lot with { OwnerId = recipientId } : lot)
                .OrderBy(lot => lot.Id, StringComparer.Ordinal)
                .ToArray()
            : checkpoint.Lots.Select(lot => lot.Id == source.Id
                    ? lot with { Quantity = lot.Quantity - quantity }
                    : lot)
                .Append(source with
                {
                    Id = $"{source.Id}#transfer:{transferId}",
                    OwnerId = recipientId,
                    Quantity = quantity,
                    ProvenanceLotId = source.Id,
                })
                .OrderBy(lot => lot.Id, StringComparer.Ordinal)
                .ToArray();
        return Commit(
            checkpoint,
            lots: lots,
            eventKind: "inventory_transferred",
            detail: $"{transferId}:{senderId}:{recipientId}:{lotId}:{quantity}:{purpose}");
    }

    public static InventoryCheckpoint ReleaseExpiredReservations(InventoryCheckpoint checkpoint, long targetTick)
    {
        ValidateCheckpoint(checkpoint);
        ArgumentOutOfRangeException.ThrowIfLessThan(targetTick, checkpoint.WorldTick);

        var released = checkpoint.Reservations.Where(reservation =>
            reservation.State == InventoryReservationState.Reserved && reservation.ExpiryTick <= targetTick)
            .Select(reservation => reservation.Id)
            .ToHashSet(StringComparer.Ordinal);
        var reservations = checkpoint.Reservations.Select(reservation => released.Contains(reservation.Id)
                ? reservation with { State = InventoryReservationState.Released }
                : reservation)
            .OrderBy(reservation => reservation.Id, StringComparer.Ordinal)
            .ToArray();
        var events = released.OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => ("reservation_released", id))
            .ToArray();
        return Commit(checkpoint, targetTick, reservations: reservations, pendingEvents: events);
    }

    public static InventoryCheckpoint CreateDirectBarterOffer(
        InventoryCheckpoint checkpoint,
        DirectBarterProposal proposal)
    {
        ValidateCheckpoint(checkpoint);
        ArgumentNullException.ThrowIfNull(proposal);
        ValidateProposal(proposal, checkpoint.WorldTick);
        if (checkpoint.Offers.Any(offer => string.Equals(offer.Id, proposal.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("A barter offer ID cannot be reused.");
        }

        var firstLot = checkpoint.GetLot(proposal.FirstLotId);
        var secondLot = checkpoint.GetLot(proposal.SecondLotId);
        EnsureOwnerAndAvailableQuantity(checkpoint, firstLot, proposal.FirstPartyId, proposal.FirstQuantity);
        EnsureOwnerAndAvailableQuantity(checkpoint, secondLot, proposal.SecondPartyId, proposal.SecondQuantity);
        var firstReservation = new InventoryReservation(
            $"{proposal.Id}:first", proposal.FirstPartyId, proposal.FirstLotId, proposal.FirstQuantity,
            $"barter:{proposal.Id}", proposal.ExpiryTick, true, InventoryReservationState.Reserved);
        var secondReservation = new InventoryReservation(
            $"{proposal.Id}:second", proposal.SecondPartyId, proposal.SecondLotId, proposal.SecondQuantity,
            $"barter:{proposal.Id}", proposal.ExpiryTick, true, InventoryReservationState.Reserved);
        if (checkpoint.Reservations.Any(reservation =>
            string.Equals(reservation.Id, firstReservation.Id, StringComparison.Ordinal) ||
            string.Equals(reservation.Id, secondReservation.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The barter offer reservation IDs are already in use.");
        }

        var offer = new DirectBarterOffer(
            proposal.Id, proposal.Revision, proposal.FirstPartyId, proposal.SecondPartyId,
            proposal.FirstLotId, proposal.FirstQuantity, proposal.SecondLotId, proposal.SecondQuantity,
            proposal.ExpiryTick, DirectBarterState.Open, []);
        return Commit(
            checkpoint,
            reservations: checkpoint.Reservations.Append(firstReservation).Append(secondReservation)
                .OrderBy(reservation => reservation.Id, StringComparer.Ordinal).ToArray(),
            offers: checkpoint.Offers.Append(offer).OrderBy(candidate => candidate.Id, StringComparer.Ordinal).ToArray(),
            eventKind: "barter_offered",
            detail: $"{proposal.Id}:r{proposal.Revision}");
    }

    public static InventoryCheckpoint AcceptDirectBarterOffer(
        InventoryCheckpoint checkpoint,
        string offerId,
        int revision,
        string partyId)
    {
        ValidateCheckpoint(checkpoint);
        var offer = checkpoint.GetOffer(offerId);
        if (offer.State != DirectBarterState.Open || offer.Revision != revision ||
            (partyId != offer.FirstPartyId && partyId != offer.SecondPartyId))
        {
            throw new InvalidOperationException("Only an open offer's exact revision may be accepted by a party.");
        }

        if (offer.AcceptedBy.Contains(partyId, StringComparer.Ordinal))
        {
            return checkpoint;
        }

        var acceptedBy = offer.AcceptedBy.Append(partyId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var acceptedOffer = offer with { AcceptedBy = acceptedBy };
        var offers = checkpoint.Offers.Select(candidate => string.Equals(candidate.Id, offer.Id, StringComparison.Ordinal)
                ? acceptedOffer
                : candidate)
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
        var accepted = Commit(checkpoint, offers: offers, eventKind: "barter_accepted", detail: $"{offer.Id}:{partyId}:r{revision}");
        return acceptedBy.Length == 2 ? Settle(accepted, acceptedOffer) : accepted;
    }

    private static InventoryCheckpoint Settle(InventoryCheckpoint checkpoint, DirectBarterOffer offer)
    {
        var firstReservation = checkpoint.GetReservation($"{offer.Id}:first");
        var secondReservation = checkpoint.GetReservation($"{offer.Id}:second");
        if (firstReservation.State != InventoryReservationState.Reserved ||
            secondReservation.State != InventoryReservationState.Reserved ||
            checkpoint.WorldTick > offer.ExpiryTick)
        {
            throw new InvalidOperationException("The barter offer no longer has two valid reservations.");
        }

        var firstLot = checkpoint.GetLot(offer.FirstLotId);
        var secondLot = checkpoint.GetLot(offer.SecondLotId);
        EnsureOwnerAndExactQuantity(firstLot, offer.FirstPartyId, offer.FirstQuantity);
        EnsureOwnerAndExactQuantity(secondLot, offer.SecondPartyId, offer.SecondQuantity);
        var afterFirstTransfer = TransferExact(checkpoint.Lots, firstLot, offer.FirstQuantity, offer.SecondPartyId, offer.Id);
        var afterBothTransfers = TransferExact(afterFirstTransfer, secondLot, offer.SecondQuantity, offer.FirstPartyId, offer.Id);
        var reservations = checkpoint.Reservations.Select(reservation =>
            reservation.Id == firstReservation.Id || reservation.Id == secondReservation.Id
                ? reservation with { State = InventoryReservationState.Completed }
                : reservation).OrderBy(reservation => reservation.Id, StringComparer.Ordinal).ToArray();
        var offers = checkpoint.Offers.Select(candidate => candidate.Id == offer.Id
                ? candidate with { State = DirectBarterState.Settled }
                : candidate).OrderBy(candidate => candidate.Id, StringComparer.Ordinal).ToArray();
        return Commit(checkpoint, lots: afterBothTransfers, reservations: reservations, offers: offers,
            eventKind: "barter_settled", detail: $"{offer.Id}:r{offer.Revision}");
    }

    private static InventoryLot[] TransferExact(
        IReadOnlyList<InventoryLot> lots,
        InventoryLot source,
        int quantity,
        string recipientId,
        string offerId)
    {
        if (source.Quantity == quantity)
        {
            return lots.Select(lot => lot.Id == source.Id ? lot with { OwnerId = recipientId } : lot)
                .OrderBy(lot => lot.Id, StringComparer.Ordinal).ToArray();
        }

        var transferId = $"{source.Id}#barter:{offerId}";
        if (lots.Any(lot => lot.Id == transferId))
        {
            throw new InvalidOperationException("The barter transfer lot ID is already in use.");
        }

        var transferred = source with
        {
            Id = transferId,
            OwnerId = recipientId,
            Quantity = quantity,
            ProvenanceLotId = source.Id,
        };
        return lots.Select(lot => lot.Id == source.Id ? lot with { Quantity = lot.Quantity - quantity } : lot)
            .Append(transferred).OrderBy(lot => lot.Id, StringComparer.Ordinal).ToArray();
    }

    private static void EnsureOwnerAndAvailableQuantity(
        InventoryCheckpoint checkpoint,
        InventoryLot lot,
        string expectedOwnerId,
        int requestedQuantity)
    {
        EnsureOwnerAndExactQuantity(lot, expectedOwnerId, requestedQuantity);

        var reserved = checkpoint.Reservations.Where(reservation => reservation.LotId == lot.Id &&
                reservation.State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed or InventoryReservationState.Committed)
            .Sum(reservation => reservation.Quantity);
        if (reserved > lot.Quantity - requestedQuantity)
        {
            throw new InvalidOperationException("An active reservation already consumes the requested lot quantity.");
        }
    }

    private static void EnsureOwnerAndExactQuantity(
        InventoryLot lot,
        string expectedOwnerId,
        int requestedQuantity)
    {
        if (lot.OwnerId != expectedOwnerId || requestedQuantity <= 0 || lot.Quantity < requestedQuantity ||
            lot.FreshnessBasisPoints == 0 || lot.ConditionBasisPoints == 0)
        {
            throw new InvalidOperationException("The exact owned usable lot quantity is unavailable.");
        }
    }

    private static InventoryCheckpoint Commit(
        InventoryCheckpoint checkpoint,
        long? targetTick = null,
        IReadOnlyList<InventoryLot>? lots = null,
        IReadOnlyList<InventoryReservation>? reservations = null,
        IReadOnlyList<DirectBarterOffer>? offers = null,
        string? eventKind = null,
        string? detail = null,
        IEnumerable<(string Kind, string Detail)>? pendingEvents = null)
    {
        var nextTick = targetTick ?? checkpoint.WorldTick;
        var events = checkpoint.Events.ToList();
        if (eventKind is not null)
        {
            events.Add(new InventoryEvent(checked(events.Count + 1L), nextTick, eventKind, detail ?? string.Empty));
        }

        if (pendingEvents is not null)
        {
            foreach (var pending in pendingEvents.OrderBy(item => item.Detail, StringComparer.Ordinal))
            {
                events.Add(new InventoryEvent(checked(events.Count + 1L), nextTick, pending.Kind, pending.Detail));
            }
        }

        return new InventoryCheckpoint(
            nextTick,
            lots ?? checkpoint.Lots,
            reservations ?? checkpoint.Reservations,
            offers ?? checkpoint.Offers,
            events);
    }

    private static void ValidateCheckpoint(InventoryCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (checkpoint.WorldTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        ValidateLots(checkpoint.Lots);
        EnsureCanonicalIds(checkpoint.Reservations.Select(reservation => reservation.Id), "reservations");
        EnsureCanonicalIds(checkpoint.Offers.Select(offer => offer.Id), "offers");
    }

    private static void ValidateLots(IReadOnlyList<InventoryLot> lots)
    {
        EnsureCanonicalIds(lots.Select(lot => lot.Id), "lots");
        foreach (var lot in lots)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(lot.ItemKind);
            ArgumentException.ThrowIfNullOrWhiteSpace(lot.OwnerId);
            if (lot.Quantity <= 0 || lot.LastProcessedTick < 0 ||
                lot.ConditionBasisPoints is < 0 or > 10_000 || lot.FreshnessBasisPoints is < 0 or > 10_000)
            {
                throw new ArgumentOutOfRangeException(nameof(lots));
            }
        }
    }

    private static void EnsureCanonicalIds(IEnumerable<string> ids, string collectionName)
    {
        var actual = ids.ToArray();
        if (actual.Any(string.IsNullOrWhiteSpace) || actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !actual.SequenceEqual(actual.OrderBy(id => id, StringComparer.Ordinal)))
        {
            throw new InvalidDataException($"Inventory {collectionName} must have unique canonical IDs.");
        }
    }

    private static void ValidateProposal(DirectBarterProposal proposal, long worldTick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proposal.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposal.FirstPartyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposal.SecondPartyId);
        if (proposal.Revision < 0 || proposal.FirstPartyId == proposal.SecondPartyId || proposal.FirstQuantity <= 0 ||
            proposal.SecondQuantity <= 0 || proposal.ExpiryTick < worldTick)
        {
            throw new ArgumentOutOfRangeException(nameof(proposal));
        }
    }
}

public static class InventoryCheckpointCodec
{
    private const string Header = "agentworld.inventory-fixture/v1";

    public static byte[] Encode(InventoryCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var builder = new StringBuilder(Header).Append('\n').Append("tick=").Append(checkpoint.WorldTick).Append('\n');
        foreach (var lot in checkpoint.Lots)
        {
            builder.Append("lot=").Append(lot.Id).Append('|').Append(lot.ItemKind).Append('|').Append(lot.OwnerId).Append('|')
                .Append(lot.Quantity).Append('|').Append(lot.ConditionBasisPoints).Append('|').Append(lot.FreshnessBasisPoints).Append('|')
                .Append(lot.LastProcessedTick).Append('|').Append(lot.ProvenanceLotId ?? "-").Append('\n');
        }

        foreach (var reservation in checkpoint.Reservations)
        {
            builder.Append("reservation=").Append(reservation.Id).Append('|').Append(reservation.OwnerId).Append('|').Append(reservation.LotId).Append('|')
                .Append(reservation.Quantity).Append('|').Append(reservation.Purpose).Append('|').Append(reservation.ExpiryTick).Append('|')
                .Append(reservation.IsExclusive ? "exclusive" : "shared").Append('|').Append(reservation.State).Append('\n');
        }

        foreach (var offer in checkpoint.Offers)
        {
            builder.Append("offer=").Append(offer.Id).Append('|').Append(offer.Revision).Append('|').Append(offer.FirstPartyId).Append('|')
                .Append(offer.SecondPartyId).Append('|').Append(offer.FirstLotId).Append('|').Append(offer.FirstQuantity).Append('|')
                .Append(offer.SecondLotId).Append('|').Append(offer.SecondQuantity).Append('|').Append(offer.ExpiryTick).Append('|')
                .Append(offer.State).Append('|').Append(string.Join(',', offer.AcceptedBy)).Append('\n');
        }

        foreach (var worldEvent in checkpoint.Events)
        {
            builder.Append("event=").Append(worldEvent.EventId).Append('|').Append(worldEvent.WorldTick).Append('|')
                .Append(worldEvent.Kind).Append('|').Append(worldEvent.Detail).Append('\n');
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static InventoryCheckpoint Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var lines = Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2 || lines[0] != Header)
        {
            throw new InvalidDataException("The inventory checkpoint format is not supported.");
        }

        var checkpoint = new InventoryCheckpoint(
            ParseLong(ValueAfterPrefix(lines[1], "tick=")),
            lines.Skip(2).Where(line => line.StartsWith("lot=", StringComparison.Ordinal)).Select(ParseLot).OrderBy(lot => lot.Id, StringComparer.Ordinal).ToArray(),
            lines.Skip(2).Where(line => line.StartsWith("reservation=", StringComparison.Ordinal)).Select(ParseReservation).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            lines.Skip(2).Where(line => line.StartsWith("offer=", StringComparison.Ordinal)).Select(ParseOffer).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            lines.Skip(2).Where(line => line.StartsWith("event=", StringComparison.Ordinal)).Select(ParseEvent).OrderBy(item => item.EventId).ToArray());
        _ = InventoryDigest.State(checkpoint);
        return checkpoint;
    }

    private static InventoryLot ParseLot(string line)
    {
        var parts = ValueAfterPrefix(line, "lot=").Split('|');
        if (parts.Length != 8) throw new InvalidDataException("The saved lot is invalid.");
        return new InventoryLot(parts[0], parts[1], parts[2], ParseInt(parts[3]), ParseInt(parts[4]), ParseInt(parts[5]), ParseLong(parts[6]), parts[7] == "-" ? null : parts[7]);
    }

    private static InventoryReservation ParseReservation(string line)
    {
        var parts = ValueAfterPrefix(line, "reservation=").Split('|');
        if (parts.Length != 8 || !Enum.TryParse<InventoryReservationState>(parts[7], out var state)) throw new InvalidDataException("The saved reservation is invalid.");
        return new InventoryReservation(parts[0], parts[1], parts[2], ParseInt(parts[3]), parts[4], ParseLong(parts[5]), parts[6] == "exclusive", state);
    }

    private static DirectBarterOffer ParseOffer(string line)
    {
        var parts = ValueAfterPrefix(line, "offer=").Split('|');
        if (parts.Length != 11 || !Enum.TryParse<DirectBarterState>(parts[9], out var state)) throw new InvalidDataException("The saved barter offer is invalid.");
        var accepted = string.IsNullOrEmpty(parts[10]) ? [] : parts[10].Split(',', StringSplitOptions.RemoveEmptyEntries).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        return new DirectBarterOffer(parts[0], ParseInt(parts[1]), parts[2], parts[3], parts[4], ParseInt(parts[5]), parts[6], ParseInt(parts[7]), ParseLong(parts[8]), state, accepted);
    }

    private static InventoryEvent ParseEvent(string line)
    {
        var parts = ValueAfterPrefix(line, "event=").Split('|');
        if (parts.Length != 4) throw new InvalidDataException("The saved inventory event is invalid.");
        return new InventoryEvent(ParseLong(parts[0]), ParseLong(parts[1]), parts[2], parts[3]);
    }

    private static string ValueAfterPrefix(string line, string prefix) => line.StartsWith(prefix, StringComparison.Ordinal) ? line[prefix.Length..] : throw new InvalidDataException("The inventory checkpoint line is invalid.");
    private static int ParseInt(string value) => int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result) ? result : throw new InvalidDataException("The saved inventory integer is invalid.");
    private static long ParseLong(string value) => long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result) ? result : throw new InvalidDataException("The saved inventory long is invalid.");
}

public static class InventoryDigest
{
    public static string State(InventoryCheckpoint checkpoint)
    {
        var canonical = Encoding.UTF8.GetString(InventoryCheckpointCodec.Encode(checkpoint));
        return Digest(string.Join('\n', canonical.Split('\n').Where(line => !line.StartsWith("event=", StringComparison.Ordinal))));
    }

    public static string Events(IEnumerable<InventoryEvent> events) => Digest(string.Join('\n', events.OrderBy(item => item.EventId).Select(item => $"{item.EventId}|{item.WorldTick}|{item.Kind}|{item.Detail}")));

    private static string Digest(string canonical) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
}
