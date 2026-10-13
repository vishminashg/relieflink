using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Data;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Services;

/// <summary>The action is not allowed in the workflow's or the request's current state (maps to HTTP 409).</summary>
public class WorkflowConflictException(string message) : Exception(message);

/// <summary>
/// Runs the relief workflow. ASP.NET Core owns every status change here; the agent only returns a proposal.
/// Nothing the agent says can move the workflow past the coordinator approval gate.
///
///   start  -> Planning -> Proposed -> Validating -> PendingCoordinatorApproval
///   retry  -> Validating (checks failed) or Planning (agent unreachable) goes round again, at most MaxRetries times
///   fail   -> FailedSafe, and the request returns to Submitted so a new workflow can be started
///   human  -> approve / reject / revise, then execute after approval
/// </summary>
public class WorkflowOrchestrator(
    AppDbContext db,
    IPlannerClient planner,
    IAuditService audit,
    TimeProvider clock,
    ILogger<WorkflowOrchestrator> log)
{
    public const string PlannerAgent = "Planner";
    private const int MaxToolCalls = 20;
    private const int MaxStoredJsonLength = 4000;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------- Start ----------
    /// <summary>Creates the workflow for a Submitted request and runs the first planning cycle.</summary>
    public async Task<AgentWorkflow> StartAsync(ReliefRequest request, Guid userId)
    {
        if (request.Status != RequestStatus.Submitted)
            throw new WorkflowConflictException(
                $"A workflow can only be started for a Submitted request. This one is {request.Status}.");

        RequestStatusRules.Transition(request, RequestStatus.UnderReview);

        var workflow = new AgentWorkflow
        {
            ReliefRequestId = request.Id,
            Objective = Describe(request),
            Status = WorkflowStatus.Submitted,
            CreatedByUserId = userId
        };
        db.AgentWorkflows.Add(workflow);
        audit.Record(userId, "workflow.started", nameof(AgentWorkflow), workflow.Id.ToString(),
            new { ReliefRequestId = request.Id });
        await db.SaveChangesAsync();

        await PlanAsync(workflow, request, coordinatorComment: null);
        return workflow;
    }

    // ---------- Planning cycle (first run, and after a revision request) ----------
    private async Task PlanAsync(AgentWorkflow workflow, ReliefRequest request, string? coordinatorComment)
    {
        workflow.RetryCount = 0;
        WorkflowStateMachine.Transition(workflow, WorkflowStatus.Planning);
        await db.SaveChangesAsync();

        List<string> failures = [];

        while (true)
        {
            var attempt = await db.WorkflowSteps.CountAsync(s => s.WorkflowId == workflow.Id) + 1;
            var feedback = failures.Count > 0 || coordinatorComment is not null
                ? new PlannerFeedback(failures, coordinatorComment)
                : null;
            var input = BuildInput(request, attempt, feedback);

            var step = new WorkflowStep
            {
                WorkflowId = workflow.Id,
                Sequence = attempt,
                AgentName = PlannerAgent,
                Goal = "Propose a relief plan that covers every requested need.",
                Status = StepStatus.Running,
                StartedAt = Now,
                InputJson = JsonSerializer.Serialize(input, HttpPlannerClient.Json)
            };
            db.WorkflowSteps.Add(step);
            await db.SaveChangesAsync();

            var timer = Stopwatch.StartNew();
            PlannerResponse? response = null;
            string? error = null;
            try
            {
                response = await planner.PlanAsync(input, CancellationToken.None);
            }
            catch (PlannerUnavailableException ex)
            {
                error = ex.Message;
                log.LogWarning(ex, "Planner call failed for workflow {WorkflowId} (attempt {Attempt})", workflow.Id, attempt);
            }
            timer.Stop();
            step.CompletedAt = Now;
            step.DurationMs = (int)timer.ElapsedMilliseconds;

            // ----- The agent could not be used: retry, or fail safe -----
            if (error is not null)
            {
                step.Status = StepStatus.Failed;
                step.Error = Truncate(error, 2000);
                await db.SaveChangesAsync();

                if (workflow.RetryCount >= WorkflowStateMachine.MaxRetries)
                {
                    await FailSafeAsync(workflow, request, $"The planner could not be used: {error}");
                    return;
                }
                workflow.RetryCount++;
                failures = [$"The previous attempt failed: {error}"];
                continue;
            }

            // ----- We have a proposal: store it and validate it -----
            step.Status = StepStatus.Succeeded;
            step.OutputJson = response!.Plan is null ? null : JsonSerializer.Serialize(response.Plan, HttpPlannerClient.Json);
            StoreToolCalls(step, response.ToolCalls);

            workflow.PlanJson = step.OutputJson;
            WorkflowStateMachine.Transition(workflow, WorkflowStatus.Proposed);
            await db.SaveChangesAsync();

            WorkflowStateMachine.Transition(workflow, WorkflowStatus.Validating);
            var checks = PlanValidator.Validate(response.Plan, new PlanValidationContext(
                request.Urgency, request.Items.Select(i => new PlannerNeed(i.Category, i.Quantity, i.Description)).ToList()));
            foreach (var check in checks)
                db.ValidationResults.Add(new ValidationResult
                {
                    WorkflowId = workflow.Id,
                    Attempt = attempt,
                    CheckName = check.Name,
                    Passed = check.Passed,
                    Detail = Truncate(check.Detail, 2000)
                });

            if (PlanValidator.AllPassed(checks))
            {
                workflow.ProposalJson = step.OutputJson;
                WorkflowStateMachine.Transition(workflow, WorkflowStatus.PendingCoordinatorApproval);
                RequestStatusRules.Transition(request, RequestStatus.PlanReady);
                audit.Record(null, "workflow.proposal_ready", nameof(AgentWorkflow), workflow.Id.ToString(),
                    new { Attempt = attempt });
                await db.SaveChangesAsync();
                return;
            }

            failures = checks.Where(c => !c.Passed).Select(c => $"{c.Name}: {c.Detail}").ToList();
            if (workflow.RetryCount >= WorkflowStateMachine.MaxRetries)
            {
                await db.SaveChangesAsync(); // keep the failed validation rows as evidence
                await FailSafeAsync(workflow, request, "The plan failed validation: " + string.Join(" | ", failures));
                return;
            }

            workflow.RetryCount++;
            WorkflowStateMachine.Transition(workflow, WorkflowStatus.Planning);
            await db.SaveChangesAsync();
        }
    }

    // ---------- Human decisions ----------
    public async Task ApproveAsync(AgentWorkflow workflow, ReliefRequest request, Guid userId, string? comment)
    {
        EnsureAwaitingDecision(workflow, request);

        WorkflowStateMachine.Transition(workflow, WorkflowStatus.Approved);
        RequestStatusRules.Transition(request, RequestStatus.Approved);
        AddDecision(workflow, userId, DecisionType.Approved, comment);
        audit.Record(userId, "workflow.approved", nameof(AgentWorkflow), workflow.Id.ToString());
        await db.SaveChangesAsync();
    }

    public async Task RejectAsync(AgentWorkflow workflow, ReliefRequest request, Guid userId, string comment, bool rejectRequest)
    {
        if (workflow.Status != WorkflowStatus.PendingCoordinatorApproval)
            throw new WorkflowConflictException($"Only a plan waiting for approval can be rejected. This workflow is {workflow.Status}.");

        WorkflowStateMachine.Transition(workflow, WorkflowStatus.Rejected);

        // If the citizen already cancelled the request, there is nothing to move: just close the workflow.
        if (request.Status == RequestStatus.PlanReady)
        {
            if (rejectRequest)
                RequestStatusRules.Transition(request, RequestStatus.Rejected);
            else
            {
                // Plan declined, request still valid: send it back so a new workflow can be started.
                RequestStatusRules.Transition(request, RequestStatus.UnderReview);
                RequestStatusRules.Transition(request, RequestStatus.Submitted);
            }
        }

        AddDecision(workflow, userId, DecisionType.Rejected, comment);
        audit.Record(userId, "workflow.rejected", nameof(AgentWorkflow), workflow.Id.ToString(), new { rejectRequest });
        await db.SaveChangesAsync();
    }

    public async Task ReviseAsync(AgentWorkflow workflow, ReliefRequest request, Guid userId, string comment)
    {
        EnsureAwaitingDecision(workflow, request);
        if (workflow.RevisionCount >= WorkflowStateMachine.MaxRevisions)
            throw new WorkflowConflictException(
                $"The maximum of {WorkflowStateMachine.MaxRevisions} revisions has been reached. Approve or reject the plan.");

        WorkflowStateMachine.Transition(workflow, WorkflowStatus.RevisionRequested);
        RequestStatusRules.Transition(request, RequestStatus.UnderReview);
        workflow.RevisionCount++;
        workflow.ProposalJson = null; // the old proposal is no longer the current one
        AddDecision(workflow, userId, DecisionType.RevisionRequested, comment);
        audit.Record(userId, "workflow.revision_requested", nameof(AgentWorkflow), workflow.Id.ToString());
        await db.SaveChangesAsync();

        await PlanAsync(workflow, request, comment);
    }

    /// <summary>Hands an approved plan to the field. Only possible after approval.</summary>
    public async Task ExecuteAsync(AgentWorkflow workflow, ReliefRequest request, Guid userId)
    {
        if (workflow.Status != WorkflowStatus.Approved)
            throw new WorkflowConflictException($"Only an approved plan can be executed. This workflow is {workflow.Status}.");
        if (request.Status != RequestStatus.Approved)
            throw new WorkflowConflictException($"The request is {request.Status}, not Approved.");

        WorkflowStateMachine.Transition(workflow, WorkflowStatus.Executed);
        RequestStatusRules.Transition(request, RequestStatus.InProgress);
        audit.Record(userId, "workflow.executed", nameof(AgentWorkflow), workflow.Id.ToString());
        await db.SaveChangesAsync();
    }

    // ---------- Helpers ----------
    private async Task FailSafeAsync(AgentWorkflow workflow, ReliefRequest request, string reason)
    {
        workflow.FailureReason = Truncate(reason, 2000);
        WorkflowStateMachine.Transition(workflow, WorkflowStatus.FailedSafe);

        // Put the request back where a coordinator can start again.
        if (request.Status == RequestStatus.UnderReview)
            RequestStatusRules.Transition(request, RequestStatus.Submitted);

        audit.Record(null, "workflow.failed_safe", nameof(AgentWorkflow), workflow.Id.ToString(),
            new { Reason = workflow.FailureReason });
        await db.SaveChangesAsync();
    }

    private void EnsureAwaitingDecision(AgentWorkflow workflow, ReliefRequest request)
    {
        if (workflow.Status != WorkflowStatus.PendingCoordinatorApproval)
            throw new WorkflowConflictException(
                $"The workflow is {workflow.Status}; only a plan waiting for approval can be decided.");
        if (request.Status != RequestStatus.PlanReady)
            throw new WorkflowConflictException(
                $"The request is {request.Status} and is no longer waiting for a decision. Reject this plan to close it.");
    }

    private void AddDecision(AgentWorkflow workflow, Guid userId, DecisionType decision, string? comment) =>
        // Added explicitly (not through workflow.Decisions) because the new row already has an Id.
        db.ApprovalDecisions.Add(new ApprovalDecision
        {
            WorkflowId = workflow.Id,
            DecidedByUserId = userId,
            Decision = decision,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()
        });

    private void StoreToolCalls(WorkflowStep step, List<PlannerToolCall>? calls)
    {
        // The list comes from another service: bound how many we keep and how large each one is.
        var sequence = 0;
        foreach (var call in (calls ?? []).Take(MaxToolCalls))
            db.ToolCalls.Add(new ToolCall
            {
                StepId = step.Id,
                Sequence = ++sequence,
                ToolName = Truncate(string.IsNullOrWhiteSpace(call.Name) ? "(unnamed)" : call.Name.Trim(), 80),
                ArgumentsJson = BoundedJson(call.Arguments),
                ResultJson = BoundedJson(call.Result),
                Succeeded = call.Succeeded,
                DurationMs = call.DurationMs
            });
    }

    private static string? BoundedJson(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        var raw = element.Value.GetRawText();
        return raw.Length <= MaxStoredJsonLength ? raw : "{\"truncated\":true}";
    }

    private static PlannerInput BuildInput(ReliefRequest r, int attempt, PlannerFeedback? feedback)
    {
        var reasons = string.IsNullOrWhiteSpace(r.TriageReasonsJson)
            ? []
            : JsonSerializer.Deserialize<string[]>(r.TriageReasonsJson) ?? [];

        return new PlannerInput(
            r.Id, r.District, r.HouseholdSize, r.ElderlyCount, r.ChildrenCount, r.HomeFlooded, r.NeededBy,
            r.TriageScore, r.Urgency.ToString(), reasons,
            r.Items.OrderBy(i => i.Category).Select(i => new PlannerNeed(i.Category, i.Quantity, i.Description)).ToList(),
            r.Notes, attempt, feedback);
    }

    private static string Describe(ReliefRequest r) =>
        $"Plan relief in {r.District} for a household of {r.HouseholdSize} " +
        $"({string.Join(", ", r.Items.OrderBy(i => i.Category).Select(i => i.Category))}), urgency {r.Urgency}.";

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
