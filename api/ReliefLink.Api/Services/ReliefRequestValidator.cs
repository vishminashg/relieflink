using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Services;

/// <summary>Validates a relief request in one place so every rule is easy to find and to unit test.</summary>
public static class ReliefRequestValidator
{
    public const int MaxHouseholdSize = 50;
    public const int MaxItems = 6;
    public const int MaxQuantity = 1000;

    /// <summary>Returns field name -> error messages. An empty dictionary means the input is valid.</summary>
    public static Dictionary<string, string[]> Validate(ReliefRequestInput input, DateTime now)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = new List<string>();
            list.Add(message);
        }

        if (input.HouseholdSize is < 1 or > MaxHouseholdSize)
            Add(nameof(input.HouseholdSize), $"Household size must be between 1 and {MaxHouseholdSize}.");

        if (input.ElderlyCount < 0) Add(nameof(input.ElderlyCount), "Elderly count cannot be negative.");
        if (input.ChildrenCount < 0) Add(nameof(input.ChildrenCount), "Children count cannot be negative.");
        if (input.ElderlyCount >= 0 && input.ChildrenCount >= 0 &&
            input.ElderlyCount + input.ChildrenCount > input.HouseholdSize)
            Add(nameof(input.ElderlyCount), "Elderly plus children cannot be more than the household size.");

        if (SriLanka.CanonicalDistrict(input.District) is null)
            Add(nameof(input.District), "Choose a valid Sri Lankan district.");

        var address = input.AddressText?.Trim() ?? string.Empty;
        if (address.Length < 5 || address.Length > 300)
            Add(nameof(input.AddressText), "Address must be between 5 and 300 characters.");

        if (input.Latitude is < SriLanka.MinLatitude or > SriLanka.MaxLatitude ||
            input.Longitude is < SriLanka.MinLongitude or > SriLanka.MaxLongitude)
            Add("Location", "Coordinates must be inside Sri Lanka.");

        if (input.Notes is { Length: > 1000 })
            Add(nameof(input.Notes), "Notes cannot be longer than 1000 characters.");

        if (!string.IsNullOrWhiteSpace(input.PhotoUrl))
        {
            var ok = input.PhotoUrl.Length <= 500 &&
                     Uri.TryCreate(input.PhotoUrl, UriKind.Absolute, out var uri) &&
                     uri.Scheme == Uri.UriSchemeHttps;
            if (!ok) Add(nameof(input.PhotoUrl), "Photo must be an https link of at most 500 characters.");
        }

        if (input.NeededBy is { } neededBy)
        {
            if (neededBy < now.AddMinutes(-5))
                Add(nameof(input.NeededBy), "The needed-by time cannot be in the past.");
            else if (neededBy > now.AddDays(30))
                Add(nameof(input.NeededBy), "The needed-by time must be within 30 days.");
        }

        var items = input.Items ?? [];
        if (items.Count == 0)
            Add(nameof(input.Items), "Add at least one need.");
        else if (items.Count > MaxItems)
            Add(nameof(input.Items), $"No more than {MaxItems} needs can be added.");
        else
        {
            if (items.Select(i => i.Category).Distinct().Count() != items.Count)
                Add(nameof(input.Items), "Each need category can only appear once.");
            if (items.Any(i => i.Quantity is < 1 or > MaxQuantity))
                Add(nameof(input.Items), $"Each quantity must be between 1 and {MaxQuantity}.");
            if (items.Any(i => i.Description is { Length: > 300 }))
                Add(nameof(input.Items), "A need description cannot be longer than 300 characters.");
        }

        return errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }
}
