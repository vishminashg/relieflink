namespace ReliefLink.Api.Domain;

public record PlanCheck(string Name, bool Passed, string Detail);

/// <summary>What the plan is checked against: the facts in our own database, never what the agent claims.</summary>
public record PlanValidationContext(UrgencyLevel RequestUrgency, IReadOnlyList<PlannerNeed> RequestedNeeds);

/// <summary>
/// Deterministic safety checks run on every plan before a coordinator sees it. This is ordinary code, not AI:
/// an agent (or a prompt injected through a citizen's notes) cannot talk its way past it.
/// All checks always run, so every problem is reported together and can be fed back to the agent.
/// </summary>
public static class PlanValidator
{
    public const int MaxSummaryLength = 1000;
    public const int MaxLineLength = 500;
    public const int MaxRationaleItems = 10;

    /// <summary>The slowest acceptable response time for each urgency level.</summary>
    public static int MaxResponseHours(UrgencyLevel level) => level switch
    {
        UrgencyLevel.Critical => 6,
        UrgencyLevel.High => 12,
        UrgencyLevel.Medium => 24,
        _ => 72
    };

    public static IReadOnlyList<PlanCheck> Validate(ReliefPlan? plan, PlanValidationContext ctx)
    {
        if (plan is null)
            return [new PlanCheck("plan-present", false, "The agent returned no plan.")];

        var checks = new List<PlanCheck> { new("plan-present", true, "A plan was returned.") };
        var lines = plan.Needs ?? [];

        // Parse the categories the agent used; anything we do not recognise is an invented need.
        var parsed = lines.Select(l =>
        {
            var ok = TryParseName<NeedCategory>(l.Category, out var category);
            return (Line: l, Ok: ok, Category: category);
        }).ToList();

        var requested = ctx.RequestedNeeds.ToDictionary(n => n.Category, n => n.Quantity);

        // 1. Every requested need must be covered exactly once.
        var covered = parsed.Where(p => p.Ok).Select(p => p.Category).ToList();
        var missing = requested.Keys.Where(k => !covered.Contains(k)).ToList();
        checks.Add(missing.Count == 0
            ? new PlanCheck("needs-covered", true, "Every requested need has a plan line.")
            : new PlanCheck("needs-covered", false, $"No plan line for: {string.Join(", ", missing)}."));

        // 2. The plan may not invent needs or repeat a need.
        var invented = parsed.Where(p => !p.Ok || !requested.ContainsKey(p.Category))
            .Select(p => p.Line.Category ?? "(empty)").ToList();
        var duplicated = covered.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key.ToString()).ToList();
        checks.Add(invented.Count == 0 && duplicated.Count == 0
            ? new PlanCheck("no-invented-needs", true, "The plan only contains requested needs, once each.")
            : new PlanCheck("no-invented-needs", false,
                $"Not requested: [{string.Join(", ", invented)}]. Repeated: [{string.Join(", ", duplicated)}]."));

        // 3. Quantities must be between 1 and what the family asked for.
        var badQuantities = parsed.Where(p => p.Ok && requested.TryGetValue(p.Category, out var q) &&
                                              (p.Line.Quantity < 1 || p.Line.Quantity > q))
            .Select(p => $"{p.Category} ({p.Line.Quantity} of {requested[p.Category]})").ToList();
        checks.Add(badQuantities.Count == 0
            ? new PlanCheck("quantities-within-request", true, "No quantity is below 1 or above what was requested.")
            : new PlanCheck("quantities-within-request", false, $"Quantity out of range for: {string.Join(", ", badQuantities)}."));

        // 4. The agent may raise urgency but never lower the triage result.
        var urgencyOk = TryParseName<UrgencyLevel>(plan.RecommendedUrgency, out var recommended);
        checks.Add(!urgencyOk
            ? new PlanCheck("urgency-not-downgraded", false, $"'{plan.RecommendedUrgency}' is not a valid urgency level.")
            : recommended < ctx.RequestUrgency
                ? new PlanCheck("urgency-not-downgraded", false,
                    $"Recommended {recommended} is lower than the triage result {ctx.RequestUrgency}.")
                : new PlanCheck("urgency-not-downgraded", true, $"Recommended {recommended}, triage result {ctx.RequestUrgency}."));

        // 5. The promised response time must fit the urgency.
        // The stricter of the two applies, so lowering the urgency can never buy the agent a slower response time.
        var level = urgencyOk && recommended > ctx.RequestUrgency ? recommended : ctx.RequestUrgency;
        var limit = MaxResponseHours(level);
        checks.Add(plan.TargetResponseHours is >= 1 && plan.TargetResponseHours <= limit
            ? new PlanCheck("response-time-matches-urgency", true, $"{plan.TargetResponseHours}h is within the {limit}h limit for {level}.")
            : new PlanCheck("response-time-matches-urgency", false,
                $"{plan.TargetResponseHours}h is not between 1 and {limit}h, the limit for {level}."));

        // 6. Text must be present and a sensible size.
        var problems = new List<string>();
        var summary = plan.Summary?.Trim();
        if (string.IsNullOrEmpty(summary) || summary.Length < 10) problems.Add("summary is missing or too short");
        else if (summary.Length > MaxSummaryLength) problems.Add($"summary is longer than {MaxSummaryLength} characters");
        var rationale = plan.Rationale ?? [];
        if (rationale.Count == 0) problems.Add("rationale is empty");
        if (rationale.Count > MaxRationaleItems) problems.Add($"more than {MaxRationaleItems} rationale items");
        if (rationale.Any(r => string.IsNullOrWhiteSpace(r) || r.Length > MaxLineLength)) problems.Add("a rationale item is empty or too long");
        if (lines.Any(l => string.IsNullOrWhiteSpace(l.Action) || l.Action.Length > MaxLineLength)) problems.Add("a plan line has no action or a too-long action");
        checks.Add(problems.Count == 0
            ? new PlanCheck("text-present-and-bounded", true, "Summary, rationale and actions are present and within limits.")
            : new PlanCheck("text-present-and-bounded", false, string.Join("; ", problems) + "."));

        // 7. No links or markup: the plan is shown to coordinators and must not carry anything clickable or executable.
        var allText = new List<string?> { plan.Summary };
        allText.AddRange(rationale);
        allText.AddRange(plan.Risks ?? []);
        allText.AddRange(lines.Select(l => l.Action));
        var unsafeText = allText.Any(t => t is not null &&
            (t.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
             t.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
             t.Contains('<') || t.Contains('>')));
        checks.Add(unsafeText
            ? new PlanCheck("no-links-or-markup", false, "The plan contains a link or markup, which is not allowed.")
            : new PlanCheck("no-links-or-markup", true, "No links or markup."));

        return checks;
    }

    /// <summary>
    /// Accepts only the real names (any letter case). Enum.TryParse would also accept numbers such as "1",
    /// which would let an agent smuggle in a value we never intended.
    /// </summary>
    private static bool TryParseName<T>(string? text, out T value) where T : struct, Enum
    {
        foreach (var name in Enum.GetNames<T>())
            if (string.Equals(name, text?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                value = Enum.Parse<T>(name);
                return true;
            }

        value = default;
        return false;
    }

    public static bool AllPassed(IEnumerable<PlanCheck> checks) => checks.All(c => c.Passed);
}
