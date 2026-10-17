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
/// Component B: Supply and Inventory Management - Depots & Warehouses.
/// </summary>
[ApiController]
[Route("api/depots")]
[Authorize]
public class DepotsController(AppDbContext db, IAuditService audit) : ControllerBase
{
    // ---------- List Depots ----------
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DepotDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? district,
        [FromQuery] string? search,
        [FromQuery] bool? activeOnly,
        CancellationToken ct)
    {
        var query = db.Depots.AsNoTracking().AsQueryable();

        if (activeOnly is null or true)
            query = query.Where(d => d.IsActive);

        if (!string.IsNullOrWhiteSpace(district))
        {
            var canonical = SriLanka.CanonicalDistrict(district);
            if (canonical is not null)
                query = query.Where(d => d.District == canonical);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(d => d.Name.ToLower().Contains(term) || d.Code.ToLower().Contains(term));
        }

        var depots = await query
            .OrderBy(d => d.District)
            .ThenBy(d => d.Name)
            .Select(d => ToDto(d))
            .ToListAsync(ct);

        return Ok(depots);
    }

    // ---------- Get Depot By Id ----------
    [HttpGet("{id:guid}")]
    [ProducesResponseType<DepotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var depot = await db.Depots.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (depot is null) return NotFound();
        return Ok(ToDto(depot));
    }

    // ---------- Get Depot Stock Summary ----------
    [HttpGet("{id:guid}/summary")]
    [ProducesResponseType<DepotStockSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary(Guid id, CancellationToken ct)
    {
        var depot = await db.Depots
            .AsNoTracking()
            .Include(d => d.StockLevels)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (depot is null) return NotFound();

        var summary = new DepotStockSummaryDto(
            depot.Id,
            depot.Code,
            depot.Name,
            depot.District,
            depot.StockLevels.Count,
            depot.StockLevels.Sum(s => s.QuantityOnHand),
            depot.StockLevels.Sum(s => s.QuantityReserved),
            depot.StockLevels.Sum(s => s.QuantityAvailable));

        return Ok(summary);
    }

    // ---------- Create Depot ----------
    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<DepotDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] DepotInput input, CancellationToken ct)
    {
        var errors = InventoryValidator.ValidateDepot(input);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var code = input.Code!.Trim().ToUpperInvariant();
        var exists = await db.Depots.AnyAsync(d => d.Code == code, ct);
        if (exists)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: $"Depot code '{code}' already exists.");

        var depot = new Depot
        {
            Code = code,
            Name = input.Name!.Trim(),
            District = SriLanka.CanonicalDistrict(input.District)!,
            AddressText = input.AddressText!.Trim(),
            Latitude = input.Latitude ?? 6.9271, // Colombo fallback if omitted
            Longitude = input.Longitude ?? 79.8612,
            ContactPerson = input.ContactPerson!.Trim(),
            ContactPhone = input.ContactPhone!.Trim(),
            CapacitySquareMeters = input.CapacitySquareMeters ?? 100,
            IsActive = true,
            Notes = input.Notes?.Trim()
        };

        db.Depots.Add(depot);
        audit.Record(User.GetUserId(), "DepotCreated", "Depot", depot.Id.ToString(), new { depot.Code, depot.Name, depot.District });
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = depot.Id }, ToDto(depot));
    }

    // ---------- Update Depot ----------
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<DepotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] DepotInput input, CancellationToken ct)
    {
        var errors = InventoryValidator.ValidateDepot(input);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var depot = await db.Depots.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (depot is null) return NotFound();

        var code = input.Code!.Trim().ToUpperInvariant();
        if (depot.Code != code)
        {
            var conflict = await db.Depots.AnyAsync(d => d.Code == code && d.Id != id, ct);
            if (conflict)
                return Problem(statusCode: StatusCodes.Status409Conflict, title: $"Depot code '{code}' is taken by another depot.");
            depot.Code = code;
        }

        depot.Name = input.Name!.Trim();
        depot.District = SriLanka.CanonicalDistrict(input.District)!;
        depot.AddressText = input.AddressText!.Trim();
        if (input.Latitude.HasValue) depot.Latitude = input.Latitude.Value;
        if (input.Longitude.HasValue) depot.Longitude = input.Longitude.Value;
        depot.ContactPerson = input.ContactPerson!.Trim();
        depot.ContactPhone = input.ContactPhone!.Trim();
        if (input.CapacitySquareMeters.HasValue) depot.CapacitySquareMeters = input.CapacitySquareMeters.Value;
        depot.Notes = input.Notes?.Trim();

        audit.Record(User.GetUserId(), "DepotUpdated", "Depot", depot.Id.ToString(), new { depot.Code, depot.Name });
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(depot));
    }

    // ---------- Deactivate Depot ----------
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var depot = await db.Depots.Include(d => d.Reservations).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (depot is null) return NotFound();

        var activeReservations = depot.Reservations.Count(r => r.Status == ReservationStatus.Allocated);
        if (activeReservations > 0)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: $"Cannot deactivate depot '{depot.Name}' because it has {activeReservations} active stock reservation(s).");
        }

        depot.IsActive = false;
        audit.Record(User.GetUserId(), "DepotDeactivated", "Depot", depot.Id.ToString(), new { depot.Code, depot.Name });
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static DepotDto ToDto(Depot d) => new(
        d.Id,
        d.Code,
        d.Name,
        d.District,
        d.AddressText,
        d.Latitude,
        d.Longitude,
        d.ContactPerson,
        d.ContactPhone,
        d.CapacitySquareMeters,
        d.IsActive,
        d.Notes,
        d.CreatedAt,
        d.UpdatedAt);
}
