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
/// Component B: Supply and Inventory Management - Supply Catalog Items.
/// </summary>
[ApiController]
[Route("api/supply-items")]
[Authorize]
public class SupplyItemsController(AppDbContext db, IAuditService audit) : ControllerBase
{
    // ---------- List Supply Items ----------
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SupplyItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] NeedCategory? category,
        [FromQuery] string? search,
        [FromQuery] bool? activeOnly,
        CancellationToken ct)
    {
        var query = db.SupplyItems.AsNoTracking().AsQueryable();

        if (activeOnly is null or true)
            query = query.Where(i => i.IsActive);

        if (category.HasValue)
            query = query.Where(i => i.Category == category.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(term) || i.Code.ToLower().Contains(term));
        }

        var items = await query
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .Select(i => ToDto(i))
            .ToListAsync(ct);

        return Ok(items);
    }

    // ---------- Get Supply Item By Id ----------
    [HttpGet("{id:guid}")]
    [ProducesResponseType<SupplyItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await db.SupplyItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return NotFound();
        return Ok(ToDto(item));
    }

    // ---------- Create Supply Item ----------
    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<SupplyItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] SupplyItemInput input, CancellationToken ct)
    {
        var errors = InventoryValidator.ValidateSupplyItem(input);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var code = input.Code!.Trim().ToUpperInvariant();
        var exists = await db.SupplyItems.AnyAsync(i => i.Code == code, ct);
        if (exists)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: $"Supply item code '{code}' already exists.");

        var item = new SupplyItem
        {
            Code = code,
            Name = input.Name!.Trim(),
            Description = input.Description?.Trim(),
            Category = input.Category!.Value,
            UnitOfMeasure = input.UnitOfMeasure!.Trim(),
            WeightKg = input.WeightKg ?? 1.0,
            ReorderThreshold = input.ReorderThreshold ?? 50,
            StandardFamilyAllocation = input.StandardFamilyAllocation ?? 1,
            IsActive = true
        };

        db.SupplyItems.Add(item);
        audit.Record(User.GetUserId(), "SupplyItemCreated", "SupplyItem", item.Id.ToString(), new { item.Code, item.Name, Category = item.Category.ToString() });
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, ToDto(item));
    }

    // ---------- Update Supply Item ----------
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<SupplyItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SupplyItemInput input, CancellationToken ct)
    {
        var errors = InventoryValidator.ValidateSupplyItem(input);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var item = await db.SupplyItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return NotFound();

        var code = input.Code!.Trim().ToUpperInvariant();
        if (item.Code != code)
        {
            var conflict = await db.SupplyItems.AnyAsync(i => i.Code == code && i.Id != id, ct);
            if (conflict)
                return Problem(statusCode: StatusCodes.Status409Conflict, title: $"Supply item code '{code}' is taken by another item.");
            item.Code = code;
        }

        item.Name = input.Name!.Trim();
        item.Description = input.Description?.Trim();
        item.Category = input.Category!.Value;
        item.UnitOfMeasure = input.UnitOfMeasure!.Trim();
        if (input.WeightKg.HasValue) item.WeightKg = input.WeightKg.Value;
        if (input.ReorderThreshold.HasValue) item.ReorderThreshold = input.ReorderThreshold.Value;
        if (input.StandardFamilyAllocation.HasValue) item.StandardFamilyAllocation = input.StandardFamilyAllocation.Value;

        audit.Record(User.GetUserId(), "SupplyItemUpdated", "SupplyItem", item.Id.ToString(), new { item.Code, item.Name });
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(item));
    }

    private static SupplyItemDto ToDto(SupplyItem i) => new(
        i.Id,
        i.Code,
        i.Name,
        i.Description,
        i.Category,
        i.UnitOfMeasure,
        i.WeightKg,
        i.ReorderThreshold,
        i.StandardFamilyAllocation,
        i.IsActive,
        i.CreatedAt,
        i.UpdatedAt);
}
