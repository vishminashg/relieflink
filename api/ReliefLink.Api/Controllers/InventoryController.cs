using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Auth;
using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Data;
using ReliefLink.Api.Domain;
using ReliefLink.Api.Services;

namespace ReliefLink.Api.Controllers;

/// <summary>
/// Component B: Supply and Inventory Management - Stock, Reservations, and Availability.
/// Implements transactional stock reservation with strict guarantees against negative stock.
/// </summary>
[ApiController]
[Authorize]
public class InventoryController(AppDbContext db, IAuditService audit) : ControllerBase
{
    // ==========================================
    // 1. Stock Queries & Summaries
    // ==========================================

    [HttpGet("api/stock")]
    [ProducesResponseType<IReadOnlyList<StockLevelDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListStock(
        [FromQuery] Guid? depotId,
        [FromQuery] Guid? supplyItemId,
        [FromQuery] NeedCategory? category,
        [FromQuery] bool? isLowStock,
        CancellationToken ct)
    {
        var query = db.StockLevels
            .AsNoTracking()
            .Include(s => s.Depot)
            .Include(s => s.SupplyItem)
            .AsQueryable();

        if (depotId.HasValue)
            query = query.Where(s => s.DepotId == depotId.Value);

        if (supplyItemId.HasValue)
            query = query.Where(s => s.SupplyItemId == supplyItemId.Value);

        if (category.HasValue)
            query = query.Where(s => s.SupplyItem != null && s.SupplyItem.Category == category.Value);

        var list = await query
            .OrderBy(s => s.Depot!.District)
            .ThenBy(s => s.SupplyItem!.Name)
            .ToListAsync(ct);

        var dtos = list
            .Where(s => !isLowStock.HasValue || !isLowStock.Value || (s.SupplyItem != null && InventoryManager.IsLowStock(s.QuantityAvailable, s.SupplyItem.ReorderThreshold)))
            .Select(s => ToStockDto(s))
            .ToList();

        return Ok(dtos);
    }

    [HttpGet("api/inventory/availability")]
    [ProducesResponseType<IReadOnlyList<StockAvailabilityDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckAvailability(
        [FromQuery] Guid? supplyItemId,
        [FromQuery] NeedCategory? category,
        [FromQuery] string? district,
        CancellationToken ct)
    {
        var itemsQuery = db.SupplyItems
            .AsNoTracking()
            .Include(i => i.StockLevels)
                .ThenInclude(s => s.Depot)
            .Where(i => i.IsActive)
            .AsQueryable();

        if (supplyItemId.HasValue)
            itemsQuery = itemsQuery.Where(i => i.Id == supplyItemId.Value);

        if (category.HasValue)
            itemsQuery = itemsQuery.Where(i => i.Category == category.Value);

        var items = await itemsQuery.ToListAsync(ct);

        var results = new List<StockAvailabilityDto>();
        foreach (var item in items)
        {
            var options = new List<DepotStockOptionDto>();
            foreach (var stock in item.StockLevels.Where(s => s.Depot != null && s.Depot.IsActive))
            {
                var proximity = InventoryManager.ScoreDistrictProximity(stock.Depot!.District, district);
                options.Add(new DepotStockOptionDto(
                    stock.Depot.Id,
                    stock.Depot.Name,
                    stock.Depot.District,
                    proximity,
                    stock.QuantityOnHand,
                    stock.QuantityReserved,
                    stock.QuantityAvailable));
            }

            options = options.OrderByDescending(o => o.ProximityScore).ThenByDescending(o => o.QuantityAvailable).ToList();

            results.Add(new StockAvailabilityDto(
                item.Id,
                item.Code,
                item.Name,
                item.Category,
                options.Sum(o => o.QuantityAvailable),
                options));
        }

        return Ok(results);
    }

    [HttpGet("api/inventory/low-stock")]
    [ProducesResponseType<IReadOnlyList<LowStockAlertDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStockAlerts(CancellationToken ct)
    {
        var stockList = await db.StockLevels
            .AsNoTracking()
            .Include(s => s.Depot)
            .Include(s => s.SupplyItem)
            .Where(s => s.Depot != null && s.Depot.IsActive && s.SupplyItem != null && s.SupplyItem.IsActive)
            .ToListAsync(ct);

        var alerts = new List<LowStockAlertDto>();
        foreach (var s in stockList)
        {
            var threshold = s.SupplyItem!.ReorderThreshold;
            if (InventoryManager.IsLowStock(s.QuantityAvailable, threshold))
            {
                alerts.Add(new LowStockAlertDto(
                    s.DepotId,
                    s.Depot!.Name,
                    s.Depot.District,
                    s.SupplyItemId,
                    s.SupplyItem.Code,
                    s.SupplyItem.Name,
                    s.SupplyItem.Category,
                    s.QuantityOnHand,
                    s.QuantityReserved,
                    s.QuantityAvailable,
                    threshold,
                    Math.Max(0, threshold - s.QuantityAvailable)));
            }
        }

        return Ok(alerts.OrderByDescending(a => a.Shortage).ToList());
    }

    [HttpGet("api/inventory/summary")]
    [ProducesResponseType<InventorySummaryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var totalDepots = await db.Depots.CountAsync(d => d.IsActive, ct);
        var totalItems = await db.SupplyItems.CountAsync(i => i.IsActive, ct);

        var stock = await db.StockLevels
            .AsNoTracking()
            .Include(s => s.SupplyItem)
            .ToListAsync(ct);

        var totalOnHand = stock.Sum(s => s.QuantityOnHand);
        var totalReserved = stock.Sum(s => s.QuantityReserved);
        var totalAvailable = stock.Sum(s => s.QuantityAvailable);
        var lowStockCount = stock.Count(s => s.SupplyItem != null && InventoryManager.IsLowStock(s.QuantityAvailable, s.SupplyItem.ReorderThreshold));
        var activeReservations = await db.StockReservations.CountAsync(r => r.Status == ReservationStatus.Allocated, ct);

        return Ok(new InventorySummaryDto(
            totalDepots,
            totalItems,
            totalOnHand,
            totalReserved,
            totalAvailable,
            lowStockCount,
            activeReservations));
    }

    // ==========================================
    // 2. Physical Stock Movements (Receive / Adjust)
    // ==========================================

    [HttpPost("api/stock/receive")]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<StockLevelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReceiveStock([FromBody] ReceiveStockInput input, CancellationToken ct)
    {
        var errors = InventoryValidator.ValidateReceiveStock(input);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var depot = await db.Depots.FirstOrDefaultAsync(d => d.Id == input.DepotId, ct);
        if (depot is null) return NotFound($"Depot with ID '{input.DepotId}' not found.");

        var item = await db.SupplyItems.FirstOrDefaultAsync(i => i.Id == input.SupplyItemId, ct);
        if (item is null) return NotFound($"Supply item with ID '{input.SupplyItemId}' not found.");

        var currentUserId = User.GetUserId();
        if (currentUserId is null) return Unauthorized();

        var stock = await db.StockLevels
            .Include(s => s.Depot)
            .Include(s => s.SupplyItem)
            .FirstOrDefaultAsync(s => s.DepotId == input.DepotId && s.SupplyItemId == input.SupplyItemId, ct);

        var beforeOnHand = stock?.QuantityOnHand ?? 0;

        if (stock is null)
        {
            stock = new StockLevel
            {
                DepotId = input.DepotId,
                SupplyItemId = input.SupplyItemId,
                QuantityOnHand = 0,
                QuantityReserved = 0,
                StorageBin = input.StorageBin?.Trim()
            };
            db.StockLevels.Add(stock);
        }
        else if (!string.IsNullOrWhiteSpace(input.StorageBin))
        {
            stock.StorageBin = input.StorageBin.Trim();
        }

        var now = DateTime.UtcNow;
        InventoryManager.ReceiveStock(stock, input.Quantity, now);

        var transaction = new StockTransaction
        {
            DepotId = input.DepotId,
            SupplyItemId = input.SupplyItemId,
            Type = StockTransactionType.Receive,
            QuantityChanged = input.Quantity,
            QuantityBefore = beforeOnHand,
            QuantityAfter = stock.QuantityOnHand,
            ReferenceNumber = input.ReferenceNumber?.Trim(),
            PerformedByUserId = currentUserId.Value,
            Notes = input.Notes?.Trim()
        };
        db.StockTransactions.Add(transaction);

        audit.Record(currentUserId, "StockReceived", "StockLevel", stock.Id.ToString(), new
        {
            DepotCode = depot.Code,
            ItemCode = item.Code,
            QuantityReceived = input.Quantity,
            NewOnHand = stock.QuantityOnHand
        });

        await db.SaveChangesAsync(ct);

        return Ok(ToStockDto(stock));
    }

    [HttpPost("api/stock/adjust")]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<StockLevelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdjustStock([FromBody] AdjustStockInput input, CancellationToken ct)
    {
        if (input.NewOnHand < 0)
            return BadRequest("New on-hand quantity cannot be negative.");

        var stock = await db.StockLevels
            .Include(s => s.Depot)
            .Include(s => s.SupplyItem)
            .FirstOrDefaultAsync(s => s.DepotId == input.DepotId && s.SupplyItemId == input.SupplyItemId, ct);

        if (stock is null) return NotFound("Stock level not found for the specified depot and item.");

        var currentUserId = User.GetUserId();
        if (currentUserId is null) return Unauthorized();

        var beforeOnHand = stock.QuantityOnHand;

        try
        {
            InventoryManager.AdjustStock(stock, input.NewOnHand);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Message);
        }

        var transaction = new StockTransaction
        {
            DepotId = input.DepotId,
            SupplyItemId = input.SupplyItemId,
            Type = StockTransactionType.Adjust,
            QuantityChanged = stock.QuantityOnHand - beforeOnHand,
            QuantityBefore = beforeOnHand,
            QuantityAfter = stock.QuantityOnHand,
            PerformedByUserId = currentUserId.Value,
            Notes = input.Reason?.Trim()
        };
        db.StockTransactions.Add(transaction);

        audit.Record(currentUserId, "StockAdjusted", "StockLevel", stock.Id.ToString(), new
        {
            DepotCode = stock.Depot!.Code,
            ItemCode = stock.SupplyItem!.Code,
            Before = beforeOnHand,
            After = stock.QuantityOnHand,
            Reason = input.Reason
        });

        await db.SaveChangesAsync(ct);

        return Ok(ToStockDto(stock));
    }

    // ==========================================
    // 3. Stock Reservations (Core Business Operation)
    // ==========================================

    [HttpPost("api/reservations")]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<StockReservationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationInput input, CancellationToken ct)
    {
        var errors = InventoryValidator.ValidateReservation(input);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var currentUserId = User.GetUserId();
        if (currentUserId is null) return Unauthorized();

        // Atomically query stock level
        var stock = await db.StockLevels
            .Include(s => s.Depot)
            .Include(s => s.SupplyItem)
            .FirstOrDefaultAsync(s => s.DepotId == input.DepotId && s.SupplyItemId == input.SupplyItemId, ct);

        if (stock is null)
            return NotFound("No inventory record exists for this item at the selected depot.");

        var hold = input.HoldHours.HasValue ? TimeSpan.FromHours(input.HoldHours.Value) : (TimeSpan?)null;

        StockReservation reservation;
        try
        {
            reservation = InventoryManager.AllocateReservation(stock, input.Quantity, currentUserId.Value, input.ReliefRequestId, hold, input.Notes);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Message);
        }

        db.StockReservations.Add(reservation);

        var transaction = new StockTransaction
        {
            DepotId = input.DepotId,
            SupplyItemId = input.SupplyItemId,
            Type = StockTransactionType.Reserve,
            QuantityChanged = input.Quantity,
            QuantityBefore = stock.QuantityOnHand,
            QuantityAfter = stock.QuantityOnHand, // Reserved increases, physical on-hand stays until dispatch
            ReferenceNumber = reservation.ReservationNumber,
            PerformedByUserId = currentUserId.Value,
            Notes = $"Reserved for mission. ReliefRequestId: {input.ReliefRequestId}"
        };
        db.StockTransactions.Add(transaction);

        audit.Record(currentUserId, "StockReserved", "StockReservation", reservation.Id.ToString(), new
        {
            reservation.ReservationNumber,
            DepotCode = stock.Depot!.Code,
            ItemCode = stock.SupplyItem!.Code,
            reservation.Quantity,
            reservation.ReliefRequestId
        });

        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetReservationById), new { id = reservation.Id }, ToReservationDto(reservation, stock.Depot.Name, stock.SupplyItem.Name));
    }

    [HttpGet("api/reservations/{id:guid}")]
    [ProducesResponseType<StockReservationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReservationById(Guid id, CancellationToken ct)
    {
        var res = await db.StockReservations
            .AsNoTracking()
            .Include(r => r.Depot)
            .Include(r => r.SupplyItem)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (res is null) return NotFound();
        return Ok(ToReservationDto(res, res.Depot?.Name ?? "", res.SupplyItem?.Name ?? ""));
    }

    [HttpGet("api/reservations")]
    [ProducesResponseType<IReadOnlyList<StockReservationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListReservations(
        [FromQuery] Guid? depotId,
        [FromQuery] Guid? reliefRequestId,
        [FromQuery] ReservationStatus? status,
        CancellationToken ct)
    {
        var query = db.StockReservations
            .AsNoTracking()
            .Include(r => r.Depot)
            .Include(r => r.SupplyItem)
            .AsQueryable();

        if (depotId.HasValue)
            query = query.Where(r => r.DepotId == depotId.Value);

        if (reliefRequestId.HasValue)
            query = query.Where(r => r.ReliefRequestId == reliefRequestId.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var list = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return Ok(list.Select(r => ToReservationDto(r, r.Depot?.Name ?? "", r.SupplyItem?.Name ?? "")).ToList());
    }

    [HttpPost("api/reservations/{id:guid}/fulfill")]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<StockReservationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> FulfillReservation(Guid id, [FromQuery] string? notes, CancellationToken ct)
    {
        var reservation = await db.StockReservations
            .Include(r => r.Depot)
            .Include(r => r.SupplyItem)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (reservation is null) return NotFound();

        var stock = await db.StockLevels
            .FirstOrDefaultAsync(s => s.DepotId == reservation.DepotId && s.SupplyItemId == reservation.SupplyItemId, ct);

        if (stock is null) return NotFound("Associated stock level not found.");

        var currentUserId = User.GetUserId();
        if (currentUserId is null) return Unauthorized();

        var beforeOnHand = stock.QuantityOnHand;

        try
        {
            InventoryManager.FulfillReservation(reservation, stock, DateTime.UtcNow, notes);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Message);
        }

        var transaction = new StockTransaction
        {
            DepotId = reservation.DepotId,
            SupplyItemId = reservation.SupplyItemId,
            Type = StockTransactionType.Dispatch,
            QuantityChanged = -reservation.Quantity,
            QuantityBefore = beforeOnHand,
            QuantityAfter = stock.QuantityOnHand,
            ReferenceNumber = reservation.ReservationNumber,
            PerformedByUserId = currentUserId.Value,
            Notes = $"Fulfillment dispatch. {notes}"
        };
        db.StockTransactions.Add(transaction);

        audit.Record(currentUserId, "ReservationFulfilled", "StockReservation", reservation.Id.ToString(), new
        {
            reservation.ReservationNumber,
            reservation.Quantity,
            RemainingOnHand = stock.QuantityOnHand
        });

        await db.SaveChangesAsync(ct);

        return Ok(ToReservationDto(reservation, reservation.Depot?.Name ?? "", reservation.SupplyItem?.Name ?? ""));
    }

    [HttpPost("api/reservations/{id:guid}/cancel")]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<StockReservationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelReservation(Guid id, [FromQuery] string? reason, CancellationToken ct)
    {
        var reservation = await db.StockReservations
            .Include(r => r.Depot)
            .Include(r => r.SupplyItem)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (reservation is null) return NotFound();

        var stock = await db.StockLevels
            .FirstOrDefaultAsync(s => s.DepotId == reservation.DepotId && s.SupplyItemId == reservation.SupplyItemId, ct);

        if (stock is null) return NotFound("Associated stock level not found.");

        var currentUserId = User.GetUserId();
        if (currentUserId is null) return Unauthorized();

        try
        {
            InventoryManager.ReleaseReservation(reservation, stock, DateTime.UtcNow, reason);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Message);
        }

        var transaction = new StockTransaction
        {
            DepotId = reservation.DepotId,
            SupplyItemId = reservation.SupplyItemId,
            Type = StockTransactionType.ReleaseReservation,
            QuantityChanged = reservation.Quantity,
            QuantityBefore = stock.QuantityOnHand,
            QuantityAfter = stock.QuantityOnHand,
            ReferenceNumber = reservation.ReservationNumber,
            PerformedByUserId = currentUserId.Value,
            Notes = $"Reservation cancelled: {reason}"
        };
        db.StockTransactions.Add(transaction);

        audit.Record(currentUserId, "ReservationCancelled", "StockReservation", reservation.Id.ToString(), new
        {
            reservation.ReservationNumber,
            reservation.Quantity,
            Reason = reason
        });

        await db.SaveChangesAsync(ct);

        return Ok(ToReservationDto(reservation, reservation.Depot?.Name ?? "", reservation.SupplyItem?.Name ?? ""));
    }

    private static StockLevelDto ToStockDto(StockLevel s) => new(
        s.Id,
        s.DepotId,
        s.Depot?.Code ?? "",
        s.Depot?.Name ?? "",
        s.Depot?.District ?? "",
        s.SupplyItemId,
        s.SupplyItem?.Code ?? "",
        s.SupplyItem?.Name ?? "",
        s.SupplyItem?.Category ?? NeedCategory.Food,
        s.SupplyItem?.UnitOfMeasure ?? "",
        s.QuantityOnHand,
        s.QuantityReserved,
        s.QuantityAvailable,
        s.SupplyItem != null && InventoryManager.IsLowStock(s.QuantityAvailable, s.SupplyItem.ReorderThreshold),
        s.LastRestockedAt,
        s.StorageBin);

    private static StockReservationDto ToReservationDto(StockReservation r, string depotName, string itemName) => new(
        r.Id,
        r.ReservationNumber,
        r.DepotId,
        depotName,
        r.SupplyItemId,
        itemName,
        r.Quantity,
        r.ReliefRequestId,
        r.Status,
        r.ReservedByUserId,
        r.ExpiresAt,
        r.FulfilledAt,
        r.CancelledAt,
        r.Notes,
        r.CreatedAt);
}
