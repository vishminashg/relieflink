using System.Text.Json;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Controllers.Dtos;

public record StartWorkflowBody(Guid ReliefRequestId);

/// <summary>Comment is optional when approving, required when rejecting or asking for a revision.</summary>
public record DecisionBody(string? Comment, bool RejectRequest = false);

public record WorkflowSummaryDto(
    Guid Id,
    Guid? ReliefRequestId,
    string Objective,
    string Status,
    int RevisionCount,
    int RetryCount,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static WorkflowSummaryDto From(AgentWorkflow w) => new(
        w.Id, w.ReliefRequestId, w.Objective, w.Status.ToString(), w.RevisionCount, w.RetryCount,
        w.FailureReason, w.CreatedAt, w.UpdatedAt);
}

public record ToolCallDto(string ToolName, JsonElement? Arguments, JsonElement? Result, bool Succeeded, int? DurationMs);

public record WorkflowStepDto(
    int Sequence,
    string AgentName,
    string Goal,
    string Status,
    JsonElement? Input,
    JsonElement? Output,
    string? Error,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int? DurationMs,
    IReadOnlyList<ToolCallDto> ToolCalls);

public record ValidationDto(int Attempt, string CheckName, bool Passed, string? Detail);

public record DecisionDto(Guid DecidedByUserId, string? DecidedByName, string Decision, string? Comment, DateTime CreatedAt);

public record WorkflowDetailDto(
    WorkflowSummaryDto Workflow,
    JsonElement? Plan,
    JsonElement? Proposal,
    IReadOnlyList<WorkflowStepDto> Steps,
    IReadOnlyList<ValidationDto> Validations,
    IReadOnlyList<DecisionDto> Decisions)
{
    public static WorkflowDetailDto From(AgentWorkflow w) => new(
        WorkflowSummaryDto.From(w),
        Parse(w.PlanJson),
        Parse(w.ProposalJson),
        w.Steps.OrderBy(s => s.Sequence).Select(s => new WorkflowStepDto(
            s.Sequence, s.AgentName, s.Goal, s.Status.ToString(), Parse(s.InputJson), Parse(s.OutputJson),
            s.Error, s.StartedAt, s.CompletedAt, s.DurationMs,
            s.ToolCalls.OrderBy(t => t.Sequence)
                .Select(t => new ToolCallDto(t.ToolName, Parse(t.ArgumentsJson), Parse(t.ResultJson), t.Succeeded, t.DurationMs))
                .ToList())).ToList(),
        w.Validations.OrderBy(v => v.Attempt).ThenBy(v => v.CreatedAt).ThenBy(v => v.CheckName)
            .Select(v => new ValidationDto(v.Attempt, v.CheckName, v.Passed, v.Detail)).ToList(),
        w.Decisions.OrderBy(d => d.CreatedAt)
            .Select(d => new DecisionDto(d.DecidedByUserId, d.DecidedBy?.FullName, d.Decision.ToString(), d.Comment, d.CreatedAt)).ToList());

    private static JsonElement? Parse(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonDocument.Parse(json).RootElement.Clone();
}
