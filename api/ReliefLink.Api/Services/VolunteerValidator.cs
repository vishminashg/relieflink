using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Services;

/// <summary>
/// Centralized validation for volunteer profiles, availability intervals, skills, and assignments.
/// </summary>
public static class VolunteerValidator
{
    public const int MaxHoursPerWeekUpper = 168;

    public static Dictionary<string, string[]> ValidateProfile(VolunteerProfileInput input)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = new List<string>();
            list.Add(message);
        }

        if (SriLanka.CanonicalDistrict(input.District) is null)
            Add(nameof(input.District), "Choose a valid Sri Lankan district.");

        var emergencyName = input.EmergencyContactName?.Trim() ?? string.Empty;
        if (emergencyName.Length is < 2 or > 120)
            Add(nameof(input.EmergencyContactName), "Emergency contact name must be between 2 and 120 characters.");

        var emergencyPhone = input.EmergencyContactPhone?.Trim() ?? string.Empty;
        if (emergencyPhone.Length is < 7 or > 30)
            Add(nameof(input.EmergencyContactPhone), "Emergency contact phone must be between 7 and 30 characters.");

        if (input.MaxHoursPerWeek is < 1 or > MaxHoursPerWeekUpper)
            Add(nameof(input.MaxHoursPerWeek), $"Max hours per week must be between 1 and {MaxHoursPerWeekUpper}.");

        if (input.Notes is { Length: > 1000 })
            Add(nameof(input.Notes), "Notes cannot be longer than 1000 characters.");

        if (input.Skills is { Count: > 0 } skills)
        {
            if (skills.Select(s => s.Skill).Distinct().Count() != skills.Count)
                Add(nameof(input.Skills), "Each skill can only be registered once.");

            foreach (var skill in skills)
            {
                if (skill.CertificateReference is { Length: > 100 })
                    Add(nameof(input.Skills), "Certificate reference cannot be longer than 100 characters.");
            }
        }

        return errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    public static Dictionary<string, string[]> ValidateSkills(List<VolunteerSkillInput> skills)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = new List<string>();
            list.Add(message);
        }

        if (skills.Select(s => s.Skill).Distinct().Count() != skills.Count)
            Add(nameof(skills), "Each skill can only be registered once.");

        foreach (var skill in skills)
        {
            if (skill.CertificateReference is { Length: > 100 })
                Add(nameof(skills), "Certificate reference cannot be longer than 100 characters.");
        }

        return errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    public static Dictionary<string, string[]> ValidateAvailabilities(List<VolunteerAvailabilityInput> availabilities, DateTime now)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = new List<string>();
            list.Add(message);
        }

        for (var i = 0; i < availabilities.Count; i++)
        {
            var slot = availabilities[i];
            var prefix = $"Availabilities[{i}]";

            if (slot.Type == AvailabilityType.RecurringWeekly)
            {
                if (slot.DayOfWeek is null)
                    Add(prefix, "DayOfWeek is required for recurring availability.");

                if (slot.StartTime is null || slot.EndTime is null)
                    Add(prefix, "StartTime and EndTime are required for recurring availability.");
                else if (slot.EndTime <= slot.StartTime)
                    Add(prefix, "EndTime must be strictly after StartTime.");
            }
            else if (slot.Type == AvailabilityType.SpecificDate)
            {
                if (slot.SpecificStartUtc is null || slot.SpecificEndUtc is null)
                    Add(prefix, "SpecificStartUtc and SpecificEndUtc are required for specific date availability.");
                else
                {
                    if (slot.SpecificEndUtc <= slot.SpecificStartUtc)
                        Add(prefix, "SpecificEndUtc must be strictly after SpecificStartUtc.");
                    if (slot.SpecificEndUtc < now.AddDays(-1))
                        Add(prefix, "Specific date availability cannot be in the past.");
                }
            }
        }

        return errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    public static Dictionary<string, string[]> ValidateAssignment(CreateAssignmentInput input, DateTime now)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = new List<string>();
            list.Add(message);
        }

        if (input.VolunteerId == Guid.Empty)
            Add(nameof(input.VolunteerId), "A valid volunteer ID is required.");

        if (input.EndTime <= input.StartTime)
            Add(nameof(input.EndTime), "EndTime must be strictly after StartTime.");

        if (input.StartTime < now.AddDays(-7))
            Add(nameof(input.StartTime), "Assignment start time cannot be more than 7 days in the past.");

        if (input.RoleOrTask is { Length: > 200 })
            Add(nameof(input.RoleOrTask), "Role or task cannot exceed 200 characters.");

        if (input.Notes is { Length: > 1000 })
            Add(nameof(input.Notes), "Notes cannot exceed 1000 characters.");

        return errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }
}
