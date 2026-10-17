using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Domain;
using ReliefLink.Api.Services;

namespace ReliefLink.Tests.Unit;

public class InventoryValidatorTests
{
    [Fact]
    public void ValidateDepot_Succeeds_ForValidInput()
    {
        var input = new DepotInput(
            Code: "DEP-KLU-01",
            Name: "Kalutara Depot",
            District: "Kalutara",
            AddressText: "123 Galle Road, Kalutara",
            Latitude: 6.5854,
            Longitude: 79.9607,
            ContactPerson: "Sunil Silva",
            ContactPhone: "0771234567",
            CapacitySquareMeters: 500,
            Notes: "Good access"
        );

        var errors = InventoryValidator.ValidateDepot(input);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateDepot_Fails_ForInvalidDistrictAndShortName()
    {
        var input = new DepotInput(
            Code: "D",
            Name: "A",
            District: "NonExistentDistrict",
            AddressText: "Short",
            Latitude: 15.0, // outside Sri Lanka
            Longitude: 50.0,
            ContactPerson: "S",
            ContactPhone: "123",
            CapacitySquareMeters: -10,
            Notes: null
        );

        var errors = InventoryValidator.ValidateDepot(input);
        Assert.True(errors.ContainsKey(nameof(input.Code)));
        Assert.True(errors.ContainsKey(nameof(input.Name)));
        Assert.True(errors.ContainsKey(nameof(input.District)));
        Assert.True(errors.ContainsKey(nameof(input.Latitude)));
        Assert.True(errors.ContainsKey(nameof(input.Longitude)));
        Assert.True(errors.ContainsKey(nameof(input.ContactPerson)));
        Assert.True(errors.ContainsKey(nameof(input.ContactPhone)));
        Assert.True(errors.ContainsKey(nameof(input.CapacitySquareMeters)));
    }

    [Fact]
    public void ValidateSupplyItem_Succeeds_ForValidInput()
    {
        var input = new SupplyItemInput(
            Code: "SKU-WTR-5L",
            Name: "Bottled Water (5L)",
            Description: "Clean drinking water",
            Category: NeedCategory.Water,
            UnitOfMeasure: "Bottle",
            WeightKg: 5.0,
            ReorderThreshold: 50,
            StandardFamilyAllocation: 4
        );

        var errors = InventoryValidator.ValidateSupplyItem(input);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSupplyItem_Fails_WhenCategoryMissingOrThresholdNegative()
    {
        var input = new SupplyItemInput(
            Code: "S",
            Name: "",
            Description: null,
            Category: null,
            UnitOfMeasure: "",
            WeightKg: -1.0,
            ReorderThreshold: -5,
            StandardFamilyAllocation: 0
        );

        var errors = InventoryValidator.ValidateSupplyItem(input);
        Assert.True(errors.ContainsKey(nameof(input.Code)));
        Assert.True(errors.ContainsKey(nameof(input.Name)));
        Assert.True(errors.ContainsKey(nameof(input.Category)));
        Assert.True(errors.ContainsKey(nameof(input.UnitOfMeasure)));
        Assert.True(errors.ContainsKey(nameof(input.WeightKg)));
        Assert.True(errors.ContainsKey(nameof(input.ReorderThreshold)));
        Assert.True(errors.ContainsKey(nameof(input.StandardFamilyAllocation)));
    }

    [Fact]
    public void ValidateReceiveStock_ChecksQuantityAndIds()
    {
        var invalid = new ReceiveStockInput(
            DepotId: Guid.Empty,
            SupplyItemId: Guid.Empty,
            Quantity: 0,
            ReferenceNumber: null,
            StorageBin: null,
            Notes: null
        );

        var errors = InventoryValidator.ValidateReceiveStock(invalid);
        Assert.True(errors.ContainsKey(nameof(invalid.DepotId)));
        Assert.True(errors.ContainsKey(nameof(invalid.SupplyItemId)));
        Assert.True(errors.ContainsKey(nameof(invalid.Quantity)));
    }

    [Fact]
    public void ValidateReservation_ChecksHoldHoursAndQuantity()
    {
        var invalid = new CreateReservationInput(
            DepotId: Guid.NewGuid(),
            SupplyItemId: Guid.NewGuid(),
            Quantity: -1,
            ReliefRequestId: null,
            HoldHours: 1000, // max is 720
            Notes: null
        );

        var errors = InventoryValidator.ValidateReservation(invalid);
        Assert.True(errors.ContainsKey(nameof(invalid.Quantity)));
        Assert.True(errors.ContainsKey(nameof(invalid.HoldHours)));
    }
}
