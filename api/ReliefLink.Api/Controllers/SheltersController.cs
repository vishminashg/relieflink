using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Auth;
using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Data;
using ReliefLink.Api.Domain;
using ReliefLink.Api.Services;

namespace ReliefLink.Api.Controllers;

[ApiController]
[Route("api/shelters")]
[Authorize]
public sealed class SheltersController(AppDbContext db, IAuditService audit, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> List([FromQuery] string? district, [FromQuery] bool? active,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Shelters.Include(x => x.CheckIns).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(district)) query = query.Where(x => x.District == district.Trim());
        if (active.HasValue) query = query.Where(x => x.IsActive == active.Value);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search.Trim()) || x.Address.Contains(search.Trim()));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.District).ThenBy(x => x.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { items = items.Select(ShelterDto.From), total, page, pageSize });
    }

    [HttpGet("nearest")]
    public async Task<IActionResult> Nearest([FromQuery] double latitude, [FromQuery] double longitude,
        [FromQuery] int limit = 5, CancellationToken ct = default)
    {
        if (latitude is < SriLanka.MinLatitude or > SriLanka.MaxLatitude || longitude is < SriLanka.MinLongitude or > SriLanka.MaxLongitude)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Coordinates are outside Sri Lanka.");

        limit = Math.Clamp(limit, 1, 20);
        var shelters = await db.Shelters.Include(x => x.CheckIns).AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var result = shelters.OrderBy(x => DistanceSquared(x.Latitude, x.Longitude, latitude, longitude))
            .Take(limit).Select(ShelterDto.From);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var shelter = await db.Shelters.Include(x => x.CheckIns).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return shelter is null ? NotFound() : Ok(ShelterDto.From(shelter));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Create(ShelterInput input, CancellationToken ct)
    {
        var errors = ShelterValidator.Validate(input);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var shelter = new Shelter { Name = input.Name.Trim(), District = SriLanka.CanonicalDistrict(input.District)!,
            Address = input.Address.Trim(), Latitude = input.Latitude, Longitude = input.Longitude,
            Capacity = input.Capacity, IsActive = input.IsActive, Notes = input.Notes?.Trim() };
        db.Shelters.Add(shelter);
        var userId = User.GetUserId();
        audit.Record(userId, "shelter.created", nameof(Shelter), shelter.Id.ToString(), new { shelter.Name, shelter.District });
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = shelter.Id }, ShelterDto.From(shelter));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Update(Guid id, ShelterInput input, CancellationToken ct)
    {
        var errors = ShelterValidator.Validate(input);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));
        var shelter = await db.Shelters.Include(x => x.CheckIns).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (shelter is null) return NotFound();
        var occupied = ShelterCapacityRules.OccupiedSpaces(shelter.CheckIns);
        if (input.Capacity < occupied) return Problem(statusCode: StatusCodes.Status409Conflict, title: "Capacity cannot be below current occupancy.");

        shelter.Name = input.Name.Trim(); shelter.District = SriLanka.CanonicalDistrict(input.District)!; shelter.Address = input.Address.Trim();
        shelter.Latitude = input.Latitude; shelter.Longitude = input.Longitude; shelter.Capacity = input.Capacity;
        shelter.IsActive = input.IsActive; shelter.Notes = input.Notes?.Trim();
        audit.Record(User.GetUserId(), "shelter.updated", nameof(Shelter), shelter.Id.ToString());
        await db.SaveChangesAsync(ct);
        return Ok(ShelterDto.From(shelter));
    }

    [HttpPost("{id:guid}/check-in")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> CheckIn(Guid id, ShelterCheckInInput input, CancellationToken ct)
    {
        var errors = ShelterValidator.ValidateCheckIn(input);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var shelter = await db.Shelters.Include(x => x.CheckIns).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (shelter is null) return NotFound();
        if (!shelter.IsActive) return Problem(statusCode: StatusCodes.Status409Conflict, title: "Shelter is inactive.");
        if (!ShelterCapacityRules.CanCheckIn(shelter.Capacity, ShelterCapacityRules.OccupiedSpaces(shelter.CheckIns), input.HouseholdSize))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Shelter does not have enough capacity.");
        if (input.ReliefRequestId.HasValue && !await db.ReliefRequests.AnyAsync(x => x.Id == input.ReliefRequestId, ct)) return NotFound();

        var checkIn = new ShelterCheckIn { ShelterId = id, ReliefRequestId = input.ReliefRequestId, HouseholdSize = input.HouseholdSize,
            CheckedInAt = clock.GetUtcNow().UtcDateTime, CheckedInByUserId = User.GetUserId()!.Value };
        db.ShelterCheckIns.Add(checkIn);
        audit.Record(User.GetUserId(), "shelter.checked_in", nameof(ShelterCheckIn), checkIn.Id.ToString(), new { shelterId = id, input.HouseholdSize });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Created($"/api/shelters/{id}/check-ins/{checkIn.Id}", ToDto(checkIn));
    }

    [HttpPost("{id:guid}/check-out/{checkInId:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> CheckOut(Guid id, Guid checkInId, CancellationToken ct)
    {
        var checkIn = await db.ShelterCheckIns.FirstOrDefaultAsync(x => x.Id == checkInId && x.ShelterId == id, ct);
        if (checkIn is null) return NotFound();
        if (!checkIn.IsActive) return Problem(statusCode: StatusCodes.Status409Conflict, title: "Check-in is already closed.");
        checkIn.CheckedOutAt = clock.GetUtcNow().UtcDateTime;
        audit.Record(User.GetUserId(), "shelter.checked_out", nameof(ShelterCheckIn), checkIn.Id.ToString());
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(checkIn));
    }

    private static ShelterCheckInDto ToDto(ShelterCheckIn x) => new(x.Id, x.ShelterId, x.ReliefRequestId, x.HouseholdSize, x.CheckedInAt, x.CheckedOutAt, x.IsActive);
    private static double DistanceSquared(double aLat, double aLon, double bLat, double bLon) => Math.Pow(aLat - bLat, 2) + Math.Pow(aLon - bLon, 2);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var shelter = await db.Shelters.Include(x => x.CheckIns).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (shelter is null) return NotFound();
        if (ShelterCapacityRules.OccupiedSpaces(shelter.CheckIns) > 0)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Cannot deactivate a shelter with active check-ins.");
        shelter.IsActive = false;
        audit.Record(User.GetUserId(), "shelter.deactivated", nameof(Shelter), shelter.Id.ToString());
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
