using ReliefLink.Api.Domain;

namespace ReliefLink.Tests.Unit;

public class WorkflowStateMachineTests
{
    [Fact]
    public void HappyPath_ThroughApproval_IsAllowed()
    {
        var path = new[]
        {
            WorkflowStatus.Submitted, WorkflowStatus.Planning, WorkflowStatus.Proposed,
            WorkflowStatus.Validating, WorkflowStatus.PendingCoordinatorApproval,
            WorkflowStatus.Approved, WorkflowStatus.Executed
        };

        for (var i = 0; i < path.Length - 1; i++)
            Assert.True(WorkflowStateMachine.CanTransition(path[i], path[i + 1]), $"{path[i]} -> {path[i + 1]}");
    }

    [Theory]
    [InlineData(WorkflowStatus.Submitted, WorkflowStatus.Approved)]
    [InlineData(WorkflowStatus.Planning, WorkflowStatus.Approved)]
    [InlineData(WorkflowStatus.Proposed, WorkflowStatus.Approved)]
    [InlineData(WorkflowStatus.Validating, WorkflowStatus.Approved)]
    [InlineData(WorkflowStatus.Validating, WorkflowStatus.Executed)]
    [InlineData(WorkflowStatus.Submitted, WorkflowStatus.Executed)]
    [InlineData(WorkflowStatus.PendingCoordinatorApproval, WorkflowStatus.Executed)]
    public void ApprovalGate_CannotBeSkipped(WorkflowStatus from, WorkflowStatus to)
    {
        Assert.False(WorkflowStateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData(WorkflowStatus.Executed)]
    [InlineData(WorkflowStatus.Rejected)]
    [InlineData(WorkflowStatus.FailedSafe)]
    public void TerminalStates_HaveNoExits(WorkflowStatus terminal)
    {
        Assert.True(WorkflowStateMachine.IsTerminal(terminal));
        foreach (var target in Enum.GetValues<WorkflowStatus>())
            Assert.False(WorkflowStateMachine.CanTransition(terminal, target), $"{terminal} -> {target}");
    }

    [Fact]
    public void NonTerminalStates_AreNotMarkedTerminal()
    {
        Assert.False(WorkflowStateMachine.IsTerminal(WorkflowStatus.PendingCoordinatorApproval));
        Assert.False(WorkflowStateMachine.IsTerminal(WorkflowStatus.Approved));
    }

    [Fact]
    public void Revision_LoopsBackToPlanning()
    {
        Assert.True(WorkflowStateMachine.CanTransition(WorkflowStatus.PendingCoordinatorApproval, WorkflowStatus.RevisionRequested));
        Assert.True(WorkflowStateMachine.CanTransition(WorkflowStatus.RevisionRequested, WorkflowStatus.Planning));
    }

    [Fact]
    public void ValidationFailure_CanRetryPlanning_OrFailSafe()
    {
        Assert.True(WorkflowStateMachine.CanTransition(WorkflowStatus.Validating, WorkflowStatus.Planning));
        Assert.True(WorkflowStateMachine.CanTransition(WorkflowStatus.Validating, WorkflowStatus.FailedSafe));
    }

    [Fact]
    public void Transition_AppliesLegalChange()
    {
        var wf = new AgentWorkflow { Status = WorkflowStatus.Submitted };
        WorkflowStateMachine.Transition(wf, WorkflowStatus.Planning);
        Assert.Equal(WorkflowStatus.Planning, wf.Status);
    }

    [Fact]
    public void Transition_Illegal_ThrowsAndLeavesStatusUnchanged()
    {
        var wf = new AgentWorkflow { Status = WorkflowStatus.Validating };

        var ex = Assert.Throws<InvalidWorkflowTransitionException>(
            () => WorkflowStateMachine.Transition(wf, WorkflowStatus.Approved));

        Assert.Equal(WorkflowStatus.Validating, wf.Status);
        Assert.Equal(WorkflowStatus.Validating, ex.From);
        Assert.Equal(WorkflowStatus.Approved, ex.To);
    }

    [Fact]
    public void EveryStatus_IsCoveredByTheTransitionTable()
    {
        // Guards against adding a new status and forgetting to define where it can go.
        foreach (var status in Enum.GetValues<WorkflowStatus>())
        {
            var exists = Enum.GetValues<WorkflowStatus>().Any(t => WorkflowStateMachine.CanTransition(status, t))
                         || WorkflowStateMachine.IsTerminal(status);
            Assert.True(exists, $"{status} is not defined in the transition table.");
        }
    }
}
