using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Controllers.Dtos;

public record VolunteerSkillInput(
    VolunteerSkillType Skill,
    SkillProficiency Proficiency = SkillProficiency.Intermediate,
    string? CertificateReference = null,
    bool IsVerified = false);

public record VolunteerAvailabilityInput(
    AvailabilityType Type,
    DayOfWeek? DayOfWeek = null,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    DateTime? SpecificStartUtc = null,
    DateTime? SpecificEndUtc = null);

public record VolunteerProfileInput(
    string? District,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    int MaxHoursPerWeek = 40,
    string? Notes = null,
    List<VolunteerSkillInput>? Skills = null);

public record UpdateVolunteerSkillsInput(List<VolunteerSkillInput> Skills);

public record UpdateVolunteerAvailabilityInput(List<VolunteerAvailabilityInput> Availabilities);

public record VolunteerUserDto(Guid Id, string FullName, string Email, string? Phone);

public record VolunteerSkillDto(
    Guid Id,
    string Skill,
    string Proficiency,
    string? CertificateReference,
    bool IsVerified);

public record VolunteerAvailabilityDto(
    Guid Id,
    string Type,
    string? DayOfWeek,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    DateTime? SpecificStartUtc,
    DateTime? SpecificEndUtc);

public record VolunteerDto(
    Guid Id,
    VolunteerUserDto? User,
    string District,
    string EmergencyContactName,
    string EmergencyContactPhone,
    bool IsActive,
    int MaxHoursPerWeek,
    string? Notes,
    IReadOnlyList<VolunteerSkillDto> Skills,
    IReadOnlyList<VolunteerAvailabilityDto> Availabilities,
    int ActiveAssignmentsCount,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static VolunteerDto From(Volunteer v) => new(
        v.Id,
        v.User is null ? null : new VolunteerUserDto(v.User.Id, v.User.FullName, v.User.Email, v.User.Phone),
        v.District,
        v.EmergencyContactName,
        v.EmergencyContactPhone,
        v.IsActive,
        v.MaxHoursPerWeek,
        v.Notes,
        v.Skills.OrderBy(s => s.Skill.ToString())
            .Select(s => new VolunteerSkillDto(s.Id, s.Skill.ToString(), s.Proficiency.ToString(), s.CertificateReference, s.IsVerified))
            .ToList(),
        v.Availabilities.OrderBy(a => a.Type.ToString())
            .Select(a => new VolunteerAvailabilityDto(a.Id, a.Type.ToString(), a.DayOfWeek?.ToString(), a.StartTime, a.EndTime, a.SpecificStartUtc, a.SpecificEndUtc))
            .ToList(),
        v.Assignments.Count(a => AssignmentStatusRules.IsActive(a.Status)),
        v.CreatedAt,
        v.UpdatedAt);
}

public record VolunteerMatchQuery(
    List<VolunteerSkillType>? Skills,
    string? District,
    DateTime? StartTime,
    DateTime? EndTime,
    bool RequireAllSkills = true,
    int Limit = 10);

public record VolunteerMatchResultDto(
    VolunteerDto Volunteer,
    int Score,
    string Quality,
    bool IsEligible,
    bool HasScheduleConflict,
    bool HasAvailability,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills);

public record VolunteerAgentMatchInput(
    Guid RequestId,
    string? District,
    List<VolunteerSkillType>? RequiredSkills,
    DateTime StartTime,
    DateTime EndTime,
    int VolunteersNeeded = 1,
    string? Notes = null);

public record CreateAssignmentInput(
    Guid VolunteerId,
    Guid? ReliefRequestId,
    DateTime StartTime,
    DateTime EndTime,
    string? RoleOrTask,
    string? Notes);

public record UpdateAssignmentStatusInput(
    AssignmentStatus Status,
    string? Notes);

public record VolunteerAssignmentDto(
    Guid Id,
    Guid VolunteerId,
    string VolunteerName,
    Guid? ReliefRequestId,
    DateTime StartTime,
    DateTime EndTime,
    string Status,
    string? RoleOrTask,
    string? Notes,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static VolunteerAssignmentDto From(VolunteerAssignment a) => new(
        a.Id,
        a.VolunteerId,
        a.Volunteer?.User?.FullName ?? "Volunteer",
        a.ReliefRequestId,
        a.StartTime,
        a.EndTime,
        a.Status.ToString(),
        a.RoleOrTask,
        a.Notes,
        a.CreatedByUserId,
        a.CreatedAt,
        a.UpdatedAt);
}

public record VolunteerSummaryDto(
    int TotalVolunteers,
    int ActiveVolunteers,
    int CurrentlyAssignedVolunteers,
    int AvailableVolunteers,
    IReadOnlyDictionary<string, int> ByDistrict,
    IReadOnlyDictionary<string, int> BySkill,
    IReadOnlyDictionary<string, int> AssignmentsByStatus);
