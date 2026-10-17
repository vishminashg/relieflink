namespace ReliefLink.Api.Domain;

public enum VolunteerSkillType
{
    FirstAid,
    Medical,
    SearchAndRescue,
    BoatOperation,
    HeavyVehicleDriving,
    FoodDistribution,
    ChildCare,
    ElderlyCare
}

public enum SkillProficiency
{
    Beginner,
    Intermediate,
    Advanced,
    Expert
}

public enum AvailabilityType
{
    RecurringWeekly,
    SpecificDate
}

public enum AssignmentStatus
{
    Assigned,
    InProgress,
    Completed,
    Cancelled
}

/// <summary>
/// A registered volunteer profile linked to a User account.
/// Owned by Component C (Volunteer Management).
/// </summary>
public class Volunteer : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string District { get; set; } = string.Empty;
    public string EmergencyContactName { get; set; } = string.Empty;
    public string EmergencyContactPhone { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int MaxHoursPerWeek { get; set; } = 40;
    public string? Notes { get; set; }

    public List<VolunteerSkill> Skills { get; set; } = new();
    public List<VolunteerAvailability> Availabilities { get; set; } = new();
    public List<VolunteerAssignment> Assignments { get; set; } = new();
}

public class VolunteerSkill : BaseEntity
{
    public Guid VolunteerId { get; set; }
    public Volunteer? Volunteer { get; set; }

    public VolunteerSkillType Skill { get; set; }
    public SkillProficiency Proficiency { get; set; } = SkillProficiency.Intermediate;
    public string? CertificateReference { get; set; }
    public bool IsVerified { get; set; }
}

public class VolunteerAvailability : BaseEntity
{
    public Guid VolunteerId { get; set; }
    public Volunteer? Volunteer { get; set; }

    public AvailabilityType Type { get; set; } = AvailabilityType.RecurringWeekly;

    /// <summary>0 = Sunday, 1 = Monday, ..., 6 = Saturday (used when Type is RecurringWeekly).</summary>
    public DayOfWeek? DayOfWeek { get; set; }

    /// <summary>Start of available window for recurring schedules.</summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>End of available window for recurring schedules.</summary>
    public TimeOnly? EndTime { get; set; }

    /// <summary>Specific start time in UTC (used when Type is SpecificDate).</summary>
    public DateTime? SpecificStartUtc { get; set; }

    /// <summary>Specific end time in UTC (used when Type is SpecificDate).</summary>
    public DateTime? SpecificEndUtc { get; set; }
}

public class VolunteerAssignment : BaseEntity
{
    public Guid VolunteerId { get; set; }
    public Volunteer? Volunteer { get; set; }

    /// <summary>Optional link to a relief request mission.</summary>
    public Guid? ReliefRequestId { get; set; }
    public ReliefRequest? ReliefRequest { get; set; }

    public DateTime StartTime { get; set; } // UTC
    public DateTime EndTime { get; set; }   // UTC

    public AssignmentStatus Status { get; set; } = AssignmentStatus.Assigned;
    public string? RoleOrTask { get; set; }
    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }
}
