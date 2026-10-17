using ReliefLink.Api.Domain;
using Xunit;

namespace ReliefLink.Tests.Unit;

public class VolunteerMatcherTests
{
    private static readonly DateTime BaseTime = new(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc);

    // ==========================================
    // 1. HasConflict & Interval Overlap Math
    // ==========================================

    [Fact]
    public void HasConflict_WhenIntervalsDirectlyOverlap_ReturnsTrue()
    {
        var startA = BaseTime;
        var endA = BaseTime.AddHours(3); // 09:00 - 12:00
        var startB = BaseTime.AddHours(1);
        var endB = BaseTime.AddHours(4); // 10:00 - 13:00

        Assert.True(VolunteerMatcher.HasConflict(startA, endA, startB, endB));
    }

    [Fact]
    public void HasConflict_WhenIntervalContainedWithinAnother_ReturnsTrue()
    {
        var startA = BaseTime;
        var endA = BaseTime.AddHours(4); // 09:00 - 13:00
        var startB = BaseTime.AddHours(1);
        var endB = BaseTime.AddHours(2); // 10:00 - 11:00

        Assert.True(VolunteerMatcher.HasConflict(startA, endA, startB, endB));
    }

    [Fact]
    public void HasConflict_WhenWithinTransitBufferWindow_ReturnsTrue()
    {
        var startA = BaseTime;
        var endA = BaseTime.AddHours(3); // 09:00 - 12:00
        var startB = BaseTime.AddHours(3).AddMinutes(15); // 12:15 - starts within 30m transit buffer
        var endB = BaseTime.AddHours(5);

        Assert.True(VolunteerMatcher.HasConflict(startA, endA, startB, endB));
    }

    [Fact]
    public void HasConflict_WhenBeyondTransitBufferWindow_ReturnsFalse()
    {
        var startA = BaseTime;
        var endA = BaseTime.AddHours(3); // 09:00 - 12:00
        var startB = BaseTime.AddHours(3).AddMinutes(31); // 12:31 - past the 30m buffer
        var endB = BaseTime.AddHours(5);

        Assert.False(VolunteerMatcher.HasConflict(startA, endA, startB, endB));
    }

    [Fact]
    public void HasConflict_WhenZeroBufferAndBackToBack_ReturnsFalse()
    {
        var startA = BaseTime;
        var endA = BaseTime.AddHours(3); // 09:00 - 12:00
        var startB = BaseTime.AddHours(3); // 12:00
        var endB = BaseTime.AddHours(5);

        Assert.False(VolunteerMatcher.HasConflict(startA, endA, startB, endB, TimeSpan.Zero));
    }

    // ==========================================
    // 2. Proximity Scoring
    // ==========================================

    [Theory]
    [InlineData("Kalutara", "Kalutara", 35)]
    [InlineData("kalutara", "KALUTARA", 35)]
    [InlineData("Colombo", "Kalutara", 20)] // Adjacent neighbor
    [InlineData("Galle", "Kalutara", 20)]   // Adjacent neighbor
    [InlineData("Ratnapura", "Kalutara", 20)] // Adjacent neighbor
    [InlineData("Jaffna", "Kalutara", 5)]   // Distant district
    [InlineData("Anuradhapura", "Kalutara", 5)]
    [InlineData(null, "Kalutara", 5)]
    public void CalculateProximityScore_ReflectsAdjacencyRules(string? volunteerDistrict, string? targetDistrict, int expected)
    {
        var score = VolunteerMatcher.CalculateProximityScore(volunteerDistrict, targetDistrict);
        Assert.Equal(expected, score);
    }

    // ==========================================
    // 3. Availability Coverage
    // ==========================================

    [Fact]
    public void IsWindowCoveredByAvailability_WhenNoSlots_ReturnsTrue()
    {
        var result = VolunteerMatcher.IsWindowCoveredByAvailability([], BaseTime, BaseTime.AddHours(3));
        Assert.True(result);
    }

    [Fact]
    public void IsWindowCoveredByAvailability_WhenSpecificDateMatches_ReturnsTrue()
    {
        var slots = new List<VolunteerAvailability>
        {
            new()
            {
                Type = AvailabilityType.SpecificDate,
                SpecificStartUtc = BaseTime.AddHours(-1),
                SpecificEndUtc = BaseTime.AddHours(5)
            }
        };

        var result = VolunteerMatcher.IsWindowCoveredByAvailability(slots, BaseTime, BaseTime.AddHours(3));
        Assert.True(result);
    }

    [Fact]
    public void IsWindowCoveredByAvailability_WhenSpecificDateTooShort_ReturnsFalse()
    {
        var slots = new List<VolunteerAvailability>
        {
            new()
            {
                Type = AvailabilityType.SpecificDate,
                SpecificStartUtc = BaseTime.AddHours(-1),
                SpecificEndUtc = BaseTime.AddHours(2) // Ends before mission ends at +3h
            }
        };

        var result = VolunteerMatcher.IsWindowCoveredByAvailability(slots, BaseTime, BaseTime.AddHours(3));
        Assert.False(result);
    }

    // ==========================================
    // 4. ScoreVolunteer
    // ==========================================

    [Fact]
    public void ScoreVolunteer_WhenGoldenCandidate_ReturnsHighScoreAndEligible()
    {
        var volunteer = new Volunteer
        {
            Id = Guid.NewGuid(),
            District = "Kalutara",
            IsActive = true,
            Skills =
            [
                new VolunteerSkill { Skill = VolunteerSkillType.BoatOperation, IsVerified = true, Proficiency = SkillProficiency.Expert },
                new VolunteerSkill { Skill = VolunteerSkillType.FirstAid, IsVerified = true, Proficiency = SkillProficiency.Advanced }
            ],
            Assignments =
            [
                new VolunteerAssignment { Status = AssignmentStatus.Completed },
                new VolunteerAssignment { Status = AssignmentStatus.Completed },
                new VolunteerAssignment { Status = AssignmentStatus.Completed }
            ]
        };

        var score = VolunteerMatcher.ScoreVolunteer(
            volunteer,
            requiredSkills: [VolunteerSkillType.BoatOperation, VolunteerSkillType.FirstAid],
            targetDistrict: "Kalutara",
            missionStartUtc: BaseTime,
            missionEndUtc: BaseTime.AddHours(3));

        Assert.True(score.IsEligible);
        Assert.False(score.HasScheduleConflict);
        Assert.Equal(MatchQuality.Excellent, score.Quality);
        Assert.True(score.TotalScore >= VolunteerMatcher.ExcellentThreshold);
        Assert.Equal(2, score.MatchedSkills.Count);
        Assert.Empty(score.MissingSkills);
    }

    [Fact]
    public void ScoreVolunteer_WhenMissingRequiredSkill_IsMarkedIneligible()
    {
        var volunteer = new Volunteer
        {
            Id = Guid.NewGuid(),
            District = "Kalutara",
            IsActive = true,
            Skills = [new VolunteerSkill { Skill = VolunteerSkillType.FoodDistribution }]
        };

        var score = VolunteerMatcher.ScoreVolunteer(
            volunteer,
            requiredSkills: [VolunteerSkillType.BoatOperation],
            targetDistrict: "Kalutara",
            missionStartUtc: BaseTime,
            missionEndUtc: BaseTime.AddHours(3),
            requireAllSkills: true);

        Assert.False(score.IsEligible);
        Assert.Contains(VolunteerSkillType.BoatOperation, score.MissingSkills);
    }

    [Fact]
    public void ScoreVolunteer_WhenHasActiveConflictingAssignment_IsMarkedIneligible()
    {
        var volunteer = new Volunteer
        {
            Id = Guid.NewGuid(),
            District = "Kalutara",
            IsActive = true,
            Skills = [new VolunteerSkill { Skill = VolunteerSkillType.BoatOperation }],
            Assignments =
            [
                new VolunteerAssignment
                {
                    StartTime = BaseTime.AddHours(1),
                    EndTime = BaseTime.AddHours(4),
                    Status = AssignmentStatus.InProgress // Active overlapping assignment
                }
            ]
        };

        var score = VolunteerMatcher.ScoreVolunteer(
            volunteer,
            requiredSkills: [VolunteerSkillType.BoatOperation],
            targetDistrict: "Kalutara",
            missionStartUtc: BaseTime,
            missionEndUtc: BaseTime.AddHours(3));

        Assert.True(score.HasScheduleConflict);
        Assert.False(score.IsEligible);
    }

    // ==========================================
    // 5. ValidateProposal
    // ==========================================

    [Fact]
    public void ValidateProposal_WhenAllValid_PassesAllChecks()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var v1 = new Volunteer
        {
            Id = id1,
            IsActive = true,
            District = "Kalutara",
            Skills = [new VolunteerSkill { Skill = VolunteerSkillType.BoatOperation }]
        };
        var v2 = new Volunteer
        {
            Id = id2,
            IsActive = true,
            District = "Kalutara",
            Skills = [new VolunteerSkill { Skill = VolunteerSkillType.FirstAid }]
        };

        var lookup = new Dictionary<Guid, Volunteer> { [id1] = v1, [id2] = v2 };

        var checks = VolunteerMatcher.ValidateProposal(
            proposedVolunteerIds: [id1, id2],
            volunteersById: lookup,
            requiredSkills: [VolunteerSkillType.BoatOperation, VolunteerSkillType.FirstAid],
            requestedCount: 2,
            missionStartUtc: BaseTime,
            missionEndUtc: BaseTime.AddHours(3),
            summary: "Assigned experienced boat operator and first-aider from Kalutara for flood rescue.");

        Assert.True(VolunteerMatcher.AllPassed(checks));
    }

    [Fact]
    public void ValidateProposal_WhenDoubleBookingConflict_FailsNoDoubleBookingCheck()
    {
        var id1 = Guid.NewGuid();

        var v1 = new Volunteer
        {
            Id = id1,
            IsActive = true,
            District = "Kalutara",
            Skills = [new VolunteerSkill { Skill = VolunteerSkillType.BoatOperation }],
            Assignments =
            [
                new VolunteerAssignment
                {
                    StartTime = BaseTime,
                    EndTime = BaseTime.AddHours(4),
                    Status = AssignmentStatus.Assigned
                }
            ]
        };

        var lookup = new Dictionary<Guid, Volunteer> { [id1] = v1 };

        var checks = VolunteerMatcher.ValidateProposal(
            proposedVolunteerIds: [id1],
            volunteersById: lookup,
            requiredSkills: [VolunteerSkillType.BoatOperation],
            requestedCount: 1,
            missionStartUtc: BaseTime.AddHours(1),
            missionEndUtc: BaseTime.AddHours(3),
            summary: "Assigned boat operator for flood rescue.");

        Assert.False(VolunteerMatcher.AllPassed(checks));
        var check = checks.First(c => c.Name == "no-double-booking");
        Assert.False(check.Passed);
    }

    [Fact]
    public void ValidateProposal_WhenVolunteerIsDuplicated_FailsUniqueVolunteersCheck()
    {
        var id = Guid.NewGuid();
        var volunteer = new Volunteer
        {
            Id = id,
            IsActive = true,
            District = "Kalutara",
            Skills = [new VolunteerSkill { Skill = VolunteerSkillType.BoatOperation }]
        };

        var checks = VolunteerMatcher.ValidateProposal(
            proposedVolunteerIds: [id, id],
            volunteersById: new Dictionary<Guid, Volunteer> { [id] = volunteer },
            requiredSkills: [VolunteerSkillType.BoatOperation],
            requestedCount: 2,
            missionStartUtc: BaseTime,
            missionEndUtc: BaseTime.AddHours(3),
            summary: "The proposed roster contains one volunteer twice.");

        var check = checks.First(c => c.Name == "unique-volunteers");
        Assert.False(check.Passed);
    }

    [Fact]
    public void ValidateProposal_WhenVolunteerAvailabilityDoesNotCoverMission_FailsAvailabilityCheck()
    {
        var id = Guid.NewGuid();
        var volunteer = new Volunteer
        {
            Id = id,
            IsActive = true,
            District = "Kalutara",
            Skills = [new VolunteerSkill { Skill = VolunteerSkillType.BoatOperation }],
            Availabilities =
            [
                new VolunteerAvailability
                {
                    Type = AvailabilityType.SpecificDate,
                    SpecificStartUtc = BaseTime,
                    SpecificEndUtc = BaseTime.AddHours(1)
                }
            ]
        };

        var checks = VolunteerMatcher.ValidateProposal(
            proposedVolunteerIds: [id],
            volunteersById: new Dictionary<Guid, Volunteer> { [id] = volunteer },
            requiredSkills: [VolunteerSkillType.BoatOperation],
            requestedCount: 1,
            missionStartUtc: BaseTime,
            missionEndUtc: BaseTime.AddHours(3),
            summary: "The proposed volunteer is available for the mission.");

        var check = checks.First(c => c.Name == "availability-covered");
        Assert.False(check.Passed);
    }

    [Fact]
    public void ValidateProposal_WhenSummaryContainsLinkOrMarkup_FailsRationaleCheck()
    {
        var id1 = Guid.NewGuid();
        var v1 = new Volunteer
        {
            Id = id1,
            IsActive = true,
            Skills = [new VolunteerSkill { Skill = VolunteerSkillType.BoatOperation }]
        };

        var lookup = new Dictionary<Guid, Volunteer> { [id1] = v1 };

        var checks = VolunteerMatcher.ValidateProposal(
            proposedVolunteerIds: [id1],
            volunteersById: lookup,
            requiredSkills: [VolunteerSkillType.BoatOperation],
            requestedCount: 1,
            missionStartUtc: BaseTime,
            missionEndUtc: BaseTime.AddHours(3),
            summary: "Assigned volunteer with details at https://phishing.site <script>");

        var check = checks.First(c => c.Name == "rationale-present");
        Assert.False(check.Passed);
    }
}
