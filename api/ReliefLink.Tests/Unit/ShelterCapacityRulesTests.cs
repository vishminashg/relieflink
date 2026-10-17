using ReliefLink.Api.Domain;

namespace ReliefLink.Tests.Unit;

public sealed class ShelterCapacityRulesTests
{
    [Fact]
    public void OccupiedSpaces_counts_only_active_check_ins()
    {
        var checkIns = new[]
        {
            new ShelterCheckIn { HouseholdSize = 4 },
            new ShelterCheckIn { HouseholdSize = 2, CheckedOutAt = DateTime.UtcNow }
        };

        Assert.Equal(4, ShelterCapacityRules.OccupiedSpaces(checkIns));
    }

    [Theory]
    [InlineData(10, 6, 4, true)]
    [InlineData(10, 6, 5, false)]
    [InlineData(10, 6, 0, false)]
    public void CanCheckIn_enforces_capacity_and_positive_household_size(
        int capacity, int occupied, int householdSize, bool expected)
    {
        Assert.Equal(expected, ShelterCapacityRules.CanCheckIn(capacity, occupied, householdSize));
    }
}
