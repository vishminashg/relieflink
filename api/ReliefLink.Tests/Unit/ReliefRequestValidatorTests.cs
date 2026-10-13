using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Domain;
using ReliefLink.Api.Services;

namespace ReliefLink.Tests.Unit;

public class ReliefRequestValidatorTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);

    private static ReliefRequestInput Valid() => new(
        HouseholdSize: 6, ElderlyCount: 2, ChildrenCount: 2, HomeFlooded: true,
        District: "Kalutara", AddressText: "12 River Road, Kalutara",
        Latitude: 6.58, Longitude: 79.96,
        Notes: "Water is rising", PhotoUrl: null, NeededBy: Now.AddHours(20),
        Items: [new NeedItemInput(NeedCategory.Food, 6, null), new NeedItemInput(NeedCategory.Water, 12, "Drinking water")]);

    [Fact]
    public void ValidInput_HasNoErrors()
    {
        Assert.Empty(ReliefRequestValidator.Validate(Valid(), Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(51)]
    public void HouseholdSize_MustBeBetweenOneAndFifty(int size)
    {
        var errors = ReliefRequestValidator.Validate(Valid() with { HouseholdSize = size, ElderlyCount = 0, ChildrenCount = 0 }, Now);

        Assert.Contains("HouseholdSize", errors.Keys);
    }

    [Fact]
    public void ElderlyPlusChildren_CannotExceedHousehold()
    {
        var errors = ReliefRequestValidator.Validate(Valid() with { HouseholdSize = 3, ElderlyCount = 2, ChildrenCount = 2 }, Now);

        Assert.Contains("ElderlyCount", errors.Keys);
    }

    [Fact]
    public void NegativeCounts_AreRejected()
    {
        var errors = ReliefRequestValidator.Validate(Valid() with { ElderlyCount = -1, ChildrenCount = -1 }, Now);

        Assert.Contains("ElderlyCount", errors.Keys);
        Assert.Contains("ChildrenCount", errors.Keys);
    }

    [Theory]
    [InlineData("Atlantis")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownDistrict_IsRejected(string? district)
    {
        Assert.Contains("District", ReliefRequestValidator.Validate(Valid() with { District = district }, Now).Keys);
    }

    [Theory]
    [InlineData("kalutara")]
    [InlineData("  KALUTARA ")]
    [InlineData("Nuwara Eliya")]
    public void DistrictMatching_IgnoresCaseAndSpaces(string district)
    {
        Assert.DoesNotContain("District", ReliefRequestValidator.Validate(Valid() with { District = district }, Now).Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData(null)]
    public void AddressTooShort_IsRejected(string? address)
    {
        Assert.Contains("AddressText", ReliefRequestValidator.Validate(Valid() with { AddressText = address }, Now).Keys);
    }

    [Theory]
    [InlineData(51.5, -0.12)]  // London
    [InlineData(0, 0)]         // the default when coordinates are missing
    [InlineData(6.58, 100.0)]  // longitude out of range
    [InlineData(20.0, 80.0)]   // latitude out of range
    public void CoordinatesOutsideSriLanka_AreRejected(double lat, double lon)
    {
        var errors = ReliefRequestValidator.Validate(Valid() with { Latitude = lat, Longitude = lon }, Now);

        Assert.Contains("Location", errors.Keys);
    }

    [Theory]
    [InlineData("http://example.com/photo.jpg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    public void PhotoUrl_MustBeHttps(string url)
    {
        Assert.Contains("PhotoUrl", ReliefRequestValidator.Validate(Valid() with { PhotoUrl = url }, Now).Keys);
    }

    [Fact]
    public void PhotoUrl_HttpsIsAccepted_AndOptional()
    {
        Assert.Empty(ReliefRequestValidator.Validate(Valid() with { PhotoUrl = "https://example.com/p.jpg" }, Now));
        Assert.Empty(ReliefRequestValidator.Validate(Valid() with { PhotoUrl = null }, Now));
    }

    [Fact]
    public void NeededBy_InThePast_IsRejected()
    {
        Assert.Contains("NeededBy", ReliefRequestValidator.Validate(Valid() with { NeededBy = Now.AddHours(-2) }, Now).Keys);
    }

    [Fact]
    public void NeededBy_MoreThanThirtyDaysAway_IsRejected()
    {
        Assert.Contains("NeededBy", ReliefRequestValidator.Validate(Valid() with { NeededBy = Now.AddDays(31) }, Now).Keys);
    }

    [Fact]
    public void NeededBy_JustNow_IsAllowedBecauseOfSmallClockDifferences()
    {
        Assert.Empty(ReliefRequestValidator.Validate(Valid() with { NeededBy = Now.AddMinutes(-2) }, Now));
    }

    [Fact]
    public void NeededBy_IsOptional()
    {
        Assert.Empty(ReliefRequestValidator.Validate(Valid() with { NeededBy = null }, Now));
    }

    [Fact]
    public void NotesLongerThanThousandCharacters_AreRejected()
    {
        Assert.Contains("Notes", ReliefRequestValidator.Validate(Valid() with { Notes = new string('x', 1001) }, Now).Keys);
    }

    [Fact]
    public void AtLeastOneNeed_IsRequired()
    {
        Assert.Contains("Items", ReliefRequestValidator.Validate(Valid() with { Items = [] }, Now).Keys);
        Assert.Contains("Items", ReliefRequestValidator.Validate(Valid() with { Items = null }, Now).Keys);
    }

    [Fact]
    public void DuplicateNeedCategories_AreRejected()
    {
        var items = new List<NeedItemInput> { new(NeedCategory.Food, 1, null), new(NeedCategory.Food, 2, null) };

        Assert.Contains("Items", ReliefRequestValidator.Validate(Valid() with { Items = items }, Now).Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1001)]
    public void NeedQuantity_MustBeBetweenOneAndThousand(int quantity)
    {
        var items = new List<NeedItemInput> { new(NeedCategory.Food, quantity, null) };

        Assert.Contains("Items", ReliefRequestValidator.Validate(Valid() with { Items = items }, Now).Keys);
    }

    [Fact]
    public void AllErrorsAreReportedTogether_NotJustTheFirst()
    {
        var bad = Valid() with { HouseholdSize = 0, ElderlyCount = 0, ChildrenCount = 0, District = "Nowhere", Items = [] };

        var errors = ReliefRequestValidator.Validate(bad, Now);

        Assert.True(errors.Count >= 3, $"Expected at least 3 fields with errors, got {errors.Count}.");
    }
}
