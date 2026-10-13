using System.Collections.Concurrent;
using System.Text.Json;
using ReliefLink.Api.Domain;
using ReliefLink.Api.Services;

namespace ReliefLink.Tests.Infrastructure;

/// <summary>
/// A stand-in for the Python agent service. By default it returns a correct plan. A test can script a different
/// answer (a bad plan, an outage) for one specific relief request, and read back exactly what the backend sent.
/// </summary>
public class ScriptedPlanner : IPlannerClient
{
    private readonly ConcurrentDictionary<Guid, Func<PlannerInput, PlannerResponse>> _scripts = new();
    private readonly ConcurrentDictionary<Guid, List<PlannerInput>> _calls = new();

    /// <summary>The script may throw <see cref="PlannerUnavailableException"/> to simulate an outage.</summary>
    public void Script(Guid requestId, Func<PlannerInput, PlannerResponse> script) => _scripts[requestId] = script;

    public IReadOnlyList<PlannerInput> CallsFor(Guid requestId) =>
        _calls.TryGetValue(requestId, out var list) ? list.ToList() : [];

    public Task<PlannerResponse> PlanAsync(PlannerInput input, CancellationToken ct)
    {
        _calls.GetOrAdd(input.RequestId, _ => []).Add(input);
        return _scripts.TryGetValue(input.RequestId, out var script)
            ? Task.FromResult(script(input))
            : Task.FromResult(GoodPlan(input));
    }

    /// <summary>A plan that satisfies every validation rule for this input.</summary>
    public static PlannerResponse GoodPlan(PlannerInput input)
    {
        var urgency = Enum.Parse<UrgencyLevel>(input.Urgency);
        var plan = new ReliefPlan(
            Summary: $"Deliver {string.Join(", ", input.Needs.Select(n => n.Category))} to {input.District}.",
            RecommendedUrgency: input.Urgency,
            TargetResponseHours: PlanValidator.MaxResponseHours(urgency),
            Needs: input.Needs.Select(n => new PlanNeedLine(n.Category.ToString(), n.Quantity, $"Prepare {n.Quantity} units of {n.Category}.")).ToList(),
            Rationale: [$"Triage score {input.TriageScore} gives {input.Urgency} urgency."],
            Risks: []);

        return new PlannerResponse(plan, [
            new PlannerToolCall("triage_breakdown",
                JsonSerializer.SerializeToElement(new { requestId = input.RequestId }),
                JsonSerializer.SerializeToElement(new { score = input.TriageScore }), true, 3)
        ]);
    }
}
