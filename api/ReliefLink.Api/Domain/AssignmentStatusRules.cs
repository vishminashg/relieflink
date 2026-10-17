namespace ReliefLink.Api.Domain;

public class InvalidAssignmentTransitionException(AssignmentStatus from, AssignmentStatus to)
    : Exception($"Illegal volunteer assignment transition {from} -> {to}.")
{
    public AssignmentStatus From { get; } = from;
    public AssignmentStatus To { get; } = to;
}

/// <summary>
/// Legal lifecycle transitions for volunteer assignments:
///   Assigned -> InProgress -> Completed
///   Assigned | InProgress -> Cancelled
/// Completed and Cancelled are terminal states.
/// </summary>
public static class AssignmentStatusRules
{
    private static readonly Dictionary<AssignmentStatus, AssignmentStatus[]> Allowed = new()
    {
        [AssignmentStatus.Assigned] = [AssignmentStatus.InProgress, AssignmentStatus.Cancelled],
        [AssignmentStatus.InProgress] = [AssignmentStatus.Completed, AssignmentStatus.Cancelled],
        [AssignmentStatus.Completed] = [],
        [AssignmentStatus.Cancelled] = []
    };

    public static bool CanTransition(AssignmentStatus from, AssignmentStatus to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public static bool IsActive(AssignmentStatus status) =>
        status is AssignmentStatus.Assigned or AssignmentStatus.InProgress;

    public static bool IsTerminal(AssignmentStatus status) =>
        status is AssignmentStatus.Completed or AssignmentStatus.Cancelled;

    public static void Transition(VolunteerAssignment assignment, AssignmentStatus to)
    {
        if (!CanTransition(assignment.Status, to))
            throw new InvalidAssignmentTransitionException(assignment.Status, to);

        assignment.Status = to;
    }
}
