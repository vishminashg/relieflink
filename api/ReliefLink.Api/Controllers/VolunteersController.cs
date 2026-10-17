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
/// Component C: Volunteer Management.
/// Profiles, certified skills, availability scheduling, and matching.
/// </summary>
[ApiController]
[Route("api/volunteers")]
[Authorize]
public class VolunteersController(
    AppDbContext db,
    IAuditService audit,
    TimeProvider clock,
    IVolunteerAgentClient volunteerAgent) : ControllerBase
{
    private bool IsStaff => User.IsInRole(Roles.Coordinator) || User.IsInRole(Roles.Admin);

    // ---------- Create Profile ----------
    [HttpPost]
    [Authorize(Roles = Roles.Volunteer + "," + Roles.Staff)]
    [ProducesResponseType<VolunteerDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] VolunteerProfileInput input, CancellationToken ct)
    {
        var errors = VolunteerValidator.ValidateProfile(input);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var currentUserId = User.GetUserId();
        if (currentUserId is null) return Unauthorized();

        // Check if user already has a volunteer profile
        var existing = await db.Volunteers.AnyAsync(v => v.UserId == currentUserId.Value, ct);
        if (existing)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "A volunteer profile already exists for this user.");

        var volunteer = new Volunteer
        {
            UserId = currentUserId.Value,
            District = SriLanka.CanonicalDistrict(input.District)!,
            EmergencyContactName = input.EmergencyContactName!.Trim(),
            EmergencyContactPhone = input.EmergencyContactPhone!.Trim(),
            MaxHoursPerWeek = input.MaxHoursPerWeek,
            Notes = input.Notes?.Trim(),
            IsActive = true
        };

        if (input.Skills is { Count: > 0 } skills)
        {
            foreach (var s in skills)
            {
                volunteer.Skills.Add(new VolunteerSkill
                {
                    Skill = s.Skill,
                    Proficiency = s.Proficiency,
                    CertificateReference = s.CertificateReference?.Trim(),
                    IsVerified = false // Self-registered skills start unverified
                });
            }
        }

        db.Volunteers.Add(volunteer);
        audit.Record(currentUserId.Value, "volunteer.created", nameof(Volunteer), volunteer.Id.ToString());
        await db.SaveChangesAsync(ct);

        var loaded = await LoadAsync(volunteer.Id, ct);
        return CreatedAtAction(nameof(GetById), new { id = volunteer.Id }, VolunteerDto.From(loaded!));
    }

    // ---------- List Profiles (Staff) ----------
    [HttpGet]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> List(
        [FromQuery] string? district,
        [FromQuery] VolunteerSkillType? skill,
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Volunteers
            .Include(v => v.User)
            .Include(v => v.Skills)
            .Include(v => v.Availabilities)
            .Include(v => v.Assignments)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(district))
        {
            var canon = SriLanka.CanonicalDistrict(district);
            if (canon is not null) query = query.Where(v => v.District == canon);
        }

        if (skill.HasValue)
        {
            query = query.Where(v => v.Skills.Any(s => s.Skill == skill.Value));
        }

        if (isActive.HasValue)
        {
            query = query.Where(v => v.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(v =>
                (v.User != null && (v.User.FullName.ToLower().Contains(term) || v.User.Email.ToLower().Contains(term))) ||
                v.EmergencyContactName.ToLower().Contains(term) ||
                v.EmergencyContactPhone.Contains(term));
        }

        query = sort switch
        {
            "name_asc" => query.OrderBy(v => v.User != null ? v.User.FullName : string.Empty),
            "name_desc" => query.OrderByDescending(v => v.User != null ? v.User.FullName : string.Empty),
            "district_asc" => query.OrderBy(v => v.District),
            "createdAt_asc" => query.OrderBy(v => v.CreatedAt),
            _ => query.OrderByDescending(v => v.CreatedAt)
        };

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return Ok(new
        {
            items = items.Select(VolunteerDto.From),
            totalCount = total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
            page,
            pageSize
        });
    }

    // ---------- Get Current User's Volunteer Profile ----------
    [HttpGet("me")]
    [Authorize(Roles = Roles.Volunteer + "," + Roles.Staff)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var volunteer = await db.Volunteers
            .Include(v => v.User)
            .Include(v => v.Skills)
            .Include(v => v.Availabilities)
            .Include(v => v.Assignments)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.UserId == userId.Value, ct);

        if (volunteer is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Volunteer profile not found.");

        return Ok(VolunteerDto.From(volunteer));
    }

    // ---------- Get By Id ----------
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var volunteer = await LoadAsync(id, ct);
        if (volunteer is null) return NotFoundProblem();

        if (!CanAccess(volunteer)) return NotFoundProblem();

        return Ok(VolunteerDto.From(volunteer));
    }

    // ---------- Update Profile ----------
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] VolunteerProfileInput input,
        CancellationToken ct)
    {
        var errors = VolunteerValidator.ValidateProfile(input);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var volunteer = await db.Volunteers.Include(v => v.User).FirstOrDefaultAsync(v => v.Id == id, ct);
        if (volunteer is null) return NotFoundProblem();

        if (!CanAccess(volunteer)) return NotFoundProblem();

        var userId = User.GetUserId()!.Value;

        volunteer.District = SriLanka.CanonicalDistrict(input.District)!;
        volunteer.EmergencyContactName = input.EmergencyContactName!.Trim();
        volunteer.EmergencyContactPhone = input.EmergencyContactPhone!.Trim();
        volunteer.MaxHoursPerWeek = input.MaxHoursPerWeek;
        volunteer.Notes = input.Notes?.Trim();

        audit.Record(userId, "volunteer.updated", nameof(Volunteer), volunteer.Id.ToString());
        await db.SaveChangesAsync(ct);

        var loaded = await LoadAsync(volunteer.Id, ct);
        return Ok(VolunteerDto.From(loaded!));
    }

    // ---------- Update Skills ----------
    [HttpPut("{id:guid}/skills")]
    public async Task<IActionResult> UpdateSkills(
        [FromRoute] Guid id,
        [FromBody] UpdateVolunteerSkillsInput input,
        CancellationToken ct)
    {
        var errors = VolunteerValidator.ValidateSkills(input.Skills);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var volunteer = await db.Volunteers
            .Include(v => v.Skills)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        if (volunteer is null) return NotFoundProblem();

        if (!CanAccess(volunteer)) return NotFoundProblem();

        var userId = User.GetUserId()!.Value;

        // Replace skills
        db.VolunteerSkills.RemoveRange(volunteer.Skills);
        foreach (var s in input.Skills)
        {
            db.VolunteerSkills.Add(new VolunteerSkill
            {
                VolunteerId = volunteer.Id,
                Skill = s.Skill,
                Proficiency = s.Proficiency,
                CertificateReference = s.CertificateReference?.Trim(),
                IsVerified = IsStaff && s.IsVerified // Only staff can verify skills
            });
        }

        audit.Record(userId, "volunteer.skills_updated", nameof(Volunteer), volunteer.Id.ToString());
        await db.SaveChangesAsync(ct);

        var loaded = await LoadAsync(volunteer.Id, ct);
        return Ok(VolunteerDto.From(loaded!));
    }

    // ---------- Update Availability Slots ----------
    [HttpPut("{id:guid}/availability")]
    public async Task<IActionResult> UpdateAvailability(
        [FromRoute] Guid id,
        [FromBody] UpdateVolunteerAvailabilityInput input,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var errors = VolunteerValidator.ValidateAvailabilities(input.Availabilities, now);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var volunteer = await db.Volunteers
            .Include(v => v.Availabilities)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        if (volunteer is null) return NotFoundProblem();

        if (!CanAccess(volunteer)) return NotFoundProblem();

        var userId = User.GetUserId()!.Value;

        // Replace availability windows
        db.VolunteerAvailabilities.RemoveRange(volunteer.Availabilities);
        foreach (var a in input.Availabilities)
        {
            db.VolunteerAvailabilities.Add(new VolunteerAvailability
            {
                VolunteerId = volunteer.Id,
                Type = a.Type,
                DayOfWeek = a.DayOfWeek,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                SpecificStartUtc = a.SpecificStartUtc?.ToUniversalTime(),
                SpecificEndUtc = a.SpecificEndUtc?.ToUniversalTime()
            });
        }

        audit.Record(userId, "volunteer.availability_updated", nameof(Volunteer), volunteer.Id.ToString());
        await db.SaveChangesAsync(ct);

        var loaded = await LoadAsync(volunteer.Id, ct);
        return Ok(VolunteerDto.From(loaded!));
    }

    // ---------- Core Business Operation: Match Volunteers ----------
    [HttpGet("match")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Match(
        [FromQuery] string? skills,
        [FromQuery] string? district,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] bool requireAllSkills = true,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);

        List<VolunteerSkillType> requiredSkills = [];
        if (!string.IsNullOrWhiteSpace(skills))
        {
            foreach (var token in skills.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<VolunteerSkillType>(token, true, out var parsed))
                    requiredSkills.Add(parsed);
            }
        }

        var startUtc = startTime?.ToUniversalTime() ?? clock.GetUtcNow().UtcDateTime;
        var endUtc = endTime?.ToUniversalTime() ?? startUtc.AddHours(4);

        if (endUtc <= startUtc)
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["endTime"] = ["End time must be after start time."] }));

        var targetDistrict = SriLanka.CanonicalDistrict(district) ?? string.Empty;

        // Load active volunteers with skills, availabilities, and active assignments
        var candidates = await db.Volunteers
            .Where(v => v.IsActive)
            .Include(v => v.User)
            .Include(v => v.Skills)
            .Include(v => v.Availabilities)
            .Include(v => v.Assignments.Where(a => a.Status == AssignmentStatus.Assigned || a.Status == AssignmentStatus.InProgress || a.Status == AssignmentStatus.Completed))
            .AsNoTracking()
            .ToListAsync(ct);

        var scored = candidates
            .Select(v =>
            {
                var scoreResult = VolunteerMatcher.ScoreVolunteer(
                    v, requiredSkills, targetDistrict, startUtc, endUtc, requireAllSkills);
                return (Volunteer: v, Score: scoreResult);
            })
            .Where(x => x.Score.IsEligible)
            .OrderByDescending(x => x.Score.TotalScore)
            .ThenBy(x => x.Volunteer.District == targetDistrict ? 0 : 1)
            .Take(limit)
            .Select(x => new VolunteerMatchResultDto(
                VolunteerDto.From(x.Volunteer),
                x.Score.TotalScore,
                x.Score.Quality.ToString(),
                x.Score.IsEligible,
                x.Score.HasScheduleConflict,
                x.Score.HasAvailability,
                x.Score.Reasons,
                x.Score.MatchedSkills.Select(s => s.ToString()).ToList(),
                x.Score.MissingSkills.Select(s => s.ToString()).ToList()))
            .ToList();

        return Ok(scored);
    }

    // ---------- AI Matching Proposal (Staff, read-only) ----------
    [HttpPost("agent-match")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> AgentMatch([FromBody] VolunteerAgentMatchInput input, CancellationToken ct)
    {
        if (input.VolunteersNeeded is < 1 or > 20)
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { [nameof(input.VolunteersNeeded)] = ["VolunteersNeeded must be between 1 and 20."] }));

        var startUtc = input.StartTime.ToUniversalTime();
        var endUtc = input.EndTime.ToUniversalTime();
        if (endUtc <= startUtc)
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { [nameof(input.EndTime)] = ["EndTime must be after StartTime."] }));

        var targetDistrict = SriLanka.CanonicalDistrict(input.District) ?? string.Empty;
        var requiredSkills = input.RequiredSkills ?? [];
        var candidates = await db.Volunteers
            .Where(v => v.IsActive)
            .Include(v => v.User)
            .Include(v => v.Skills)
            .Include(v => v.Availabilities)
            .Include(v => v.Assignments)
            .AsNoTracking()
            .ToListAsync(ct);

        var agentInput = new VolunteerMatchingAgentInput(
            input.RequestId,
            targetDistrict,
            requiredSkills.Select(s => s.ToString()).ToList(),
            startUtc,
            endUtc,
            input.VolunteersNeeded,
            candidates.Select(v => new VolunteerAgentCandidate(
                v.Id,
                v.User?.FullName ?? "Volunteer",
                v.District,
                v.Skills.Select(s => s.Skill.ToString()).ToList(),
                v.Skills.Any(s => s.IsVerified),
                v.Assignments.Count(a => a.Status == AssignmentStatus.Completed),
                v.Availabilities
                    .Where(a => a.Type == AvailabilityType.SpecificDate)
                    .Select(a => new VolunteerAgentAvailability(a.SpecificStartUtc, a.SpecificEndUtc))
                    .ToList(),
                v.Assignments
                    .Where(a => AssignmentStatusRules.IsActive(a.Status))
                    .Select(a => new VolunteerAgentAssignment(a.Id, a.StartTime, a.EndTime))
                    .ToList()))
            .ToList(),
            input.Notes);

        VolunteerAgentResponse response;
        try
        {
            response = await volunteerAgent.MatchAsync(agentInput, ct);
        }
        catch (PlannerUnavailableException ex)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Volunteer matching agent is unavailable.", detail: ex.Message);
        }

        var volunteersById = candidates.ToDictionary(v => v.Id);
        var checks = VolunteerMatcher.ValidateProposal(
            response.Proposal.VolunteerIds,
            volunteersById,
            requiredSkills,
            input.VolunteersNeeded,
            startUtc,
            endUtc,
            response.Proposal.Summary);

        var userId = User.GetUserId();
        if (userId is not null)
            audit.Record(userId.Value, "volunteer.agent_proposal", nameof(Volunteer), input.RequestId.ToString(),
                new { checks = checks.Select(c => new { c.Name, c.Passed }) });

        return Ok(new
        {
            proposal = response.Proposal,
            checks,
            toolCalls = response.ToolCalls ?? []
        });
    }

    // ---------- Summary Metrics (Staff Dashboard) ----------
    [HttpGet("summary")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var total = await db.Volunteers.CountAsync(ct);
        var active = await db.Volunteers.CountAsync(v => v.IsActive, ct);

        var currentlyAssigned = await db.VolunteerAssignments
            .Where(a => a.Status == AssignmentStatus.Assigned || a.Status == AssignmentStatus.InProgress)
            .Select(a => a.VolunteerId)
            .Distinct()
            .CountAsync(ct);

        var available = Math.Max(0, active - currentlyAssigned);

        var byDistrict = await db.Volunteers
            .GroupBy(v => v.District)
            .Select(g => new { District = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.District, x => x.Count, ct);

        var bySkill = await db.VolunteerSkills
            .GroupBy(s => s.Skill)
            .Select(g => new { Skill = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Skill, x => x.Count, ct);

        var assignmentsByStatus = await db.VolunteerAssignments
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        return Ok(new VolunteerSummaryDto(
            total,
            active,
            currentlyAssigned,
            available,
            byDistrict,
            bySkill,
            assignmentsByStatus));
    }

    // ---------- Helpers ----------
    private bool CanAccess(Volunteer v)
    {
        if (IsStaff) return true;
        var currentUserId = User.GetUserId();
        return currentUserId.HasValue && v.UserId == currentUserId.Value;
    }

    private Task<Volunteer?> LoadAsync(Guid id, CancellationToken ct) =>
        db.Volunteers
            .Include(v => v.User)
            .Include(v => v.Skills)
            .Include(v => v.Availabilities)
            .Include(v => v.Assignments)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    private IActionResult NotFoundProblem() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Volunteer not found.");
}
