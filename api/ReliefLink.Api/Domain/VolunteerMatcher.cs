namespace ReliefLink.Api.Domain;

public enum MatchQuality
{
    Low,
    Adequate,
    Good,
    Excellent
}

public record VolunteerMatchScore(
    Guid VolunteerId,
    int TotalScore,
    MatchQuality Quality,
    bool IsEligible,
    bool HasScheduleConflict,
    bool HasAvailability,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<VolunteerSkillType> MatchedSkills,
    IReadOnlyList<VolunteerSkillType> MissingSkills);

public record ProposalValidationCheck(string Name, bool Passed, string? Detail = null);

/// <summary>
/// Pure deterministic algorithm for:
/// 1. Mathematical interval overlap detection (No double-booking).
/// 2. Skill, proximity, and experience scoring (0-100).
/// 3. Deterministic proposal validation before coordinator approval.
/// </summary>
public static class VolunteerMatcher
{
    public const int TransitBufferMinutes = 30;
    public static readonly TimeSpan TransitBuffer = TimeSpan.FromMinutes(TransitBufferMinutes);

    public const int ExcellentThreshold = 80;
    public const int GoodThreshold = 60;
    public const int AdequateThreshold = 40;

    // Sri Lanka standard timezone is UTC+05:30.
    public static readonly TimeSpan SriLankaUtcOffset = new(5, 30, 0);

    private static readonly Dictionary<string, string[]> AdjacentDistricts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Colombo"] = ["Gampaha", "Kalutara", "Kegalle", "Ratnapura"],
        ["Gampaha"] = ["Colombo", "Kegalle", "Kurunegala", "Puttalam"],
        ["Kalutara"] = ["Colombo", "Galle", "Ratnapura"],
        ["Kandy"] = ["Matale", "Nuwara Eliya", "Kegalle", "Kurunegala", "Badulla"],
        ["Matale"] = ["Kandy", "Anuradhapura", "Polonnaruwa", "Badulla", "Kurunegala"],
        ["Nuwara Eliya"] = ["Kandy", "Badulla", "Ratnapura", "Kegalle"],
        ["Galle"] = ["Kalutara", "Matara", "Ratnapura"],
        ["Matara"] = ["Galle", "Hambantota", "Ratnapura"],
        ["Hambantota"] = ["Matara", "Ratnapura", "Monaragala", "Ampara"],
        ["Jaffna"] = ["Kilinochchi"],
        ["Kilinochchi"] = ["Jaffna", "Mullaitivu", "Mannar"],
        ["Mannar"] = ["Puttalam", "Anuradhapura", "Vavuniya", "Kilinochchi"],
        ["Vavuniya"] = ["Anuradhapura", "Mannar", "Mullaitivu", "Trincomalee"],
        ["Mullaitivu"] = ["Kilinochchi", "Vavuniya", "Trincomalee", "Mannar"],
        ["Batticaloa"] = ["Polonnaruwa", "Ampara", "Trincomalee"],
        ["Ampara"] = ["Batticaloa", "Polonnaruwa", "Badulla", "Monaragala", "Hambantota"],
        ["Trincomalee"] = ["Anuradhapura", "Polonnaruwa", "Batticaloa", "Mullaitivu", "Vavuniya"],
        ["Kurunegala"] = ["Gampaha", "Kegalle", "Kandy", "Matale", "Anuradhapura", "Puttalam"],
        ["Puttalam"] = ["Kurunegala", "Gampaha", "Anuradhapura", "Mannar"],
        ["Anuradhapura"] = ["Kurunegala", "Puttalam", "Matale", "Polonnaruwa", "Vavuniya", "Mannar", "Trincomalee"],
        ["Polonnaruwa"] = ["Anuradhapura", "Matale", "Badulla", "Ampara", "Batticaloa", "Trincomalee"],
        ["Badulla"] = ["Nuwara Eliya", "Kandy", "Matale", "Polonnaruwa", "Ampara", "Monaragala", "Ratnapura"],
        ["Monaragala"] = ["Badulla", "Ampara", "Hambantota", "Ratnapura"],
        ["Ratnapura"] = ["Kalutara", "Kegalle", "Colombo", "Galle", "Matara", "Hambantota", "Badulla", "Nuwara Eliya"],
        ["Kegalle"] = ["Gampaha", "Colombo", "Ratnapura", "Kandy", "Kurunegala", "Nuwara Eliya"]
    };

    /// <summary>
    /// Mathematical check for schedule overlap between two intervals with a transit buffer.
    /// Conflict exists iff: startA &lt; endB + buffer AND startB &lt; endA + buffer.
    /// </summary>
    public static bool HasConflict(DateTime startA, DateTime endA, DateTime startB, DateTime endB, TimeSpan? buffer = null)
    {
        var buf = buffer ?? TransitBuffer;
        return startA < endB + buf && startB < endA + buf;
    }

    /// <summary>
    /// Checks if an operational time window is covered by a volunteer's registered availability slots.
    /// Supports both SpecificDate (UTC timestamp windows) and RecurringWeekly schedules.
    /// </summary>
    public static bool IsWindowCoveredByAvailability(
        IEnumerable<VolunteerAvailability> availabilities,
        DateTime missionStartUtc,
        DateTime missionEndUtc)
    {
        var slots = availabilities.ToList();
        if (slots.Count == 0) return true; // If no slots configured, volunteer is treated as open/unrestricted.

        // 1. Check specific date availability
        var matchesSpecific = slots.Any(a =>
            a.Type == AvailabilityType.SpecificDate &&
            a.SpecificStartUtc.HasValue &&
            a.SpecificEndUtc.HasValue &&
            a.SpecificStartUtc.Value <= missionStartUtc &&
            a.SpecificEndUtc.Value >= missionEndUtc);

        if (matchesSpecific) return true;

        // 2. Check recurring weekly availability in local Sri Lanka time
        var localStart = missionStartUtc + SriLankaUtcOffset;
        var localEnd = missionEndUtc + SriLankaUtcOffset;
        var dayOfWeek = localStart.DayOfWeek;

        // Same day check
        if (localStart.Date == localEnd.Date)
        {
            var missionStartTime = TimeOnly.FromTimeSpan(localStart.TimeOfDay);
            var missionEndTime = TimeOnly.FromTimeSpan(localEnd.TimeOfDay);

            return slots.Any(a =>
                a.Type == AvailabilityType.RecurringWeekly &&
                a.DayOfWeek == dayOfWeek &&
                a.StartTime.HasValue &&
                a.EndTime.HasValue &&
                a.StartTime.Value <= missionStartTime &&
                a.EndTime.Value >= missionEndTime);
        }

        return false;
    }

    /// <summary>
    /// Determines proximity score based on district adjacency.
    /// Same district = 35 pts, adjacent = 20 pts, other = 5 pts.
    /// </summary>
    public static int CalculateProximityScore(string? volunteerDistrict, string? targetDistrict)
    {
        if (string.IsNullOrWhiteSpace(volunteerDistrict) || string.IsNullOrWhiteSpace(targetDistrict))
            return 5;

        if (volunteerDistrict.Equals(targetDistrict, StringComparison.OrdinalIgnoreCase))
            return 35;

        if (AdjacentDistricts.TryGetValue(targetDistrict, out var neighbors) &&
            neighbors.Any(n => n.Equals(volunteerDistrict, StringComparison.OrdinalIgnoreCase)))
            return 20;

        return 5;
    }

    /// <summary>
    /// Evaluates and scores a volunteer for a specific relief mission.
    /// </summary>
    public static VolunteerMatchScore ScoreVolunteer(
        Volunteer volunteer,
        IReadOnlyList<VolunteerSkillType> requiredSkills,
        string targetDistrict,
        DateTime missionStartUtc,
        DateTime missionEndUtc,
        bool requireAllSkills = true)
    {
        List<string> reasons = [];
        var activeAssignments = volunteer.Assignments
            .Where(a => AssignmentStatusRules.IsActive(a.Status))
            .ToList();

        // 1. Conflict Check (Strict No Double-Booking)
        var hasConflict = activeAssignments.Any(a =>
            HasConflict(a.StartTime, a.EndTime, missionStartUtc, missionEndUtc, TransitBuffer));

        if (hasConflict)
        {
            reasons.Add("Has an active assignment overlapping this time window (+ 30m transit buffer).");
        }

        // 2. Availability Check
        var hasAvailability = IsWindowCoveredByAvailability(volunteer.Availabilities, missionStartUtc, missionEndUtc);
        if (!hasAvailability)
        {
            reasons.Add("Mission time window falls outside registered availability schedule.");
        }

        // 3. Skill Matching
        var volunteerSkills = volunteer.Skills.ToDictionary(s => s.Skill);
        List<VolunteerSkillType> matched = [];
        List<VolunteerSkillType> missing = [];

        foreach (var req in requiredSkills)
        {
            if (volunteerSkills.ContainsKey(req))
                matched.Add(req);
            else
                missing.Add(req);
        }

        var skillScore = 0;
        if (requiredSkills.Count == 0)
        {
            skillScore = 30; // General mission, no specialist skills requested
            reasons.Add("General volunteer role (no mandatory specialist skills required).");
        }
        else if (missing.Count == 0)
        {
            skillScore = 30;
            reasons.Add($"Possesses all {matched.Count} requested skills ({string.Join(", ", matched)}).");
        }
        else
        {
            var ratio = (double)matched.Count / requiredSkills.Count;
            skillScore = (int)(ratio * 30);
            reasons.Add($"Missing required skills: {string.Join(", ", missing)}.");
        }

        // Bonus for complementary skills
        var complementarySkills = volunteerSkills.Keys.Except(requiredSkills).ToList();
        var complementaryBonus = Math.Min(15, complementarySkills.Count * 5);
        if (complementaryBonus > 0)
        {
            skillScore += complementaryBonus;
            reasons.Add($"Has complementary skills: {string.Join(", ", complementarySkills.Take(3))}.");
        }

        // 4. Proximity Score
        var proximityScore = CalculateProximityScore(volunteer.District, targetDistrict);
        if (proximityScore == 35)
            reasons.Add($"Located in the target district ({volunteer.District}).");
        else if (proximityScore == 20)
            reasons.Add($"Located in neighboring district ({volunteer.District}).");
        else
            reasons.Add($"Located in remote district ({volunteer.District}).");

        // 5. Experience & Reliability Score (Up to 20 pts)
        var experienceScore = 0;
        var completedCount = volunteer.Assignments.Count(a => a.Status == AssignmentStatus.Completed);
        var completedBonus = Math.Min(10, completedCount * 2);
        experienceScore += completedBonus;
        if (completedCount > 0)
            reasons.Add($"Completed {completedCount} prior relief assignments.");

        var hasVerifiedOrAdvanced = volunteer.Skills.Any(s => s.IsVerified || s.Proficiency is SkillProficiency.Advanced or SkillProficiency.Expert);
        if (hasVerifiedOrAdvanced)
        {
            experienceScore += 10;
            reasons.Add("Has verified certification or advanced skill proficiency.");
        }

        var totalScore = Math.Clamp(skillScore + proximityScore + experienceScore, 0, 100);

        // Eligibility determination
        var isEligible = volunteer.IsActive &&
                         !hasConflict &&
                         hasAvailability &&
                         (!requireAllSkills || missing.Count == 0);

        var quality = totalScore switch
        {
            >= ExcellentThreshold => MatchQuality.Excellent,
            >= GoodThreshold => MatchQuality.Good,
            >= AdequateThreshold => MatchQuality.Adequate,
            _ => MatchQuality.Low
        };

        return new VolunteerMatchScore(
            volunteer.Id,
            totalScore,
            quality,
            isEligible,
            hasConflict,
            hasAvailability,
            reasons,
            matched,
            missing);
    }

    /// <summary>
    /// Deterministic validation checks on an AI-proposed volunteer roster.
    /// </summary>
    public static List<ProposalValidationCheck> ValidateProposal(
        IReadOnlyList<Guid> proposedVolunteerIds,
        IReadOnlyDictionary<Guid, Volunteer> volunteersById,
        IReadOnlyList<VolunteerSkillType> requiredSkills,
        int requestedCount,
        DateTime missionStartUtc,
        DateTime missionEndUtc,
        string? summary)
    {
        List<ProposalValidationCheck> checks = [];

        // 1. Volunteers exist and active
        var allExist = proposedVolunteerIds.All(id => volunteersById.TryGetValue(id, out var v) && v.IsActive);
        checks.Add(new ProposalValidationCheck(
            "volunteers-exist",
            allExist,
            allExist ? null : "One or more proposed volunteers do not exist or are inactive."));

        // 2. Count sufficient
        var countMatches = proposedVolunteerIds.Count == requestedCount;
        checks.Add(new ProposalValidationCheck(
            "count-sufficient",
            countMatches,
            countMatches ? null : $"Proposed {proposedVolunteerIds.Count} volunteers, but mission requested {requestedCount}."));

        var hasDuplicateVolunteers = proposedVolunteerIds.Count == proposedVolunteerIds.Distinct().Count();
        checks.Add(new ProposalValidationCheck(
            "unique-volunteers",
            hasDuplicateVolunteers,
            hasDuplicateVolunteers ? null : "The proposal contains the same volunteer more than once."));

        // 3. No double booking
        var conflictFree = true;
        List<string> conflictDetails = [];

        foreach (var id in proposedVolunteerIds)
        {
            if (volunteersById.TryGetValue(id, out var v))
            {
                var conflict = v.Assignments
                    .Where(a => AssignmentStatusRules.IsActive(a.Status))
                    .Any(a => HasConflict(a.StartTime, a.EndTime, missionStartUtc, missionEndUtc, TransitBuffer));

                if (conflict)
                {
                    conflictFree = false;
                    conflictDetails.Add($"Volunteer {id} has a conflicting active assignment.");
                }
            }
        }

        checks.Add(new ProposalValidationCheck(
            "no-double-booking",
            conflictFree,
            conflictFree ? null : string.Join(" | ", conflictDetails)));

        var availabilityValid = true;
        List<string> availabilityDetails = [];

        foreach (var id in proposedVolunteerIds.Distinct())
        {
            if (volunteersById.TryGetValue(id, out var v) &&
                !IsWindowCoveredByAvailability(v.Availabilities, missionStartUtc, missionEndUtc))
            {
                availabilityValid = false;
                availabilityDetails.Add($"Volunteer {id} is not available for the requested time window.");
            }
        }

        checks.Add(new ProposalValidationCheck(
            "availability-covered",
            availabilityValid,
            availabilityValid ? null : string.Join(" | ", availabilityDetails)));

        // 4. Skills satisfied across the team
        var allSkillsSatisfied = true;
        List<VolunteerSkillType> missingFromTeam = [];

        var teamSkills = proposedVolunteerIds
            .Where(id => volunteersById.ContainsKey(id))
            .SelectMany(id => volunteersById[id].Skills.Select(s => s.Skill))
            .ToHashSet();

        foreach (var req in requiredSkills)
        {
            if (!teamSkills.Contains(req))
            {
                allSkillsSatisfied = false;
                missingFromTeam.Add(req);
            }
        }

        checks.Add(new ProposalValidationCheck(
            "skills-satisfied",
            allSkillsSatisfied,
            allSkillsSatisfied ? null : $"The proposed team is missing required skills: {string.Join(", ", missingFromTeam)}."));

        // 5. Summary present and clean of markup/links
        var summaryValid = !string.IsNullOrWhiteSpace(summary) &&
                           summary.Length >= 10 &&
                           summary.Length <= 1000 &&
                           !summary.Contains("http://", StringComparison.OrdinalIgnoreCase) &&
                           !summary.Contains("https://", StringComparison.OrdinalIgnoreCase) &&
                           !summary.Contains('<') &&
                           !summary.Contains('>');

        checks.Add(new ProposalValidationCheck(
            "rationale-present",
            summaryValid,
            summaryValid ? null : "Summary reasoning is missing, too short/long, or contains invalid links/markup."));

        return checks;
    }

    public static bool AllPassed(IEnumerable<ProposalValidationCheck> checks) => checks.All(c => c.Passed);
}
