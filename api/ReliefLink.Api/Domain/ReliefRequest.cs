namespace ReliefLink.Api.Domain;

public enum RequestStatus
{
    Submitted,
    UnderReview,
    PlanReady,
    Approved,
    InProgress,
    Fulfilled,
    Rejected,
    Cancelled
}

public enum UrgencyLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum NeedCategory
{
    Food,
    Water,
    Shelter,
    Medical,
    Clothing,
    Sanitation
}

/// <summary>A family's request for relief. Owned by Component A (Relief Request Management).</summary>
public class ReliefRequest : BaseEntity
{
    public Guid RequesterId { get; set; }
    public User? Requester { get; set; }

    public int HouseholdSize { get; set; }
    public int ElderlyCount { get; set; }
    public int ChildrenCount { get; set; }
    public bool HomeFlooded { get; set; }

    public string District { get; set; } = string.Empty;
    public string AddressText { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string? Notes { get; set; }
    public string? PhotoUrl { get; set; }

    /// <summary>When the family needs help by. Used by triage.</summary>
    public DateTime? NeededBy { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.Submitted;

    /// <summary>0-100, computed by the deterministic triage scorer, never trusted from the client.</summary>
    public int TriageScore { get; set; }
    public UrgencyLevel Urgency { get; set; } = UrgencyLevel.Low;

    /// <summary>True when a coordinator manually set the urgency. Edits then keep the manual value.</summary>
    public bool UrgencyOverridden { get; set; }
    public string? UrgencyOverrideReason { get; set; }

    /// <summary>Plain-language reasons behind the score, shown to coordinators and used as agent evidence.</summary>
    public string? TriageReasonsJson { get; set; }

    public string? CancellationReason { get; set; }

    public List<ReliefRequestItem> Items { get; set; } = new();
}

public class ReliefRequestItem : BaseEntity
{
    public Guid ReliefRequestId { get; set; }
    public ReliefRequest? ReliefRequest { get; set; }

    public NeedCategory Category { get; set; }
    public int Quantity { get; set; }
    public string? Description { get; set; }
}
