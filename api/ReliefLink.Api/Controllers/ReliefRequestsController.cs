using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Auth;
using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Data;
using ReliefLink.Api.Domain;
using ReliefLink.Api.Services;

namespace ReliefLink.Api.Controllers;

/// <summary>Component A: Relief Request Management.</summary>
[ApiController]
[Route("api/relief-requests")]
[Authorize]
public class ReliefRequestsController(AppDbContext db, IAuditService audit, TimeProvider clock) : ControllerBase
{
    private bool IsStaff => User.IsInRole(Roles.Coordinator) || User.IsInRole(Roles.Admin);

    private static readonly RequestStatus[] ClosedStatuses =
        [RequestStatus.Fulfilled, RequestStatus.Rejected, RequestStatus.Cancelled];

    // ---------- Create ----------
    [HttpPost]
    [Authorize(Roles = Roles.Citizen)]
    [ProducesResponseType<ReliefRequestDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ReliefRequestInput input, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var errors = ReliefRequestValidator.Validate(input, now);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var request = new ReliefRequest
        {
            RequesterId = userId.Value,
            HouseholdSize = input.HouseholdSize,
            ElderlyCount = input.ElderlyCount,
            ChildrenCount = input.ChildrenCount,
            HomeFlooded = input.HomeFlooded,
            District = SriLanka.CanonicalDistrict(input.District)!,
            AddressText = input.AddressText!.Trim(),
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            Notes = input.Notes?.Trim(),
            PhotoUrl = input.PhotoUrl?.Trim(),
            NeededBy = input.NeededBy?.ToUniversalTime(),
            Status = RequestStatus.Submitted
        };
        request.CreatedAt = now;
        foreach (var item in input.Items!)
            request.Items.Add(new ReliefRequestItem
            {
                Category = item.Category,
                Quantity = item.Quantity,
                Description = item.Description?.Trim()
            });

        ApplyTriage(request, now);

        db.ReliefRequests.Add(request);
        audit.Record(userId, "request.created", nameof(ReliefRequest), request.Id.ToString(),
            new { request.TriageScore, Urgency = request.Urgency.ToString() });
        await db.SaveChangesAsync(ct);

        var created = await LoadAsync(request.Id, ct);
        return CreatedAtAction(nameof(Get), new { id = request.Id }, ReliefRequestDto.From(created!));
    }

    // ---------- List (own requests for citizens, all for staff) ----------
    [HttpGet]
    [Authorize(Roles = Roles.Citizen + "," + Roles.Staff)]
    [ProducesResponseType<PagedResult<ReliefRequestDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] RequestStatus? status,
        [FromQuery] UrgencyLevel? urgency,
        [FromQuery] string? district,
        [FromQuery] string? search,
        [FromQuery] string sort = "urgency",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ReliefRequests.AsNoTracking()
            .Include(r => r.Items).Include(r => r.Requester).AsQueryable();

        // A citizen only ever sees their own requests, whatever filters they send.
        if (!IsStaff)
        {
            var userId = User.GetUserId();
            query = query.Where(r => r.RequesterId == userId);
        }

        if (status is not null) query = query.Where(r => r.Status == status);
        if (urgency is not null) query = query.Where(r => r.Urgency == urgency);
        if (!string.IsNullOrWhiteSpace(district))
        {
            var canonical = SriLanka.CanonicalDistrict(district);
            if (canonical is null) return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["district"] = ["Unknown district."] }));
            query = query.Where(r => r.District == canonical);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(r => r.AddressText.ToLower().Contains(term) ||
                                     (r.Notes != null && r.Notes.ToLower().Contains(term)));
        }

        query = sort.ToLowerInvariant() switch
        {
            "newest" => query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id),
            "oldest" => query.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id),
            // Most urgent first; among equals the one that has waited longest comes first.
            _ => query.OrderByDescending(r => r.TriageScore).ThenBy(r => r.CreatedAt).ThenBy(r => r.Id)
        };

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return Ok(new PagedResult<ReliefRequestDto>(items.Select(ReliefRequestDto.From).ToList(), page, pageSize, total));
    }

    // ---------- Summary for the coordinator dashboard ----------
    [HttpGet("summary")]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<RequestSummaryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var rows = await db.ReliefRequests.AsNoTracking()
            .Select(r => new { r.Status, r.Urgency, r.District })
            .ToListAsync(ct);

        var open = rows.Where(r => !ClosedStatuses.Contains(r.Status)).ToList();

        return Ok(new RequestSummaryDto(
            Total: rows.Count,
            Open: open.Count,
            CriticalOpen: open.Count(r => r.Urgency == UrgencyLevel.Critical),
            ByStatus: rows.GroupBy(r => r.Status.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            ByUrgency: rows.GroupBy(r => r.Urgency.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            ByDistrict: rows.GroupBy(r => r.District).ToDictionary(g => g.Key, g => g.Count())));
    }

    // ---------- Get one ----------
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Citizen + "," + Roles.Staff)]
    [ProducesResponseType<ReliefRequestDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var request = await LoadAsync(id, ct);
        if (request is null || !CanSee(request)) return NotFoundProblem();
        return Ok(ReliefRequestDto.From(request));
    }

    // ---------- Edit (owner, only while Submitted) ----------
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Citizen)]
    [ProducesResponseType<ReliefRequestDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ReliefRequestInput input, CancellationToken ct)
    {
        var request = await db.ReliefRequests.Include(r => r.Items).Include(r => r.Requester)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        // Another citizen's request looks exactly like a request that does not exist.
        if (request is null || request.RequesterId != User.GetUserId()) return NotFoundProblem();

        if (!RequestStatusRules.CanEdit(request.Status))
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: $"A request that is {request.Status} can no longer be edited.");

        var now = clock.GetUtcNow().UtcDateTime;
        var errors = ReliefRequestValidator.Validate(input, now);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        request.HouseholdSize = input.HouseholdSize;
        request.ElderlyCount = input.ElderlyCount;
        request.ChildrenCount = input.ChildrenCount;
        request.HomeFlooded = input.HomeFlooded;
        request.District = SriLanka.CanonicalDistrict(input.District)!;
        request.AddressText = input.AddressText!.Trim();
        request.Latitude = input.Latitude;
        request.Longitude = input.Longitude;
        request.Notes = input.Notes?.Trim();
        request.PhotoUrl = input.PhotoUrl?.Trim();
        request.NeededBy = input.NeededBy?.ToUniversalTime();

        SyncItems(request, input.Items!);
        ApplyTriage(request, now);

        audit.Record(User.GetUserId(), "request.updated", nameof(ReliefRequest), request.Id.ToString(),
            new { request.TriageScore, Urgency = request.Urgency.ToString() });
        await db.SaveChangesAsync(ct);

        var updated = await LoadAsync(id, ct);
        return Ok(ReliefRequestDto.From(updated!));
    }

    // ---------- Cancel (owner or staff) ----------
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Roles.Citizen + "," + Roles.Staff)]
    [ProducesResponseType<ReliefRequestDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelRequestBody? body, CancellationToken ct)
    {
        var request = await db.ReliefRequests.Include(r => r.Items).Include(r => r.Requester)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null || !CanSee(request)) return NotFoundProblem();

        if (!RequestStatusRules.CanCancel(request.Status))
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: $"A request that is {request.Status} cannot be cancelled.");

        var reason = body?.Reason?.Trim();
        if (reason is { Length: > 500 })
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["reason"] = ["Reason cannot be longer than 500 characters."] }));

        RequestStatusRules.Transition(request, RequestStatus.Cancelled);
        request.CancellationReason = string.IsNullOrEmpty(reason) ? null : reason;

        audit.Record(User.GetUserId(), "request.cancelled", nameof(ReliefRequest), request.Id.ToString(),
            new { ByStaff = IsStaff });
        await db.SaveChangesAsync(ct);

        return Ok(ReliefRequestDto.From(request));
    }

    // ---------- Triage (staff): recompute, or override with a reason ----------
    [HttpPost("{id:guid}/triage")]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<ReliefRequestDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Triage(Guid id, [FromBody] TriageRequestBody? body, CancellationToken ct)
    {
        var request = await db.ReliefRequests.Include(r => r.Items).Include(r => r.Requester)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return NotFoundProblem();

        if (!RequestStatusRules.CanTriage(request.Status))
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: $"A request that is {request.Status} can no longer be triaged.");

        var now = clock.GetUtcNow().UtcDateTime;
        string action;

        if (body?.Urgency is { } urgency)
        {
            var reason = body.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length is < 3 or > 500)
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["reason"] = ["A reason of 3 to 500 characters is required when overriding urgency."]
                }));

            ApplyTriage(request, now); // keep the computed score up to date
            request.Urgency = urgency;
            request.UrgencyOverridden = true;
            request.UrgencyOverrideReason = reason;
            action = "request.urgency_overridden";
        }
        else
        {
            request.UrgencyOverridden = false;
            request.UrgencyOverrideReason = null;
            ApplyTriage(request, now);
            action = "request.triage_recomputed";
        }

        audit.Record(User.GetUserId(), action, nameof(ReliefRequest), request.Id.ToString(),
            new { request.TriageScore, Urgency = request.Urgency.ToString(), request.UrgencyOverridden });
        await db.SaveChangesAsync(ct);

        return Ok(ReliefRequestDto.From(request));
    }

    // ---------- Helpers ----------
    private Task<ReliefRequest?> LoadAsync(Guid id, CancellationToken ct) =>
        db.ReliefRequests.AsNoTracking().Include(r => r.Items).Include(r => r.Requester)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    private bool CanSee(ReliefRequest request) => IsStaff || request.RequesterId == User.GetUserId();

    private ObjectResult NotFoundProblem() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Relief request not found.");

    /// <summary>Recomputes score, reasons and (unless a coordinator overrode it) the urgency level.</summary>
    private static void ApplyTriage(ReliefRequest request, DateTime now)
    {
        var result = TriageScorer.Score(new TriageInput(
            request.HouseholdSize, request.ElderlyCount, request.ChildrenCount, request.HomeFlooded,
            request.Items.Select(i => i.Category).ToList(), request.NeededBy, request.CreatedAt), now);

        request.TriageScore = result.Score;
        request.TriageReasonsJson = JsonSerializer.Serialize(result.Reasons);
        if (!request.UrgencyOverridden) request.Urgency = result.Level;
    }

    /// <summary>Updates items in place (matching by category) so the unique index is never violated.</summary>
    private void SyncItems(ReliefRequest request, List<NeedItemInput> wanted)
    {
        foreach (var existing in request.Items.Where(e => wanted.All(w => w.Category != e.Category)).ToList())
        {
            request.Items.Remove(existing);
            db.ReliefRequestItems.Remove(existing);
        }

        foreach (var item in wanted)
        {
            var existing = request.Items.FirstOrDefault(e => e.Category == item.Category);
            if (existing is null)
            {
                // Added explicitly: the item already has an Id, so without this EF would assume the row exists
                // and try to UPDATE it instead of INSERT it.
                db.ReliefRequestItems.Add(new ReliefRequestItem
                {
                    ReliefRequestId = request.Id,
                    Category = item.Category,
                    Quantity = item.Quantity,
                    Description = item.Description?.Trim()
                });
            }
            else
            {
                existing.Quantity = item.Quantity;
                existing.Description = item.Description?.Trim();
            }
        }
    }
}
