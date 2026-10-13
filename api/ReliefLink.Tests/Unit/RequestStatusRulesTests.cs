using ReliefLink.Api.Domain;

namespace ReliefLink.Tests.Unit;

public class RequestStatusRulesTests
{
    [Fact]
    public void NormalLifecycle_IsAllowedStepByStep()
    {
        RequestStatus[] path =
        [
            RequestStatus.Submitted, RequestStatus.UnderReview, RequestStatus.PlanReady,
            RequestStatus.Approved, RequestStatus.InProgress, RequestStatus.Fulfilled
        ];

        for (var i = 0; i < path.Length - 1; i++)
            Assert.True(RequestStatusRules.CanTransition(path[i], path[i + 1]), $"{path[i]} -> {path[i + 1]}");
    }

    [Theory]
    [InlineData(RequestStatus.Submitted, RequestStatus.Approved)]
    [InlineData(RequestStatus.Submitted, RequestStatus.Fulfilled)]
    [InlineData(RequestStatus.UnderReview, RequestStatus.Approved)]
    [InlineData(RequestStatus.PlanReady, RequestStatus.Fulfilled)]
    [InlineData(RequestStatus.Approved, RequestStatus.Cancelled)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Cancelled)]
    public void StepsCannotBeSkipped_AndStartedWorkCannotBeCancelled(RequestStatus from, RequestStatus to)
    {
        Assert.False(RequestStatusRules.CanTransition(from, to));
    }

    [Theory]
    [InlineData(RequestStatus.Fulfilled)]
    [InlineData(RequestStatus.Rejected)]
    [InlineData(RequestStatus.Cancelled)]
    public void ClosedStatuses_HaveNoExits(RequestStatus closed)
    {
        foreach (var target in Enum.GetValues<RequestStatus>())
            Assert.False(RequestStatusRules.CanTransition(closed, target), $"{closed} -> {target}");
    }

    [Fact]
    public void FailedWorkflow_ReturnsRequestToSubmitted_AndRevisionReturnsToUnderReview()
    {
        Assert.True(RequestStatusRules.CanTransition(RequestStatus.UnderReview, RequestStatus.Submitted));
        Assert.True(RequestStatusRules.CanTransition(RequestStatus.PlanReady, RequestStatus.UnderReview));
    }

    [Theory]
    [InlineData(RequestStatus.Submitted, true)]
    [InlineData(RequestStatus.UnderReview, false)]
    [InlineData(RequestStatus.PlanReady, false)]
    [InlineData(RequestStatus.Approved, false)]
    [InlineData(RequestStatus.Cancelled, false)]
    public void OnlySubmittedRequests_CanBeEdited(RequestStatus status, bool expected)
    {
        Assert.Equal(expected, RequestStatusRules.CanEdit(status));
    }

    [Theory]
    [InlineData(RequestStatus.Submitted, true)]
    [InlineData(RequestStatus.UnderReview, true)]
    [InlineData(RequestStatus.PlanReady, true)]
    [InlineData(RequestStatus.Approved, false)]
    [InlineData(RequestStatus.InProgress, false)]
    [InlineData(RequestStatus.Fulfilled, false)]
    [InlineData(RequestStatus.Rejected, false)]
    [InlineData(RequestStatus.Cancelled, false)]
    public void CancelAndTriage_OnlyWhileWaitingForDecision(RequestStatus status, bool expected)
    {
        Assert.Equal(expected, RequestStatusRules.CanCancel(status));
        Assert.Equal(expected, RequestStatusRules.CanTriage(status));
    }

    [Fact]
    public void Transition_Illegal_ThrowsAndLeavesStatusUnchanged()
    {
        var request = new ReliefRequest { Status = RequestStatus.Submitted };

        var ex = Assert.Throws<InvalidRequestTransitionException>(
            () => RequestStatusRules.Transition(request, RequestStatus.Fulfilled));

        Assert.Equal(RequestStatus.Submitted, request.Status);
        Assert.Equal(RequestStatus.Submitted, ex.From);
        Assert.Equal(RequestStatus.Fulfilled, ex.To);
    }

    [Fact]
    public void Transition_Legal_ChangesStatus()
    {
        var request = new ReliefRequest { Status = RequestStatus.Submitted };

        RequestStatusRules.Transition(request, RequestStatus.UnderReview);

        Assert.Equal(RequestStatus.UnderReview, request.Status);
    }

    [Fact]
    public void EveryStatus_IsDefinedInTheTable()
    {
        foreach (var status in Enum.GetValues<RequestStatus>())
        {
            var hasEntry = Enum.GetValues<RequestStatus>().Any(t => RequestStatusRules.CanTransition(status, t))
                           || status is RequestStatus.Fulfilled or RequestStatus.Rejected or RequestStatus.Cancelled;
            Assert.True(hasEntry, $"{status} has no defined transitions and is not a closed status.");
        }
    }
}
