using System.Text.Json;

namespace ReliefLink.Api.Domain;

// These records are the contract between ASP.NET Core and the Python agent service.
// Everything the agent sends back is untrusted data until PlanValidator has checked it.

/// <summary>One need of the request, as the agent sees it.</summary>
public record PlannerNeed(NeedCategory Category, int Quantity, string? Description);

/// <summary>Why the previous attempt was not accepted. Sent to the agent so it can improve its next plan.</summary>
public record PlannerFeedback(IReadOnlyList<string> ValidationFailures, string? CoordinatorComment);

/// <summary>What the backend sends to the Planner agent. Notes are free text written by a citizen and are untrusted.</summary>
public record PlannerInput(
    Guid RequestId,
    string District,
    int HouseholdSize,
    int ElderlyCount,
    int ChildrenCount,
    bool HomeFlooded,
    DateTime? NeededBy,
    int TriageScore,
    string Urgency,
    IReadOnlyList<string> TriageReasons,
    IReadOnlyList<PlannerNeed> Needs,
    string? Notes,
    int Attempt,
    PlannerFeedback? Feedback);

/// <summary>One line of the plan. Category is a plain string because the agent may return anything.</summary>
public record PlanNeedLine(string? Category, int Quantity, string? Action);

/// <summary>The plan proposed by the Planner agent.</summary>
public record ReliefPlan(
    string? Summary,
    string? RecommendedUrgency,
    int TargetResponseHours,
    List<PlanNeedLine>? Needs,
    List<string>? Rationale,
    List<string>? Risks);

/// <summary>A tool the agent used while planning. Stored so a coordinator can see exactly what the agent looked at.</summary>
public record PlannerToolCall(string? Name, JsonElement? Arguments, JsonElement? Result, bool Succeeded, int? DurationMs);

public record PlannerResponse(ReliefPlan? Plan, List<PlannerToolCall>? ToolCalls);

/// <summary>Thrown when the agent service cannot be reached, times out, or answers with something unusable.</summary>
public class PlannerUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
