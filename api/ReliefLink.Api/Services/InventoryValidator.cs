using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Services;

/// <summary>
/// Centralized validator for Component B: Supply and Inventory Management.
/// </summary>
public static class InventoryValidator
{
    public static Dictionary<string, string[]> ValidateDepot(DepotInput input)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = new List<string>();
            list.Add(message);
        }

        var code = input.Code?.Trim() ?? string.Empty;
        if (code.Length is < 2 or > 20)
            Add(nameof(input.Code), "Depot code must be between 2 and 20 characters.");

        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 120)
            Add(nameof(input.Name), "Depot name must be between 2 and 120 characters.");

        if (SriLanka.CanonicalDistrict(input.District) is null)
            Add(nameof(input.District), "Choose a valid Sri Lankan district.");

        var address = input.AddressText?.Trim() ?? string.Empty;
        if (address.Length is < 5 or > 250)
            Add(nameof(input.AddressText), "Address must be between 5 and 250 characters.");

        if (input.Latitude is { } lat && (lat < SriLanka.MinLatitude || lat > SriLanka.MaxLatitude))
            Add(nameof(input.Latitude), $"Latitude must be within Sri Lanka boundaries ({SriLanka.MinLatitude} - {SriLanka.MaxLatitude}).");

        if (input.Longitude is { } lng && (lng < SriLanka.MinLongitude || lng > SriLanka.MaxLongitude))
            Add(nameof(input.Longitude), $"Longitude must be within Sri Lanka boundaries ({SriLanka.MinLongitude} - {SriLanka.MaxLongitude}).");

        var contactPerson = input.ContactPerson?.Trim() ?? string.Empty;
        if (contactPerson.Length is < 2 or > 100)
            Add(nameof(input.ContactPerson), "Contact person name must be between 2 and 100 characters.");

        var contactPhone = input.ContactPhone?.Trim() ?? string.Empty;
        if (contactPhone.Length is < 7 or > 30)
            Add(nameof(input.ContactPhone), "Contact phone number must be between 7 and 30 characters.");

        if (input.CapacitySquareMeters is { } cap && cap < 0)
            Add(nameof(input.CapacitySquareMeters), "Capacity in square meters cannot be negative.");

        return errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
    }

    public static Dictionary<string, string[]> ValidateSupplyItem(SupplyItemInput input)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = new List<string>();
            list.Add(message);
        }

        var code = input.Code?.Trim() ?? string.Empty;
        if (code.Length is < 2 or > 30)
            Add(nameof(input.Code), "Item code must be between 2 and 30 characters.");

        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 150)
            Add(nameof(input.Name), "Item name must be between 2 and 150 characters.");

        if (input.Category is null)
            Add(nameof(input.Category), "A valid need category must be specified.");

        var uom = input.UnitOfMeasure?.Trim() ?? string.Empty;
        if (uom.Length is < 1 or > 30)
            Add(nameof(input.UnitOfMeasure), "Unit of measure must be between 1 and 30 characters.");

        if (input.WeightKg is { } w && w < 0)
            Add(nameof(input.WeightKg), "Weight cannot be negative.");

        if (input.ReorderThreshold is { } rt && rt < 0)
            Add(nameof(input.ReorderThreshold), "Reorder threshold cannot be negative.");

        if (input.StandardFamilyAllocation is { } sfa && sfa < 1)
            Add(nameof(input.StandardFamilyAllocation), "Standard family allocation must be at least 1.");

        return errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
    }

    public static Dictionary<string, string[]> ValidateReceiveStock(ReceiveStockInput input)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = new List<string>();
            list.Add(message);
        }

        if (input.DepotId == Guid.Empty)
            Add(nameof(input.DepotId), "A valid Depot ID is required.");

        if (input.SupplyItemId == Guid.Empty)
            Add(nameof(input.SupplyItemId), "A valid Supply Item ID is required.");

        if (input.Quantity <= 0)
            Add(nameof(input.Quantity), "Quantity received must be greater than zero.");

        return errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
    }

    public static Dictionary<string, string[]> ValidateReservation(CreateReservationInput input)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = new List<string>();
            list.Add(message);
        }

        if (input.DepotId == Guid.Empty)
            Add(nameof(input.DepotId), "A valid Depot ID is required.");

        if (input.SupplyItemId == Guid.Empty)
            Add(nameof(input.SupplyItemId), "A valid Supply Item ID is required.");

        if (input.Quantity <= 0)
            Add(nameof(input.Quantity), "Reservation quantity must be greater than zero.");

        if (input.HoldHours is { } h && h is < 1 or > 720)
            Add(nameof(input.HoldHours), "Hold duration must be between 1 and 720 hours (30 days).");

        return errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
    }
}
