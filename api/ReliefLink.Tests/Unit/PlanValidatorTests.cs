using ReliefLink.Api.Domain;

namespace ReliefLink.Tests.Unit;

public class PlanValidatorTests
{
    private static PlanValidationContext Ctx(UrgencyLevel urgency = UrgencyLevel.Critical) => new(urgency,
    [
        new PlannerNeed(NeedCategory.Food, 6, null),
        new PlannerNeed(NeedCategory.Water, 12, null),
        new PlannerNeed(NeedCategory.Shelter, 1, null)
    ]);

    private static ReliefPlan Good() => new(
        Summary: "Deliver food, water and shelter to Kalutara.",
        RecommendedUrgency: "Critical",
        TargetResponseHours: 6,
        Needs:
        [
            new PlanNeedLine("Food", 6, "Send 6 food packs."),
            new PlanNeedLine("Water", 12, "Send 12 water bottles."),
            new PlanNeedLine("Shelter", 1, "Place the family in a shelter.")
        ],
        Rationale: ["Flooded home with elderly and children."],
        Risks: ["Road access may be cut."]);

    private static PlanCheck Check(ReliefPlan? plan, string name, UrgencyLevel urgency = UrgencyLevel.Critical) =>
        PlanValidator.Validate(plan, Ctx(urgency)).Single(c => c.Name == name);

    [Fact]
    public void GoodPlan_PassesEveryCheck()
    {
        var checks = PlanValidator.Validate(Good(), Ctx());

        Assert.True(PlanValidator.AllPassed(checks), string.Join("; ", checks.Where(c => !c.Passed).Select(c => $"{c.Name}: {c.Detail}")));
        Assert.Equal(8, checks.Count);
    }

    [Fact]
    public void NoPlan_Fails()
    {
        var checks = PlanValidator.Validate(null, Ctx());

        Assert.False(PlanValidator.AllPassed(checks));
        Assert.Contains(checks, c => c.Name == "plan-present" && !c.Passed);
    }

    // ----- needs-covered -----
    [Fact]
    public void MissingNeed_Fails_AndNamesIt()
    {
        var plan = Good() with { Needs = Good().Needs!.Where(n => n.Category != "Water").ToList() };

        var check = Check(plan, "needs-covered");

        Assert.False(check.Passed);
        Assert.Contains("Water", check.Detail);
    }

    [Fact]
    public void NoNeedsAtAll_Fails()
    {
        Assert.False(Check(Good() with { Needs = null }, "needs-covered").Passed);
        Assert.False(Check(Good() with { Needs = [] }, "needs-covered").Passed);
    }

    // ----- no-invented-needs -----
    [Fact]
    public void NeedThatWasNotRequested_Fails()
    {
        var lines = Good().Needs!.Append(new PlanNeedLine("Medical", 1, "Send a medic.")).ToList();

        var check = Check(Good() with { Needs = lines }, "no-invented-needs");

        Assert.False(check.Passed);
        Assert.Contains("Medical", check.Detail);
    }

    [Theory]
    [InlineData("Gold")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("3")]   // a number must not be accepted as a category
    public void UnknownCategory_Fails(string? category)
    {
        var lines = Good().Needs!.Append(new PlanNeedLine(category, 1, "Do something.")).ToList();

        Assert.False(Check(Good() with { Needs = lines }, "no-invented-needs").Passed);
    }

    [Fact]
    public void RepeatedNeed_Fails()
    {
        var lines = Good().Needs!.Append(new PlanNeedLine("Food", 1, "Send more food.")).ToList();

        Assert.False(Check(Good() with { Needs = lines }, "no-invented-needs").Passed);
    }

    [Fact]
    public void CategoryMatching_IgnoresLetterCase()
    {
        var lines = Good().Needs!.Select(n => n with { Category = n.Category!.ToUpperInvariant() }).ToList();

        var checks = PlanValidator.Validate(Good() with { Needs = lines }, Ctx());

        Assert.True(PlanValidator.AllPassed(checks));
    }

    // ----- quantities-within-request -----
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(7)]   // the family asked for 6
    public void FoodQuantityOutOfRange_Fails(int quantity)
    {
        var lines = Good().Needs!.Select(n => n.Category == "Food" ? n with { Quantity = quantity } : n).ToList();

        Assert.False(Check(Good() with { Needs = lines }, "quantities-within-request").Passed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]   // exactly what was asked for
    public void FoodQuantityInRange_Passes(int quantity)
    {
        var lines = Good().Needs!.Select(n => n.Category == "Food" ? n with { Quantity = quantity } : n).ToList();

        Assert.True(Check(Good() with { Needs = lines }, "quantities-within-request").Passed);
    }

    // ----- urgency-not-downgraded -----
    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    [InlineData("High")]
    public void LoweringTheTriageResult_Fails(string recommended)
    {
        var plan = Good() with { RecommendedUrgency = recommended, TargetResponseHours = 1 };

        Assert.False(Check(plan, "urgency-not-downgraded").Passed);
    }

    [Theory]
    [InlineData("Banana")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1")]   // a number must not be accepted as an urgency level
    public void InvalidUrgency_Fails(string? recommended)
    {
        Assert.False(Check(Good() with { RecommendedUrgency = recommended }, "urgency-not-downgraded").Passed);
    }

    [Fact]
    public void RaisingUrgency_IsAllowed()
    {
        var plan = Good() with { RecommendedUrgency = "critical" };

        Assert.True(Check(plan, "urgency-not-downgraded", UrgencyLevel.High).Passed);
    }

    // ----- response-time-matches-urgency -----
    [Theory]
    [InlineData("Critical", 1, true)]
    [InlineData("Critical", 6, true)]
    [InlineData("Critical", 7, false)]
    [InlineData("High", 12, true)]
    [InlineData("High", 13, false)]
    [InlineData("Medium", 24, true)]
    [InlineData("Medium", 25, false)]
    [InlineData("Low", 72, true)]
    [InlineData("Low", 73, false)]
    [InlineData("Low", 0, false)]
    [InlineData("Low", -5, false)]
    public void ResponseTime_MustFitTheUrgencyLimit(string urgency, int hours, bool expected)
    {
        var plan = Good() with { RecommendedUrgency = urgency, TargetResponseHours = hours };

        Assert.Equal(expected, Check(plan, "response-time-matches-urgency", UrgencyLevel.Low).Passed);
    }

    [Fact]
    public void ResponseTime_UsesTheStricterOfRecommendedAndTriageUrgency()
    {
        // Triage says Critical (6h). Even if the agent claims Low, 72h must not be accepted.
        var lowered = Good() with { RecommendedUrgency = "Low", TargetResponseHours = 72 };
        Assert.False(Check(lowered, "response-time-matches-urgency", UrgencyLevel.Critical).Passed);

        // If the agent raises urgency to Critical for a Low request, the 6h limit applies.
        var raised = Good() with { RecommendedUrgency = "Critical", TargetResponseHours = 20 };
        Assert.False(Check(raised, "response-time-matches-urgency", UrgencyLevel.Low).Passed);
    }

    [Fact]
    public void MaxResponseHours_AreFixed()
    {
        Assert.Equal(6, PlanValidator.MaxResponseHours(UrgencyLevel.Critical));
        Assert.Equal(12, PlanValidator.MaxResponseHours(UrgencyLevel.High));
        Assert.Equal(24, PlanValidator.MaxResponseHours(UrgencyLevel.Medium));
        Assert.Equal(72, PlanValidator.MaxResponseHours(UrgencyLevel.Low));
    }

    // ----- text-present-and-bounded -----
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    public void MissingOrTinySummary_Fails(string? summary)
    {
        Assert.False(Check(Good() with { Summary = summary }, "text-present-and-bounded").Passed);
    }

    [Fact]
    public void HugeSummary_Fails()
    {
        Assert.False(Check(Good() with { Summary = new string('x', PlanValidator.MaxSummaryLength + 1) }, "text-present-and-bounded").Passed);
        Assert.True(Check(Good() with { Summary = new string('x', PlanValidator.MaxSummaryLength) }, "text-present-and-bounded").Passed);
    }

    [Fact]
    public void EmptyRationale_Fails()
    {
        Assert.False(Check(Good() with { Rationale = [] }, "text-present-and-bounded").Passed);
        Assert.False(Check(Good() with { Rationale = null }, "text-present-and-bounded").Passed);
    }

    [Fact]
    public void TooManyRationaleItems_Fails()
    {
        var many = Enumerable.Range(0, PlanValidator.MaxRationaleItems + 1).Select(i => $"Reason {i}").ToList();

        Assert.False(Check(Good() with { Rationale = many }, "text-present-and-bounded").Passed);
    }

    [Fact]
    public void BlankAction_Fails()
    {
        var lines = Good().Needs!.Select(n => n.Category == "Food" ? n with { Action = "  " } : n).ToList();

        Assert.False(Check(Good() with { Needs = lines }, "text-present-and-bounded").Passed);
    }

    // ----- no-links-or-markup -----
    [Theory]
    [InlineData("summary", "Visit https://evil.example/claim now.")]
    [InlineData("summary", "See HTTP://evil.example")]
    [InlineData("summary", "Deliver <b>food</b> quickly.")]
    [InlineData("rationale", "Run <script>alert(1)</script>")]
    [InlineData("risk", "Click http://x.y")]
    [InlineData("action", "Send food and https://x.y/z")]
    public void LinksAndMarkup_AreRejected_InEveryTextField(string field, string text)
    {
        var plan = field switch
        {
            "summary" => Good() with { Summary = text },
            "rationale" => Good() with { Rationale = [text] },
            "risk" => Good() with { Risks = [text] },
            _ => Good() with { Needs = Good().Needs!.Select(n => n.Category == "Food" ? n with { Action = text } : n).ToList() }
        };

        Assert.False(Check(plan, "no-links-or-markup").Passed);
    }

    // ----- reporting -----
    [Fact]
    public void AllProblemsAreReportedTogether()
    {
        var bad = new ReliefPlan("tiny", "Low", 100, [], [], null);

        var failed = PlanValidator.Validate(bad, Ctx()).Where(c => !c.Passed).Select(c => c.Name).ToList();

        Assert.Contains("needs-covered", failed);
        Assert.Contains("urgency-not-downgraded", failed);
        Assert.Contains("response-time-matches-urgency", failed);
        Assert.Contains("text-present-and-bounded", failed);
        Assert.True(failed.Count >= 4);
    }

    [Fact]
    public void Validation_IsDeterministic()
    {
        var first = PlanValidator.Validate(Good(), Ctx());
        var second = PlanValidator.Validate(Good(), Ctx());

        Assert.Equal(first, second);
    }
}
