using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Domain;
using ReliefLink.Api.Services;
using Xunit;

namespace ReliefLink.Tests.Unit;

public class VolunteerValidatorTests
{
    private static readonly DateTime FixedNow = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ValidateProfile_WhenValid_ReturnsNoErrors()
    {
        var input = new VolunteerProfileInput(
            District: "Kalutara",
            EmergencyContactName: "Kasun Silva",
            EmergencyContactPhone: "0771234567",
            MaxHoursPerWeek: 40,
            Notes: "Available for boat rescue",
            Skills: [new VolunteerSkillInput(VolunteerSkillType.BoatOperation)]);

        var errors = VolunteerValidator.ValidateProfile(input);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("InvalidDistrict", "Choose a valid Sri Lankan district.")]
    [InlineData("", "Choose a valid Sri Lankan district.")]
    public void ValidateProfile_WhenInvalidDistrict_ReturnsError(string district, string expectedMessage)
    {
        var input = new VolunteerProfileInput(
            District: district,
            EmergencyContactName: "Kasun Silva",
            EmergencyContactPhone: "0771234567");

        var errors = VolunteerValidator.ValidateProfile(input);
        Assert.True(errors.ContainsKey(nameof(input.District)));
        Assert.Contains(expectedMessage, errors[nameof(input.District)]);
    }

    [Theory]
    [InlineData("A")] // Too short
    [InlineData("")]
    public void ValidateProfile_WhenEmergencyContactNameInvalid_ReturnsError(string name)
    {
        var input = new VolunteerProfileInput(
            District: "Kalutara",
            EmergencyContactName: name,
            EmergencyContactPhone: "0771234567");

        var errors = VolunteerValidator.ValidateProfile(input);
        Assert.True(errors.ContainsKey(nameof(input.EmergencyContactName)));
    }

    [Theory]
    [InlineData("123")] // Too short
    [InlineData("")]
    public void ValidateProfile_WhenEmergencyPhoneInvalid_ReturnsError(string phone)
    {
        var input = new VolunteerProfileInput(
            District: "Kalutara",
            EmergencyContactName: "Kasun Silva",
            EmergencyContactPhone: phone);

        var errors = VolunteerValidator.ValidateProfile(input);
        Assert.True(errors.ContainsKey(nameof(input.EmergencyContactPhone)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(169)]
    [InlineData(-10)]
    public void ValidateProfile_WhenMaxHoursOutOfRange_ReturnsError(int hours)
    {
        var input = new VolunteerProfileInput(
            District: "Kalutara",
            EmergencyContactName: "Kasun Silva",
            EmergencyContactPhone: "0771234567",
            MaxHoursPerWeek: hours);

        var errors = VolunteerValidator.ValidateProfile(input);
        Assert.True(errors.ContainsKey(nameof(input.MaxHoursPerWeek)));
    }

    [Fact]
    public void ValidateProfile_WhenDuplicateSkills_ReturnsError()
    {
        var input = new VolunteerProfileInput(
            District: "Kalutara",
            EmergencyContactName: "Kasun Silva",
            EmergencyContactPhone: "0771234567",
            Skills:
            [
                new VolunteerSkillInput(VolunteerSkillType.BoatOperation),
                new VolunteerSkillInput(VolunteerSkillType.BoatOperation)
            ]);

        var errors = VolunteerValidator.ValidateProfile(input);
        Assert.True(errors.ContainsKey(nameof(input.Skills)));
        Assert.Contains("Each skill can only be registered once.", errors[nameof(input.Skills)]);
    }

    [Fact]
    public void ValidateAvailabilities_WhenRecurringMissingTimes_ReturnsError()
    {
        var input = new List<VolunteerAvailabilityInput>
        {
            new(AvailabilityType.RecurringWeekly, DayOfWeek: DayOfWeek.Saturday, StartTime: null, EndTime: null)
        };

        var errors = VolunteerValidator.ValidateAvailabilities(input, FixedNow);
        Assert.True(errors.ContainsKey("Availabilities[0]"));
    }

    [Fact]
    public void ValidateAvailabilities_WhenRecurringEndTimeBeforeStartTime_ReturnsError()
    {
        var input = new List<VolunteerAvailabilityInput>
        {
            new(AvailabilityType.RecurringWeekly,
                DayOfWeek: DayOfWeek.Saturday,
                StartTime: new TimeOnly(17, 0),
                EndTime: new TimeOnly(9, 0))
        };

        var errors = VolunteerValidator.ValidateAvailabilities(input, FixedNow);
        Assert.True(errors.ContainsKey("Availabilities[0]"));
        Assert.Contains("EndTime must be strictly after StartTime.", errors["Availabilities[0]"]);
    }

    [Fact]
    public void ValidateAvailabilities_WhenSpecificEndBeforeStart_ReturnsError()
    {
        var input = new List<VolunteerAvailabilityInput>
        {
            new(AvailabilityType.SpecificDate,
                SpecificStartUtc: FixedNow.AddHours(5),
                SpecificEndUtc: FixedNow.AddHours(2))
        };

        var errors = VolunteerValidator.ValidateAvailabilities(input, FixedNow);
        Assert.True(errors.ContainsKey("Availabilities[0]"));
        Assert.Contains("SpecificEndUtc must be strictly after SpecificStartUtc.", errors["Availabilities[0]"]);
    }

    [Fact]
    public void ValidateAssignment_WhenEndTimeBeforeStartTime_ReturnsError()
    {
        var input = new CreateAssignmentInput(
            VolunteerId: Guid.NewGuid(),
            ReliefRequestId: null,
            StartTime: FixedNow.AddHours(4),
            EndTime: FixedNow.AddHours(1),
            RoleOrTask: "Boat rescue",
            Notes: null);

        var errors = VolunteerValidator.ValidateAssignment(input, FixedNow);
        Assert.True(errors.ContainsKey(nameof(input.EndTime)));
    }

    [Fact]
    public void ValidateAssignment_WhenVolunteerIdEmpty_ReturnsError()
    {
        var input = new CreateAssignmentInput(
            VolunteerId: Guid.Empty,
            ReliefRequestId: null,
            StartTime: FixedNow,
            EndTime: FixedNow.AddHours(4),
            RoleOrTask: "Distribution",
            Notes: null);

        var errors = VolunteerValidator.ValidateAssignment(input, FixedNow);
        Assert.True(errors.ContainsKey(nameof(input.VolunteerId)));
    }
}
