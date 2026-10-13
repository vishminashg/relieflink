using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReliefLink.Api.Auth;
using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Data;
using ReliefLink.Api.Domain;
using ReliefLink.Api.Services;

namespace ReliefLink.Api.Controllers;

/// <summary>
/// Component A: the agentic relief workflow. Coordinators start it, an AI planner proposes a plan,
/// deterministic checks validate it, and a human coordinator approves, rejects or asks for a revision.
/// </summary>
[ApiController]
[Route("api/workflows")]
[Authorize(Roles = Roles.Staff)]
public class WorkflowsController(AppDbContext db, WorkflowOrchestrator orchestrator) : ControllerBase
{
    // ---------- Start ----------
    [HttpPost]
    [ProducesResponseType<WorkflowDetailDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Start([FromBody] StartWorkflowBody? body, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();
        if (body is null || body.ReliefRequestId == Guid.Empty)
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["reliefRequestId"] = ["A relief request id is required."] }));

        var request = await db.ReliefRequests.Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == body.ReliefRequestId, ct);
        if (request is null) return NotFoundProblem("Relief request not found.");

        // One live workflow per request. The database also enforces this with a unique index.
        if (await db.AgentWorkflows.AnyAsync(w => w.ReliefRequestId == request.Id && ActiveStatuses.Contains(w.Status), ct))
            return ConflictProblem("This request already has an active workflow.");

        try
        {
            // The run continues even if the browser disconnects, so a workflow is never left half-finished.
            var workflow = await orchestrator.StartAsync(request, userId.Value);
            var detail = await LoadAsync(workflow.Id, CancellationToken.None);
            return CreatedAtAction(nameof(Get), new { id = workflow.Id }, WorkflowDetailDto.From(detail!));
        }
        catch (WorkflowConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two coordinators clicked at the same moment.
            return ConflictProblem("This request already has an active workflow.");
        }
    }

    // ---------- List ----------
    [HttpGet]
    [ProducesResponseType<PagedResult<WorkflowSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] WorkflowStatus? status,
        [FromQuery] Guid? reliefRequestId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.AgentWorkflows.AsNoTracking().AsQueryable();
        if (status is not null) query = query.Where(w => w.Status == status);
        if (reliefRequestId is not null) query = query.Where(w => w.ReliefRequestId == reliefRequestId);
        query = query.OrderByDescending(w => w.CreatedAt).ThenBy(w => w.Id);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new PagedResult<WorkflowSummaryDto>(items.Select(WorkflowSummaryDto.From).ToList(), page, pageSize, total));
    }

    // ---------- Detail: plan, steps, tool calls, validation results, decisions ----------
    [HttpGet("{id:guid}")]
    [ProducesResponseType<WorkflowDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var workflow = await LoadAsync(id, ct);
        return workflow is null ? NotFoundProblem("Workflow not found.") : Ok(WorkflowDetailDto.From(workflow));
    }

    // ---------- Human decisions ----------
    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, [FromBody] DecisionBody? body) =>
        DecideAsync(id, (w, r, uid) => orchestrator.ApproveAsync(w, r, uid, body?.Comment));

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] DecisionBody? body)
    {
        if (RequireComment(body) is { } problem) return problem;
        return await DecideAsync(id, (w, r, uid) =>
            orchestrator.RejectAsync(w, r, uid, body!.Comment!.Trim(), body.RejectRequest));
    }

    [HttpPost("{id:guid}/revise")]
    public async Task<IActionResult> Revise(Guid id, [FromBody] DecisionBody? body)
    {
        if (RequireComment(body) is { } problem) return problem;
        return await DecideAsync(id, (w, r, uid) => orchestrator.ReviseAsync(w, r, uid, body!.Comment!.Trim()));
    }

    /// <summary>Hands an approved plan to the field. Fails with 409 unless the plan was approved first.</summary>
    [HttpPost("{id:guid}/execute")]
    public Task<IActionResult> Execute(Guid id) =>
        DecideAsync(id, (w, r, uid) => orchestrator.ExecuteAsync(w, r, uid));

    // ---------- Helpers ----------
    private static readonly WorkflowStatus[] ActiveStatuses =
    [
        WorkflowStatus.Submitted, WorkflowStatus.Planning, WorkflowStatus.Proposed, WorkflowStatus.Validating,
        WorkflowStatus.PendingCoordinatorApproval, WorkflowStatus.Approved, WorkflowStatus.RevisionRequested
    ];

    private async Task<IActionResult> DecideAsync(Guid id, Func<AgentWorkflow, ReliefRequest, Guid, Task> action)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var workflow = await db.AgentWorkflows.FirstOrDefaultAsync(w => w.Id == id);
        if (workflow is null) return NotFoundProblem("Workflow not found.");
        var request = workflow.ReliefRequestId is null
            ? null
            : await db.ReliefRequests.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == workflow.ReliefRequestId);
        if (request is null) return ConflictProblem("This workflow is not linked to a relief request.");

        try
        {
            await action(workflow, request, userId.Value);
        }
        catch (WorkflowConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
        catch (Exception ex) when (ex is InvalidWorkflowTransitionException or InvalidRequestTransitionException)
        {
            return ConflictProblem(ex.Message);
        }

        var detail = await LoadAsync(id, CancellationToken.None);
        return Ok(WorkflowDetailDto.From(detail!));
    }

    private IActionResult? RequireComment(DecisionBody? body)
    {
        var comment = body?.Comment?.Trim();
        if (string.IsNullOrEmpty(comment) || comment.Length is < 3 or > 1000)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["comment"] = ["A comment of 3 to 1000 characters is required."]
            }));
        return null;
    }

    private Task<AgentWorkflow?> LoadAsync(Guid id, CancellationToken ct) =>
        db.AgentWorkflows.AsNoTracking().AsSplitQuery()
            .Include(w => w.Steps).ThenInclude(s => s.ToolCalls)
            .Include(w => w.Validations)
            .Include(w => w.Decisions).ThenInclude(d => d.DecidedBy)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

    private ObjectResult NotFoundProblem(string title) => Problem(statusCode: StatusCodes.Status404NotFound, title: title);

    private ObjectResult ConflictProblem(string title) => Problem(statusCode: StatusCodes.Status409Conflict, title: title);
}
