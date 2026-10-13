namespace ReliefLink.Api.Domain;

public static class SriLanka
{
    public static readonly IReadOnlyList<string> Districts =
    [
        "Ampara", "Anuradhapura", "Badulla", "Batticaloa", "Colombo", "Galle", "Gampaha", "Hambantota",
        "Jaffna", "Kalutara", "Kandy", "Kegalle", "Kilinochchi", "Kurunegala", "Mannar", "Matale",
        "Matara", "Monaragala", "Mullaitivu", "Nuwara Eliya", "Polonnaruwa", "Puttalam", "Ratnapura",
        "Trincomalee", "Vavuniya"
    ];

    // A rectangle that contains Sri Lanka. Used to reject coordinates that are obviously wrong.
    public const double MinLatitude = 5.5, MaxLatitude = 10.0;
    public const double MinLongitude = 79.4, MaxLongitude = 82.0;

    /// <summary>Returns the canonical spelling of a district (case-insensitive), or null if unknown.</summary>
    public static string? CanonicalDistrict(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return Districts.FirstOrDefault(d => d.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
