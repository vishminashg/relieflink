using System.Text.Json;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Controllers.Dtos;

public record NeedItemInput(NeedCategory Category, int Quantity, string? Description);

/// <summary>What a citizen sends to create or edit a request. Status, score and urgency are never accepted from the client.</summary>
public record ReliefRequestInput(
    int HouseholdSize,
    int ElderlyCount,
    int ChildrenCount,
    bool HomeFlooded,
    string? District,
    string? AddressText,
    double Latitude,
    double Longitude,
    string? Notes,
    string? PhotoUrl,
    DateTime? NeededBy,
    List<NeedItemInput>? Items);

public record NeedItemDto(Guid Id, string Category, int Quantity, string? Description);

public record RequesterDto(Guid Id, string FullName, string? Phone);

public record ReliefRequestDto(
    Guid Id,
    RequesterDto? Requester,
    int HouseholdSize,
    int ElderlyCount,
    int ChildrenCount,
    bool HomeFlooded,
    string District,
    string AddressText,
    double Latitude,
    double Longitude,
    string? Notes,
    string? PhotoUrl,
    DateTime? NeededBy,
    string Status,
    int TriageScore,
    string Urgency,
    bool UrgencyOverridden,
    string? UrgencyOverrideReason,
    IReadOnlyList<string> TriageReasons,
    string? CancellationReason,
    IReadOnlyList<NeedItemDto> Items,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ReliefRequestDto From(ReliefRequest r) => new(
        r.Id,
        r.Requester is null ? null : new RequesterDto(r.Requester.Id, r.Requester.FullName, r.Requester.Phone),
        r.HouseholdSize, r.ElderlyCount, r.ChildrenCount, r.HomeFlooded,
        r.District, r.AddressText, r.Latitude, r.Longitude,
        r.Notes, r.PhotoUrl, r.NeededBy,
        r.Status.ToString(), r.TriageScore, r.Urgency.ToString(),
        r.UrgencyOverridden, r.UrgencyOverrideReason,
        ParseReasons(r.TriageReasonsJson),
        r.CancellationReason,
        r.Items.OrderBy(i => i.Category).Select(i => new NeedItemDto(i.Id, i.Category.ToString(), i.Quantity, i.Description)).ToList(),
        r.CreatedAt, r.UpdatedAt);

    private static IReadOnlyList<string> ParseReasons(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<string[]>(json) ?? [];
}

public record CancelRequestBody(string? Reason);

/// <summary>Staff triage action. With no Urgency, the score is recomputed and any manual override is cleared.</summary>
public record TriageRequestBody(UrgencyLevel? Urgency, string? Reason);

public record RequestSummaryDto(
    int Total,
    int Open,
    int CriticalOpen,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> ByUrgency,
    IReadOnlyDictionary<string, int> ByDistrict);
