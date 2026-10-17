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
/// Component C: Volunteer Assignments.
/// Creation with overlap conflict prevention (no double-booking), tracking, and status transitions.
/// </summary>
[ApiController]
[Route("api/assignments")]
[Authorize]
public class AssignmentsController(AppDbContext db, IAuditService audit, TimeProvider clock) : ControllerBase
{
    private bool IsStaff => User.IsInRole(Roles.Coordinator) || User.IsInRole(Roles.Admin);

    // ---------- Create Assignment (Staff) ----------
    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<VolunteerAssignmentDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateAssignmentInput input, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var errors = VolunteerValidator.ValidateAssignment(input, now);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var staffUserId = User.GetUserId();
        if (staffUserId is null) return Unauthorized();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Serialize assignment decisions for the same volunteer across coordinators.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({input.VolunteerId.ToString()}, 0))", ct);

        var volunteer = await db.Volunteers
            .Include(v => v.User)
            .Include(v => v.Assignments)
            .Include(v => v.Availabilities)
            .FirstOrDefaultAsync(v => v.Id == input.VolunteerId, ct);

        if (volunteer is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Volunteer not found.");

        if (!volunteer.IsActive)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Cannot assign an inactive volunteer.");

        var startUtc = input.StartTime.ToUniversalTime();
        var endUtc = input.EndTime.ToUniversalTime();

        if (!VolunteerMatcher.IsWindowCoveredByAvailability(volunteer.Availabilities, startUtc, endUtc))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Volunteer availability does not cover this time window.");
        }

        // Enforce Strict No Double-Booking Invariant
        var activeAssignments = volunteer.Assignments
            .Where(a => AssignmentStatusRules.IsActive(a.Status))
            .ToList();

        var hasConflict = activeAssignments.Any(a =>
            VolunteerMatcher.HasConflict(a.StartTime, a.EndTime, startUtc, endUtc, VolunteerMatcher.TransitBuffer));

        if (hasConflict)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Schedule conflict detected.",
                detail: $"Volunteer has an existing active assignment overlapping this time window (+ {VolunteerMatcher.TransitBufferMinutes}m transit buffer). Double-booking is strictly prohibited.");
        }

        if (input.ReliefRequestId.HasValue)
        {
            var requestExists = await db.ReliefRequests.AnyAsync(r => r.Id == input.ReliefRequestId.Value, ct);
            if (!requestExists)
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Relief request not found.");
        }

        var assignment = new VolunteerAssignment
        {
            VolunteerId = volunteer.Id,
            ReliefRequestId = input.ReliefRequestId,
            StartTime = startUtc,
            EndTime = endUtc,
            Status = AssignmentStatus.Assigned,
            RoleOrTask = input.RoleOrTask?.Trim(),
            Notes = input.Notes?.Trim(),
            CreatedByUserId = staffUserId.Value
        };

        db.VolunteerAssignments.Add(assignment);
        audit.Record(staffUserId.Value, "assignment.created", nameof(VolunteerAssignment), assignment.Id.ToString(),
            new { volunteer.Id, startUtc, endUtc, input.ReliefRequestId });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var loaded = await LoadAsync(assignment.Id, ct);
        return CreatedAtAction(nameof(GetById), new { id = assignment.Id }, VolunteerAssignmentDto.From(loaded!));
    }

    // ---------- List Calling Volunteer's Assignments ----------
    [HttpGet("my")]
    [Authorize(Roles = Roles.Volunteer + "," + Roles.Staff)]
    public async Task<IActionResult> GetMyAssignments(CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (currentUserId is null) return Unauthorized();

        var volunteer = await db.Volunteers.FirstOrDefaultAsync(v => v.UserId == currentUserId.Value, ct);
        if (volunteer is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Volunteer profile not found for this user.");

        var assignments = await db.VolunteerAssignments
            .Where(a => a.VolunteerId == volunteer.Id)
            .Include(a => a.Volunteer)
            .ThenInclude(v => v!.User)
            .OrderByDescending(a => a.StartTime)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(assignments.Select(VolunteerAssignmentDto.From));
    }

    // ---------- List All Assignments (Staff) ----------
    [HttpGet]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? volunteerId,
        [FromQuery] Guid? reliefRequestId,
        [FromQuery] AssignmentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.VolunteerAssignments
            .Include(a => a.Volunteer)
            .ThenInclude(v => v!.User)
            .AsNoTracking();

        if (volunteerId.HasValue) query = query.Where(a => a.VolunteerId == volunteerId.Value);
        if (reliefRequestId.HasValue) query = query.Where(a => a.ReliefRequestId == reliefRequestId.Value);
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);

        query = query.OrderByDescending(a => a.StartTime);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return Ok(new
        {
            items = items.Select(VolunteerAssignmentDto.From),
            total,
            page,
            pageSize
        });
    }

    // ---------- Get Assignment By Id ----------
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var assignment = await LoadAsync(id, ct);
        if (assignment is null) return NotFoundProblem();

        if (!CanAccess(assignment)) return NotFoundProblem();

        return Ok(VolunteerAssignmentDto.From(assignment));
    }

    // ---------- Update Status ----------
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateAssignmentStatusInput input,
        CancellationToken ct)
    {
        var assignment = await db.VolunteerAssignments
            .Include(a => a.Volunteer)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assignment is null) return NotFoundProblem();
        if (!CanAccess(assignment)) return NotFoundProblem();

        try
        {
            AssignmentStatusRules.Transition(assignment, input.Status);
        }
        catch (InvalidAssignmentTransitionException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(input.Notes))
        {
            assignment.Notes = string.IsNullOrWhiteSpace(assignment.Notes)
                ? input.Notes.Trim()
                : $"{assignment.Notes}\n{input.Notes.Trim()}";
        }

        var userId = User.GetUserId()!.Value;
        audit.Record(userId, "assignment.status_updated", nameof(VolunteerAssignment), assignment.Id.ToString(),
            new { from = assignment.Status, to = input.Status });
        await db.SaveChangesAsync(ct);

        var loaded = await LoadAsync(assignment.Id, ct);
        return Ok(VolunteerAssignmentDto.From(loaded!));
    }

    // ---------- Cancel Assignment ----------
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid id,
        [FromBody] CancelRequestBody? body,
        CancellationToken ct)
    {
        var assignment = await db.VolunteerAssignments
            .Include(a => a.Volunteer)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assignment is null) return NotFoundProblem();
        if (!CanAccess(assignment)) return NotFoundProblem();

        try
        {
            AssignmentStatusRules.Transition(assignment, AssignmentStatus.Cancelled);
        }
        catch (InvalidAssignmentTransitionException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(body?.Reason))
        {
            assignment.Notes = string.IsNullOrWhiteSpace(assignment.Notes)
                ? $"Cancelled: {body.Reason.Trim()}"
                : $"{assignment.Notes}\nCancelled: {body.Reason.Trim()}";
        }

        var userId = User.GetUserId()!.Value;
        audit.Record(userId, "assignment.cancelled", nameof(VolunteerAssignment), assignment.Id.ToString(),
            new { reason = body?.Reason });
        await db.SaveChangesAsync(ct);

        var loaded = await LoadAsync(assignment.Id, ct);
        return Ok(VolunteerAssignmentDto.From(loaded!));
    }

    // ---------- Helpers ----------
    private bool CanAccess(VolunteerAssignment a)
    {
        if (IsStaff) return true;
        var currentUserId = User.GetUserId();
        return currentUserId.HasValue && a.Volunteer?.UserId == currentUserId.Value;
    }

    private Task<VolunteerAssignment?> LoadAsync(Guid id, CancellationToken ct) =>
        db.VolunteerAssignments
            .Include(a => a.Volunteer)
            .ThenInclude(v => v!.User)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    private IActionResult NotFoundProblem() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Assignment not found.");
}
