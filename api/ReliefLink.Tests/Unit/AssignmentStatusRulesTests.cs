using ReliefLink.Api.Domain;
using Xunit;

namespace ReliefLink.Tests.Unit;

public class AssignmentStatusRulesTests
{
    [Theory]
    [InlineData(AssignmentStatus.Assigned, AssignmentStatus.InProgress, true)]
    [InlineData(AssignmentStatus.Assigned, AssignmentStatus.Cancelled, true)]
    [InlineData(AssignmentStatus.InProgress, AssignmentStatus.Completed, true)]
    [InlineData(AssignmentStatus.InProgress, AssignmentStatus.Cancelled, true)]
    [InlineData(AssignmentStatus.Completed, AssignmentStatus.InProgress, false)]
    [InlineData(AssignmentStatus.Completed, AssignmentStatus.Cancelled, false)]
    [InlineData(AssignmentStatus.Cancelled, AssignmentStatus.Assigned, false)]
    [InlineData(AssignmentStatus.Cancelled, AssignmentStatus.InProgress, false)]
    [InlineData(AssignmentStatus.Assigned, AssignmentStatus.Completed, false)]
    public void CanTransition_ReflectsAllowedLifecycle(AssignmentStatus from, AssignmentStatus to, bool expected)
    {
        Assert.Equal(expected, AssignmentStatusRules.CanTransition(from, to));
    }

    [Fact]
    public void Transition_WhenAllowed_UpdatesStatus()
    {
        var assignment = new VolunteerAssignment { Status = AssignmentStatus.Assigned };
        AssignmentStatusRules.Transition(assignment, AssignmentStatus.InProgress);
        Assert.Equal(AssignmentStatus.InProgress, assignment.Status);

        AssignmentStatusRules.Transition(assignment, AssignmentStatus.Completed);
        Assert.Equal(AssignmentStatus.Completed, assignment.Status);
    }

    [Fact]
    public void Transition_WhenIllegal_ThrowsInvalidAssignmentTransitionException()
    {
        var assignment = new VolunteerAssignment { Status = AssignmentStatus.Completed };
        var ex = Assert.Throws<InvalidAssignmentTransitionException>(() =>
            AssignmentStatusRules.Transition(assignment, AssignmentStatus.InProgress));

        Assert.Equal(AssignmentStatus.Completed, ex.From);
        Assert.Equal(AssignmentStatus.InProgress, ex.To);
    }

    [Theory]
    [InlineData(AssignmentStatus.Assigned, true)]
    [InlineData(AssignmentStatus.InProgress, true)]
    [InlineData(AssignmentStatus.Completed, false)]
    [InlineData(AssignmentStatus.Cancelled, false)]
    public void IsActive_ReturnsTrueOnlyForActiveMissions(AssignmentStatus status, bool expected)
    {
        Assert.Equal(expected, AssignmentStatusRules.IsActive(status));
    }

    [Theory]
    [InlineData(AssignmentStatus.Assigned, false)]
    [InlineData(AssignmentStatus.InProgress, false)]
    [InlineData(AssignmentStatus.Completed, true)]
    [InlineData(AssignmentStatus.Cancelled, true)]
    public void IsTerminal_ReturnsTrueOnlyForFinishedMissions(AssignmentStatus status, bool expected)
    {
        Assert.Equal(expected, AssignmentStatusRules.IsTerminal(status));
    }
}
