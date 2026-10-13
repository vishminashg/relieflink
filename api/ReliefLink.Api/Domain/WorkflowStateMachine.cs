namespace ReliefLink.Api.Domain;

/// <summary>
/// The only legal workflow transitions. Every status change in the API goes through
/// <see cref="Transition"/>, so an agent or a client cannot skip the approval gate.
///
///   Submitted -> Planning -> Proposed -> Validating -> PendingCoordinatorApproval
///   PendingCoordinatorApproval -> Approved -> Executed
///   PendingCoordinatorApproval -> RevisionRequested -> Planning
///   PendingCoordinatorApproval -> Rejected
///   Planning | Proposed | Validating -> FailedSafe
///   Validating -> Planning (validation failed, retry within the limit)
/// </summary>
public static class WorkflowStateMachine
{
    public const int MaxRevisions = 2;
    public const int MaxRetries = 2;

    private static readonly Dictionary<WorkflowStatus, WorkflowStatus[]> Allowed = new()
    {
        [WorkflowStatus.Submitted] = [WorkflowStatus.Planning, WorkflowStatus.FailedSafe],
        [WorkflowStatus.Planning] = [WorkflowStatus.Proposed, WorkflowStatus.FailedSafe],
        [WorkflowStatus.Proposed] = [WorkflowStatus.Validating, WorkflowStatus.FailedSafe],
        [WorkflowStatus.Validating] =
        [
            WorkflowStatus.PendingCoordinatorApproval,
            WorkflowStatus.Planning,
            WorkflowStatus.FailedSafe
        ],
        [WorkflowStatus.PendingCoordinatorApproval] =
        [
            WorkflowStatus.Approved,
            WorkflowStatus.Rejected,
            WorkflowStatus.RevisionRequested
        ],
        [WorkflowStatus.RevisionRequested] = [WorkflowStatus.Planning, WorkflowStatus.FailedSafe],
        [WorkflowStatus.Approved] = [WorkflowStatus.Executed, WorkflowStatus.FailedSafe],
        [WorkflowStatus.Executed] = [],
        [WorkflowStatus.Rejected] = [],
        [WorkflowStatus.FailedSafe] = []
    };

    public static bool CanTransition(WorkflowStatus from, WorkflowStatus to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public static bool IsTerminal(WorkflowStatus status) =>
        Allowed.TryGetValue(status, out var next) && next.Length == 0;

    /// <summary>Applies the transition or throws <see cref="InvalidWorkflowTransitionException"/>.</summary>
    public static void Transition(AgentWorkflow workflow, WorkflowStatus to)
    {
        if (!CanTransition(workflow.Status, to))
            throw new InvalidWorkflowTransitionException(workflow.Status, to);

        workflow.Status = to;
    }
}

public class InvalidWorkflowTransitionException(WorkflowStatus from, WorkflowStatus to)
    : Exception($"Illegal workflow transition {from} -> {to}.")
{
    public WorkflowStatus From { get; } = from;
    public WorkflowStatus To { get; } = to;
}
