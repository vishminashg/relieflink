using ReliefLink.Api.Domain;

namespace ReliefLink.Tests.Unit;

public class TriageScorerTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);

    private static TriageInput Input(
        int size = 1, int elderly = 0, int children = 0, bool flooded = false,
        NeedCategory[]? needs = null, DateTime? neededBy = null, DateTime? createdAt = null) =>
        new(size, elderly, children, flooded, needs ?? [], neededBy, createdAt ?? Now);

    [Fact]
    public void GoldenCase_FamilyOfSixWithElderlyAndChildren_IsCritical()
    {
        // household 9 + vulnerable 18 + flooded 10 + needs 24 + deadline 10 = 71
        var result = TriageScorer.Score(Input(
            size: 6, elderly: 2, children: 2, flooded: true,
            needs: [NeedCategory.Food, NeedCategory.Water, NeedCategory.Shelter],
            neededBy: Now.AddHours(24)), Now);

        Assert.Equal(71, result.Score);
        Assert.Equal(UrgencyLevel.Critical, result.Level);
    }

    [Fact]
    public void SameFamilyWithoutChildren_IsHigh()
    {
        // 9 + 10 + 10 + 24 + 10 = 63
        var result = TriageScorer.Score(Input(
            size: 6, elderly: 2, flooded: true,
            needs: [NeedCategory.Food, NeedCategory.Water, NeedCategory.Shelter],
            neededBy: Now.AddHours(24)), Now);

        Assert.Equal(63, result.Score);
        Assert.Equal(UrgencyLevel.High, result.Level);
    }

    [Fact]
    public void SameInput_AlwaysGivesSameResult()
    {
        var input = Input(size: 4, elderly: 1, flooded: true, needs: [NeedCategory.Medical], neededBy: Now.AddHours(10));

        var a = TriageScorer.Score(input, Now);
        var b = TriageScorer.Score(input, Now);

        Assert.Equal(a.Score, b.Score);
        Assert.Equal(a.Level, b.Level);
        Assert.Equal(a.Reasons, b.Reasons);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(6, 9)]
    [InlineData(10, 15)]
    [InlineData(50, 15)] // capped
    public void HouseholdPoints_AreOnePointFivePerPerson_CappedAtFifteen(int size, int expected)
    {
        Assert.Equal(expected, TriageScorer.Score(Input(size: size), Now).Score);
    }

    [Fact]
    public void VulnerablePoints_AreCappedAtTwentyFive()
    {
        // household 10 -> 15, ten elderly would be 50 but is capped at 25
        var result = TriageScorer.Score(Input(size: 10, elderly: 10), Now);

        Assert.Equal(40, result.Score);
        Assert.Equal(UrgencyLevel.Medium, result.Level);
    }

    [Fact]
    public void NeedPoints_AreCappedAtThirty()
    {
        var all = Enum.GetValues<NeedCategory>(); // 15+10+8+6+3+2 = 44, capped at 30

        var result = TriageScorer.Score(Input(size: 1, needs: all), Now);

        Assert.Equal(1 + 30, result.Score);
    }

    [Fact]
    public void DuplicateNeedCategories_AreCountedOnce()
    {
        var result = TriageScorer.Score(Input(size: 1, needs: [NeedCategory.Medical, NeedCategory.Medical]), Now);

        Assert.Equal(1 + 15, result.Score);
    }

    [Theory]
    [InlineData(-5, 15)]    // already overdue
    [InlineData(12, 15)]
    [InlineData(12.01, 10)]
    [InlineData(24, 10)]
    [InlineData(24.01, 5)]
    [InlineData(48, 5)]
    [InlineData(48.01, 0)]
    [InlineData(200, 0)]
    public void DeadlinePoints_FollowTheBoundaries(double hoursFromNow, int expectedDeadlinePoints)
    {
        var result = TriageScorer.Score(Input(size: 1, neededBy: Now.AddHours(hoursFromNow)), Now);

        Assert.Equal(1 + expectedDeadlinePoints, result.Score);
    }

    [Fact]
    public void NoDeadline_AddsNoDeadlinePoints()
    {
        Assert.Equal(1, TriageScorer.Score(Input(size: 1, neededBy: null), Now).Score);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2.9, 0)]
    [InlineData(3, 1)]
    [InlineData(8, 2)]
    [InlineData(15, 5)]
    [InlineData(100, 5)] // capped
    public void WaitingPoints_AreOnePointPerThreeHours_CappedAtFive(double hoursWaiting, int expectedWaitingPoints)
    {
        var created = Now.AddHours(-hoursWaiting);

        var result = TriageScorer.Score(Input(size: 1, createdAt: created), Now);

        Assert.Equal(1 + expectedWaitingPoints, result.Score);
    }

    [Theory]
    [InlineData(0, UrgencyLevel.Low)]
    [InlineData(29, UrgencyLevel.Low)]
    [InlineData(30, UrgencyLevel.Medium)]
    [InlineData(49, UrgencyLevel.Medium)]
    [InlineData(50, UrgencyLevel.High)]
    [InlineData(69, UrgencyLevel.High)]
    [InlineData(70, UrgencyLevel.Critical)]
    [InlineData(100, UrgencyLevel.Critical)]
    public void LevelThresholds_AreExact(int score, UrgencyLevel expected)
    {
        Assert.Equal(expected, TriageScorer.LevelFor(score));
    }

    [Fact]
    public void WorstPossibleCase_ScoresExactlyOneHundred_NeverMore()
    {
        var result = TriageScorer.Score(new TriageInput(
            50, 25, 25, true, Enum.GetValues<NeedCategory>(), Now, Now.AddHours(-1000)), Now);

        Assert.Equal(100, result.Score);
        Assert.Equal(UrgencyLevel.Critical, result.Level);
    }

    [Fact]
    public void Reasons_ExplainEachContributingFactor()
    {
        var result = TriageScorer.Score(Input(
            size: 6, elderly: 2, children: 2, flooded: true,
            needs: [NeedCategory.Food], neededBy: Now.AddHours(10)), Now);

        Assert.Contains(result.Reasons, r => r.Contains("Household of 6"));
        Assert.Contains(result.Reasons, r => r.Contains("elderly"));
        Assert.Contains(result.Reasons, r => r.Contains("flooded"));
        Assert.Contains(result.Reasons, r => r.Contains("Food"));
        Assert.Contains(result.Reasons, r => r.Contains("12 hours"));
    }

    [Fact]
    public void FactorsThatDoNotApply_ProduceNoReason()
    {
        var result = TriageScorer.Score(Input(size: 1), Now);

        Assert.Single(result.Reasons); // only the household line
    }
}
