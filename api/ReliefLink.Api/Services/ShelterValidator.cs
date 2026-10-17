using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Services;

public static class ShelterValidator
{
    public static Dictionary<string, string[]> Validate(ShelterInput input)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
            list.Add(message);
        }

        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 160)
            Add(nameof(input.Name), "Name is required and must be at most 160 characters.");
        if (SriLanka.CanonicalDistrict(input.District) is null)
            Add(nameof(input.District), "Choose a valid Sri Lankan district.");
        if (string.IsNullOrWhiteSpace(input.Address) || input.Address.Trim().Length > 300)
            Add(nameof(input.Address), "Address is required and must be at most 300 characters.");
        if (input.Capacity is < 1 or > 100000)
            Add(nameof(input.Capacity), "Capacity must be between 1 and 100000.");
        if (input.Latitude is < SriLanka.MinLatitude or > SriLanka.MaxLatitude)
            Add(nameof(input.Latitude), "Latitude must be within Sri Lanka.");
        if (input.Longitude is < SriLanka.MinLongitude or > SriLanka.MaxLongitude)
            Add(nameof(input.Longitude), "Longitude must be within Sri Lanka.");
        if (input.Notes is { Length: > 1000 })
            Add(nameof(input.Notes), "Notes cannot be longer than 1000 characters.");

        return errors.ToDictionary(x => x.Key, x => x.Value.ToArray());
    }

    public static Dictionary<string, string[]> ValidateCheckIn(ShelterCheckInInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.HouseholdSize is < 1 or > 1000)
            errors[nameof(input.HouseholdSize)] = ["Household size must be between 1 and 1000."];
        return errors;
    }
}
