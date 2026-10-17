using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Controllers.Dtos;

// ---------- Depots ----------

public record DepotDto(
    Guid Id,
    string Code,
    string Name,
    string District,
    string AddressText,
    double Latitude,
    double Longitude,
    string ContactPerson,
    string ContactPhone,
    double CapacitySquareMeters,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record DepotInput(
    string? Code,
    string? Name,
    string? District,
    string? AddressText,
    double? Latitude,
    double? Longitude,
    string? ContactPerson,
    string? ContactPhone,
    double? CapacitySquareMeters,
    string? Notes);

public record DepotStockSummaryDto(
    Guid DepotId,
    string DepotCode,
    string DepotName,
    string District,
    int TotalItemTypes,
    int TotalQuantityOnHand,
    int TotalQuantityReserved,
    int TotalQuantityAvailable);

// ---------- Supply Items ----------

public record SupplyItemDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    NeedCategory Category,
    string UnitOfMeasure,
    double WeightKg,
    int ReorderThreshold,
    int StandardFamilyAllocation,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SupplyItemInput(
    string? Code,
    string? Name,
    string? Description,
    NeedCategory? Category,
    string? UnitOfMeasure,
    double? WeightKg,
    int? ReorderThreshold,
    int? StandardFamilyAllocation);

// ---------- Stock Levels ----------

public record StockLevelDto(
    Guid Id,
    Guid DepotId,
    string DepotCode,
    string DepotName,
    string DepotDistrict,
    Guid SupplyItemId,
    string ItemCode,
    string ItemName,
    NeedCategory Category,
    string UnitOfMeasure,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    bool IsLowStock,
    DateTime? LastRestockedAt,
    string? StorageBin);

public record ReceiveStockInput(
    Guid DepotId,
    Guid SupplyItemId,
    int Quantity,
    string? ReferenceNumber,
    string? StorageBin,
    string? Notes);

public record AdjustStockInput(
    Guid DepotId,
    Guid SupplyItemId,
    int NewOnHand,
    string? Reason);

// ---------- Reservations ----------

public record CreateReservationInput(
    Guid DepotId,
    Guid SupplyItemId,
    int Quantity,
    Guid? ReliefRequestId,
    int? HoldHours,
    string? Notes);

public record StockReservationDto(
    Guid Id,
    string ReservationNumber,
    Guid DepotId,
    string DepotName,
    Guid SupplyItemId,
    string ItemName,
    int Quantity,
    Guid? ReliefRequestId,
    ReservationStatus Status,
    Guid ReservedByUserId,
    DateTime ExpiresAt,
    DateTime? FulfilledAt,
    DateTime? CancelledAt,
    string? Notes,
    DateTime CreatedAt);

// ---------- Queries & Dashboards ----------

public record DepotStockOptionDto(
    Guid DepotId,
    string DepotName,
    string District,
    int ProximityScore,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable);

public record StockAvailabilityDto(
    Guid SupplyItemId,
    string ItemCode,
    string ItemName,
    NeedCategory Category,
    int TotalAvailable,
    List<DepotStockOptionDto> Depots);

public record LowStockAlertDto(
    Guid DepotId,
    string DepotName,
    string District,
    Guid SupplyItemId,
    string ItemCode,
    string ItemName,
    NeedCategory Category,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    int ReorderThreshold,
    int Shortage);

public record InventorySummaryDto(
    int TotalDepots,
    int TotalSupplyItems,
    int TotalStockOnHand,
    int TotalStockReserved,
    int TotalStockAvailable,
    int LowStockAlertsCount,
    int ActiveReservationsCount);
