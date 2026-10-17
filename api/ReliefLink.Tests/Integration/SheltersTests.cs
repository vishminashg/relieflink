using System.Net;
using System.Net.Http.Json;
using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Tests.Infrastructure;

namespace ReliefLink.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class SheltersTests(ApiFactory factory)
{
    private static ShelterInput Input(int capacity = 5) => new(
        "Kalutara Community Shelter", "Kalutara", "Main Road, Kalutara",
        6.5854, 79.9607, capacity, true, "Open during flood response.");

    [Fact]
    public async Task Citizen_cannot_manage_shelters_but_can_find_nearest_active_shelters()
    {
        var coordinator = await factory.CoordinatorAsync();
        var create = await coordinator.PostAsJsonAsync("/api/shelters", Input());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var citizen = await factory.CitizenAsync();
        var forbidden = await citizen.PostAsJsonAsync("/api/shelters", Input());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var nearest = await citizen.GetAsync("/api/shelters/nearest?latitude=6.58&longitude=79.96");
        Assert.Equal(HttpStatusCode.OK, nearest.StatusCode);
    }

    [Fact]
    public async Task Check_in_rejects_household_that_would_exceed_capacity()
    {
        var coordinator = await factory.CoordinatorAsync();
        var create = await coordinator.PostAsJsonAsync("/api/shelters", Input(5));
        var shelter = (await create.Content.ReadFromJsonAsync<ShelterDto>())!;

        var first = await coordinator.PostAsJsonAsync($"/api/shelters/{shelter.Id}/check-in", new ShelterCheckInInput(4));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await coordinator.PostAsJsonAsync($"/api/shelters/{shelter.Id}/check-in", new ShelterCheckInInput(2));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Check_out_releases_capacity()
    {
        var coordinator = await factory.CoordinatorAsync();
        var create = await coordinator.PostAsJsonAsync("/api/shelters", Input(5));
        var shelter = (await create.Content.ReadFromJsonAsync<ShelterDto>())!;

        var checkIn = await coordinator.PostAsJsonAsync($"/api/shelters/{shelter.Id}/check-in", new ShelterCheckInInput(5));
        var record = (await checkIn.Content.ReadFromJsonAsync<ShelterCheckInDto>())!;
        var checkOut = await coordinator.PostAsync($"/api/shelters/{shelter.Id}/check-out/{record.Id}", null);
        Assert.Equal(HttpStatusCode.OK, checkOut.StatusCode);

        var newCheckIn = await coordinator.PostAsJsonAsync($"/api/shelters/{shelter.Id}/check-in", new ShelterCheckInInput(5));
        Assert.Equal(HttpStatusCode.Created, newCheckIn.StatusCode);
    }
}
