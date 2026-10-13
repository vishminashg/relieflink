namespace ReliefLink.Api.Domain;

public record TriageInput(
    int HouseholdSize,
    int ElderlyCount,
    int ChildrenCount,
    bool HomeFlooded,
    IReadOnlyCollection<NeedCategory> Needs,
    DateTime? NeededBy,
    DateTime CreatedAt);

public record TriageResult(int Score, UrgencyLevel Level, IReadOnlyList<string> Reasons);

/// <summary>
/// Deterministic, explainable urgency score from 0 to 100. No AI is involved, so the same request always
/// gets the same score and a coordinator can see exactly why.
///
///   Household size      up to 15   (1.5 points per person)
///   Vulnerable members  up to 25   (5 per elderly person, 4 per child)
///   Home flooded            10
///   Needs               up to 30   (Medical 15, Shelter 10, Water 8, Food 6, Sanitation 3, Clothing 2)
///   Deadline            up to 15   (within 12h 15, within 24h 10, within 48h 5)
///   Waiting time        up to  5   (1 point per 3 hours waiting)
///
///   Critical >= 70, High >= 50, Medium >= 30, otherwise Low.
/// </summary>
public static class TriageScorer
{
    public const int CriticalThreshold = 70;
    public const int HighThreshold = 50;
    public const int MediumThreshold = 30;

    private static readonly Dictionary<NeedCategory, int> NeedWeights = new()
    {
        [NeedCategory.Medical] = 15,
        [NeedCategory.Shelter] = 10,
        [NeedCategory.Water] = 8,
        [NeedCategory.Food] = 6,
        [NeedCategory.Sanitation] = 3,
        [NeedCategory.Clothing] = 2
    };

    public static TriageResult Score(TriageInput input, DateTime now)
    {
        var reasons = new List<string>();
        var total = 0;

        var household = Math.Min(15, input.HouseholdSize * 3 / 2);
        if (household > 0)
        {
            total += household;
            reasons.Add($"Household of {input.HouseholdSize} people (+{household})");
        }

        var vulnerable = Math.Min(25, input.ElderlyCount * 5 + input.ChildrenCount * 4);
        if (vulnerable > 0)
        {
            total += vulnerable;
            reasons.Add($"{input.ElderlyCount} elderly and {input.ChildrenCount} children in the household (+{vulnerable})");
        }

        if (input.HomeFlooded)
        {
            total += 10;
            reasons.Add("Home is flooded (+10)");
        }

        var needs = Math.Min(30, input.Needs.Distinct().Sum(n => NeedWeights.GetValueOrDefault(n)));
        if (needs > 0)
        {
            total += needs;
            reasons.Add($"Needs: {string.Join(", ", input.Needs.Distinct().OrderBy(n => n))} (+{needs})");
        }

        if (input.NeededBy is { } neededBy)
        {
            var hoursLeft = (neededBy - now).TotalHours;
            var deadline = hoursLeft <= 12 ? 15 : hoursLeft <= 24 ? 10 : hoursLeft <= 48 ? 5 : 0;
            if (deadline > 0)
            {
                total += deadline;
                reasons.Add($"Help needed within {(deadline == 15 ? "12" : deadline == 10 ? "24" : "48")} hours (+{deadline})");
            }
        }

        var hoursWaiting = (now - input.CreatedAt).TotalHours;
        var waiting = hoursWaiting > 0 ? Math.Min(5, (int)(hoursWaiting / 3)) : 0;
        if (waiting > 0)
        {
            total += waiting;
            reasons.Add($"Waiting {(int)hoursWaiting} hours (+{waiting})");
        }

        total = Math.Clamp(total, 0, 100);
        return new TriageResult(total, LevelFor(total), reasons);
    }

    public static UrgencyLevel LevelFor(int score) =>
        score >= CriticalThreshold ? UrgencyLevel.Critical
        : score >= HighThreshold ? UrgencyLevel.High
        : score >= MediumThreshold ? UrgencyLevel.Medium
        : UrgencyLevel.Low;
}
