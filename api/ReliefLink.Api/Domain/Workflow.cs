namespace ReliefLink.Api.Domain;

public enum WorkflowStatus
{
    Submitted,
    Planning,
    Proposed,
    Validating,
    PendingCoordinatorApproval,
    Approved,
    Executed,
    RevisionRequested,
    Rejected,
    FailedSafe
}

public enum StepStatus
{
    Pending,
    Running,
    Succeeded,
    Failed
}

public enum DecisionType
{
    Approved,
    Rejected,
    RevisionRequested
}

/// <summary>
/// One run of the multi-agent relief workflow. ASP.NET Core owns and persists this state;
/// the Python agent service is stateless per call.
/// </summary>
public class AgentWorkflow : BaseEntity
{
    /// <summary>Linked relief request. The foreign key is added by Component A's migration.</summary>
    public Guid? ReliefRequestId { get; set; }

    public string Objective { get; set; } = string.Empty;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Submitted;
    public int RevisionCount { get; set; }
    public int RetryCount { get; set; }

    /// <summary>Structured plan from the Planner agent (JSON).</summary>
    public string? PlanJson { get; set; }

    /// <summary>Final validated proposal shown to the coordinator (JSON).</summary>
    public string? ProposalJson { get; set; }

    public string? FailureReason { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    public List<WorkflowStep> Steps { get; set; } = new();
    public List<ValidationResult> Validations { get; set; } = new();
    public List<ApprovalDecision> Decisions { get; set; } = new();
}

public class WorkflowStep : BaseEntity
{
    public Guid WorkflowId { get; set; }
    public AgentWorkflow? Workflow { get; set; }

    public int Sequence { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public StepStatus Status { get; set; } = StepStatus.Pending;

    /// <summary>Structured input and output only. Hidden reasoning is never stored.</summary>
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }

    public string? Error { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? DurationMs { get; set; }

    public List<ToolCall> ToolCalls { get; set; } = new();
}

public class ToolCall : BaseEntity
{
    public Guid StepId { get; set; }
    public WorkflowStep? Step { get; set; }

    /// <summary>Order in which the agent used its tools (1, 2, 3 ...).</summary>
    public int Sequence { get; set; }

    public string ToolName { get; set; } = string.Empty;
    public string? ArgumentsJson { get; set; }
    public string? ResultJson { get; set; }
    public bool Succeeded { get; set; }
    public int? DurationMs { get; set; }
}

public class ValidationResult : BaseEntity
{
    public Guid WorkflowId { get; set; }
    public AgentWorkflow? Workflow { get; set; }

    /// <summary>Which planning attempt this check belongs to (1 for the first plan, 2 after a retry or revision, ...).</summary>
    public int Attempt { get; set; } = 1;

    public string CheckName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string? Detail { get; set; }
}

public class ApprovalDecision : BaseEntity
{
    public Guid WorkflowId { get; set; }
    public AgentWorkflow? Workflow { get; set; }

    public Guid DecidedByUserId { get; set; }
    public User? DecidedBy { get; set; }

    public DecisionType Decision { get; set; }
    public string? Comment { get; set; }
}

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? DetailsJson { get; set; }
}
