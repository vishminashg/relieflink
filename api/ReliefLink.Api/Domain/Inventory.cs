namespace ReliefLink.Api.Domain;

public enum ReservationStatus
{
    Pending,
    Allocated,
    Fulfilled,
    Cancelled,
    Expired
}

public enum StockTransactionType
{
    Receive,
    Adjust,
    Reserve,
    ReleaseReservation,
    Dispatch,
    Transfer
}

/// <summary>
/// A relief supply depot or warehouse.
/// Owned by Component B (Supply and Inventory Management).
/// </summary>
public class Depot : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string AddressText { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string ContactPerson { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public double CapacitySquareMeters { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public List<StockLevel> StockLevels { get; set; } = new();
    public List<StockReservation> Reservations { get; set; } = new();
    public List<StockTransaction> Transactions { get; set; } = new();
}

/// <summary>
/// Catalog definition for a physical supply item.
/// </summary>
public class SupplyItem : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public NeedCategory Category { get; set; }
    public string UnitOfMeasure { get; set; } = "Unit";
    public double WeightKg { get; set; }
    public int ReorderThreshold { get; set; } = 50;
    public int StandardFamilyAllocation { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public List<StockLevel> StockLevels { get; set; } = new();
}

/// <summary>
/// Stock tracking per item per depot.
/// Guarantees: QuantityOnHand >= 0, QuantityReserved >= 0, and QuantityOnHand >= QuantityReserved.
/// </summary>
public class StockLevel : BaseEntity
{
    public Guid DepotId { get; set; }
    public Depot? Depot { get; set; }

    public Guid SupplyItemId { get; set; }
    public SupplyItem? SupplyItem { get; set; }

    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }

    /// <summary>Available stock ready for new relief missions or immediate dispatch.</summary>
    public int QuantityAvailable => Math.Max(0, QuantityOnHand - QuantityReserved);

    public DateTime? LastRestockedAt { get; set; }
    public string? StorageBin { get; set; }
}

/// <summary>
/// Transactional stock reservation.
/// Enforces that stock is reserved atomically and cannot be double-allocated.
/// </summary>
public class StockReservation : BaseEntity
{
    public string ReservationNumber { get; set; } = string.Empty;

    public Guid DepotId { get; set; }
    public Depot? Depot { get; set; }

    public Guid SupplyItemId { get; set; }
    public SupplyItem? SupplyItem { get; set; }

    public int Quantity { get; set; }

    /// <summary>Optional link to the relief request this stock is allocated for.</summary>
    public Guid? ReliefRequestId { get; set; }
    public ReliefRequest? ReliefRequest { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Allocated;

    public Guid ReservedByUserId { get; set; }
    public User? ReservedByUser { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime? FulfilledAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Immutable audit log for any physical inventory movement or adjustment.
/// </summary>
public class StockTransaction : BaseEntity
{
    public Guid DepotId { get; set; }
    public Depot? Depot { get; set; }

    public Guid SupplyItemId { get; set; }
    public SupplyItem? SupplyItem { get; set; }

    public StockTransactionType Type { get; set; }
    public int QuantityChanged { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }

    public string? ReferenceNumber { get; set; }
    public Guid PerformedByUserId { get; set; }
    public User? PerformedByUser { get; set; }

    public string? Notes { get; set; }
}
