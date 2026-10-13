namespace ReliefLink.Api.Domain;

public class InvalidRequestTransitionException(RequestStatus from, RequestStatus to)
    : Exception($"Illegal relief request transition {from} -> {to}.")
{
    public RequestStatus From { get; } = from;
    public RequestStatus To { get; } = to;
}

/// <summary>
/// Lifecycle of a relief request:
///   Submitted -> UnderReview -> PlanReady -> Approved -> InProgress -> Fulfilled
/// A request can be Cancelled or Rejected while it is still waiting for a decision.
/// If the agent workflow fails safely, UnderReview goes back to Submitted. If the coordinator asks for a
/// revision, PlanReady goes back to UnderReview.
/// </summary>
public static class RequestStatusRules
{
    private static readonly Dictionary<RequestStatus, RequestStatus[]> Allowed = new()
    {
        [RequestStatus.Submitted] = [RequestStatus.UnderReview, RequestStatus.Cancelled, RequestStatus.Rejected],
        [RequestStatus.UnderReview] =
            [RequestStatus.PlanReady, RequestStatus.Submitted, RequestStatus.Cancelled, RequestStatus.Rejected],
        [RequestStatus.PlanReady] =
            [RequestStatus.Approved, RequestStatus.UnderReview, RequestStatus.Cancelled, RequestStatus.Rejected],
        [RequestStatus.Approved] = [RequestStatus.InProgress],
        [RequestStatus.InProgress] = [RequestStatus.Fulfilled],
        [RequestStatus.Fulfilled] = [],
        [RequestStatus.Rejected] = [],
        [RequestStatus.Cancelled] = []
    };

    public static bool CanTransition(RequestStatus from, RequestStatus to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>Only a request nobody has started working on can be edited by its owner.</summary>
    public static bool CanEdit(RequestStatus status) => status == RequestStatus.Submitted;

    public static bool CanCancel(RequestStatus status) => CanTransition(status, RequestStatus.Cancelled);

    /// <summary>Urgency can be re-triaged only while the request is still waiting for a decision.</summary>
    public static bool CanTriage(RequestStatus status) =>
        status is RequestStatus.Submitted or RequestStatus.UnderReview or RequestStatus.PlanReady;

    public static void Transition(ReliefRequest request, RequestStatus to)
    {
        if (!CanTransition(request.Status, to))
            throw new InvalidRequestTransitionException(request.Status, to);
        request.Status = to;
    }
}
