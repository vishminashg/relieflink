namespace ReliefLink.Api.Domain;

/// <summary>
/// Pure business logic for inventory management.
/// Guarantees that stock levels are never negative and that reservations are consistent.
/// </summary>
public static class InventoryManager
{
    private static readonly Dictionary<string, HashSet<string>> DistrictAdjacency = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kalutara"] = new(StringComparer.OrdinalIgnoreCase) { "Colombo", "Ratnapura", "Galle" },
        ["Colombo"] = new(StringComparer.OrdinalIgnoreCase) { "Gampaha", "Kalutara", "Kegalle" },
        ["Gampaha"] = new(StringComparer.OrdinalIgnoreCase) { "Colombo", "Kegalle", "Kurunegala", "Puttalam" },
        ["Ratnapura"] = new(StringComparer.OrdinalIgnoreCase) { "Kalutara", "Colombo", "Kegalle", "Nuwara Eliya", "Badulla", "Monaragala", "Matara", "Galle" },
        ["Galle"] = new(StringComparer.OrdinalIgnoreCase) { "Kalutara", "Ratnapura", "Matara" },
        ["Matara"] = new(StringComparer.OrdinalIgnoreCase) { "Galle", "Ratnapura", "Hambantota" },
        ["Hambantota"] = new(StringComparer.OrdinalIgnoreCase) { "Matara", "Ratnapura", "Monaragala" },
        ["Kegalle"] = new(StringComparer.OrdinalIgnoreCase) { "Gampaha", "Colombo", "Ratnapura", "Kandy", "Kurunegala", "Nuwara Eliya" },
        ["Kandy"] = new(StringComparer.OrdinalIgnoreCase) { "Kegalle", "Kurunegala", "Matale", "Badulla", "Nuwara Eliya" },
        ["Kurunegala"] = new(StringComparer.OrdinalIgnoreCase) { "Gampaha", "Kegalle", "Kandy", "Matale", "Anuradhapura", "Puttalam" }
    };

    /// <summary>
    /// Transactionally reserve stock from a depot.
    /// Strictly guarantees that QuantityOnHand >= QuantityReserved.
    /// </summary>
    public static StockReservation AllocateReservation(
        StockLevel stock,
        int requestedQty,
        Guid reservedByUserId,
        Guid? reliefRequestId = null,
        TimeSpan? holdDuration = null,
        string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(stock);
        if (requestedQty <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedQty), "Requested reservation quantity must be greater than zero.");

        var available = stock.QuantityAvailable;
        if (available < requestedQty)
        {
            throw new InvalidOperationException(
                $"Insufficient available stock. Requested: {requestedQty}, Available: {available} (On hand: {stock.QuantityOnHand}, Reserved: {stock.QuantityReserved}).");
        }

        // Apply reservation
        stock.QuantityReserved += requestedQty;

        var reservation = new StockReservation
        {
            ReservationNumber = $"RES-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            DepotId = stock.DepotId,
            SupplyItemId = stock.SupplyItemId,
            Quantity = requestedQty,
            ReliefRequestId = reliefRequestId,
            Status = ReservationStatus.Allocated,
            ReservedByUserId = reservedByUserId,
            ExpiresAt = DateTime.UtcNow.Add(holdDuration ?? TimeSpan.FromHours(48)),
            Notes = notes
        };

        return reservation;
    }

    /// <summary>
    /// Cancel or release a reservation back to the available inventory pool.
    /// </summary>
    public static void ReleaseReservation(StockReservation reservation, StockLevel stock, DateTime now, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(stock);

        if (reservation.Status != ReservationStatus.Allocated && reservation.Status != ReservationStatus.Pending)
            throw new InvalidOperationException($"Cannot release reservation with status '{reservation.Status}'.");

        if (stock.QuantityReserved < reservation.Quantity)
        {
            // Defensive recovery to keep non-negative invariant
            stock.QuantityReserved = 0;
        }
        else
        {
            stock.QuantityReserved -= reservation.Quantity;
        }

        reservation.Status = ReservationStatus.Cancelled;
        reservation.CancelledAt = now;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            reservation.Notes = string.IsNullOrWhiteSpace(reservation.Notes)
                ? $"Cancelled: {reason}"
                : $"{reservation.Notes} | Cancelled: {reason}";
        }
    }

    /// <summary>
    /// Fulfill a reservation upon dispatching the physical goods.
    /// Deducts both on-hand and reserved quantity.
    /// </summary>
    public static void FulfillReservation(StockReservation reservation, StockLevel stock, DateTime now, string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(stock);

        if (reservation.Status != ReservationStatus.Allocated)
            throw new InvalidOperationException($"Cannot fulfill reservation with status '{reservation.Status}'. Only 'Allocated' reservations can be fulfilled.");

        if (stock.QuantityOnHand < reservation.Quantity)
            throw new InvalidOperationException($"Physical stock shortage. On hand: {stock.QuantityOnHand}, but reservation is for: {reservation.Quantity}.");

        stock.QuantityOnHand -= reservation.Quantity;
        stock.QuantityReserved = Math.Max(0, stock.QuantityReserved - reservation.Quantity);

        reservation.Status = ReservationStatus.Fulfilled;
        reservation.FulfilledAt = now;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            reservation.Notes = string.IsNullOrWhiteSpace(reservation.Notes)
                ? $"Fulfilled: {notes}"
                : $"{reservation.Notes} | Fulfilled: {notes}";
        }
    }

    /// <summary>
    /// Receive new physical stock into a depot.
    /// </summary>
    public static void ReceiveStock(StockLevel stock, int receivedQty, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(stock);
        if (receivedQty <= 0)
            throw new ArgumentOutOfRangeException(nameof(receivedQty), "Received quantity must be greater than zero.");

        stock.QuantityOnHand += receivedQty;
        stock.LastRestockedAt = now;
    }

    /// <summary>
    /// Adjust physical count (e.g., following an audit / stock count).
    /// Prevents setting on-hand stock below already reserved quantities.
    /// </summary>
    public static void AdjustStock(StockLevel stock, int newOnHand)
    {
        ArgumentNullException.ThrowIfNull(stock);
        if (newOnHand < 0)
            throw new ArgumentOutOfRangeException(nameof(newOnHand), "On-hand stock cannot be negative.");

        if (newOnHand < stock.QuantityReserved)
            throw new InvalidOperationException(
                $"Cannot reduce on-hand stock ({newOnHand}) below active reservations ({stock.QuantityReserved}). Please cancel or reassign reservations first.");

        stock.QuantityOnHand = newOnHand;
    }

    /// <summary>
    /// Checks whether an item at a depot has reached or dropped below its reorder threshold.
    /// </summary>
    public static bool IsLowStock(int availableQty, int reorderThreshold)
    {
        return availableQty <= reorderThreshold;
    }

    /// <summary>
    /// Proximity score (0-100) between depot district and requested incident district.
    /// </summary>
    public static int ScoreDistrictProximity(string? depotDistrict, string? targetDistrict)
    {
        if (string.IsNullOrWhiteSpace(depotDistrict) || string.IsNullOrWhiteSpace(targetDistrict))
            return 20;

        var d = depotDistrict.Trim();
        var t = targetDistrict.Trim();

        if (d.Equals(t, StringComparison.OrdinalIgnoreCase))
            return 100;

        if (DistrictAdjacency.TryGetValue(t, out var neighbors) && neighbors.Contains(d))
            return 70;

        return 30;
    }
}
