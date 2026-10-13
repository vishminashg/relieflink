using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Domain;
using ReliefLink.Tests.Infrastructure;

namespace ReliefLink.Tests.Integration;

[Collection(ApiCollection.Name)]
public class ReliefRequestsTests(ApiFactory factory)
{
    // All tests share one database, so every test tags its data with a unique token and filters by it.
    private static string NewToken() => Guid.NewGuid().ToString("N");

    private static object GoldenPayload(string token, string district = "Kalutara") => new
    {
        householdSize = 6,
        elderlyCount = 2,
        childrenCount = 2,
        homeFlooded = true,
        district,
        addressText = $"12 River Road {token}",
        latitude = 6.58,
        longitude = 79.96,
        notes = "Water is rising",
        neededBy = DateTime.UtcNow.AddHours(20),
        items = new object[]
        {
            new { category = "Food", quantity = 6 },
            new { category = "Water", quantity = 12 },
            new { category = "Shelter", quantity = 1 }
        }
    };

    private static object LowPayload(string token, string district = "Jaffna") => new
    {
        householdSize = 1,
        elderlyCount = 0,
        childrenCount = 0,
        homeFlooded = false,
        district,
        addressText = $"3 Temple Lane {token}",
        latitude = 9.66,
        longitude = 80.01,
        items = new object[] { new { category = "Clothing", quantity = 1 } }
    };

    private async Task<(HttpClient Client, Guid UserId)> NewCitizenAsync()
    {
        var client = factory.CreateClient();
        var email = $"citizen_{NewToken()}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Another Citizen",
            email,
            password = "Passw0rdOK",
            role = "Citizen"
        });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<ApiFactory.AuthPayload>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth.User.Id);
    }

    private static async Task<ReliefRequestDto> CreateAsync(HttpClient client, object payload)
    {
        var response = await client.PostAsJsonAsync("/api/relief-requests", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReliefRequestDto>())!;
    }

    private async Task SetStatusInDbAsync(Guid id, RequestStatus status) =>
        await factory.WithDbAsync(async db =>
        {
            var r = await db.ReliefRequests.SingleAsync(x => x.Id == id);
            r.Status = status;
            return await db.SaveChangesAsync();
        });

    // ---------- Create ----------

    [Fact]
    public async Task Create_AsCitizen_Returns201_WithTriageScore_AndSubmittedStatus()
    {
        var citizen = await factory.CitizenAsync();

        var response = await citizen.PostAsJsonAsync("/api/relief-requests", GoldenPayload(NewToken()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var dto = (await response.Content.ReadFromJsonAsync<ReliefRequestDto>())!;
        Assert.Equal("Submitted", dto.Status);
        Assert.Equal(71, dto.TriageScore);       // 9 + 18 + 10 + 24 + 10
        Assert.Equal("Critical", dto.Urgency);
        Assert.False(dto.UrgencyOverridden);
        Assert.Equal(3, dto.Items.Count);
        Assert.Equal("Kalutara", dto.District);
        Assert.Equal("Saman Citizen", dto.Requester!.FullName);
        Assert.NotEmpty(dto.TriageReasons);
    }

    [Fact]
    public async Task Create_ClientCannotSetStatusScoreUrgencyOrRequester()
    {
        var citizen = await factory.CitizenAsync();
        var token = NewToken();
        var payload = new
        {
            householdSize = 1, elderlyCount = 0, childrenCount = 0, homeFlooded = false,
            district = "Jaffna", addressText = $"3 Temple Lane {token}", latitude = 9.66, longitude = 80.01,
            items = new object[] { new { category = "Clothing", quantity = 1 } },
            // None of the following must be trusted:
            status = "Approved", triageScore = 100, urgency = "Critical", requesterId = Guid.NewGuid(),
            urgencyOverridden = true
        };

        var dto = await CreateAsync(citizen, payload);

        Assert.Equal("Submitted", dto.Status);
        Assert.Equal(3, dto.TriageScore);   // 1 household + 2 clothing
        Assert.Equal("Low", dto.Urgency);
        Assert.False(dto.UrgencyOverridden);
        Assert.Equal("Saman Citizen", dto.Requester!.FullName);
    }

    [Theory]
    [InlineData("coordinator")]
    [InlineData("volunteer")]
    [InlineData("admin")]
    public async Task Create_ByNonCitizenRoles_Returns403(string who)
    {
        var client = who switch
        {
            "coordinator" => await factory.CoordinatorAsync(),
            "volunteer" => await factory.VolunteerAsync(),
            _ => await factory.AdminAsync()
        };

        var response = await client.PostAsJsonAsync("/api/relief-requests", GoldenPayload(NewToken()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/relief-requests", GoldenPayload(NewToken()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Invalid_Returns400_NamingEveryBadField()
    {
        var citizen = await factory.CitizenAsync();
        var payload = new
        {
            householdSize = 0,
            district = "Atlantis",
            addressText = "x",
            latitude = 51.5,
            longitude = -0.12,
            items = Array.Empty<object>()
        };

        var response = await citizen.PostAsJsonAsync("/api/relief-requests", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("HouseholdSize", body);
        Assert.Contains("District", body);
        Assert.Contains("AddressText", body);
        Assert.Contains("Location", body);
        Assert.Contains("Items", body);
    }

    [Fact]
    public async Task Create_WithUnknownNeedCategory_Returns400()
    {
        var citizen = await factory.CitizenAsync();
        var payload = new
        {
            householdSize = 1, district = "Jaffna", addressText = "3 Temple Lane", latitude = 9.66, longitude = 80.01,
            items = new object[] { new { category = "Gold", quantity = 1 } }
        };

        var response = await citizen.PostAsJsonAsync("/api/relief-requests", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DistrictIsStoredInCanonicalSpelling()
    {
        var citizen = await factory.CitizenAsync();

        var dto = await CreateAsync(citizen, GoldenPayload(NewToken(), district: "  kalutara "));

        Assert.Equal("Kalutara", dto.District);
    }

    [Fact]
    public async Task Create_WritesAnAuditRow()
    {
        var citizen = await factory.CitizenAsync();
        var dto = await CreateAsync(citizen, GoldenPayload(NewToken()));

        var logged = await factory.WithDbAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == "request.created" && a.EntityId == dto.Id.ToString()));

        Assert.True(logged);
    }

    // ---------- List ----------

    [Fact]
    public async Task List_Citizen_SeesOnlyTheirOwnRequests()
    {
        var token = NewToken();
        var (clientA, idA) = await NewCitizenAsync();
        var (clientB, _) = await NewCitizenAsync();
        var mine = await CreateAsync(clientA, GoldenPayload(token));
        var theirs = await CreateAsync(clientB, GoldenPayload(token));

        // Searching for the shared token must still only return A's own request.
        var page = await clientA.GetFromJsonAsync<PagedResult<ReliefRequestDto>>($"/api/relief-requests?search={token}");

        Assert.Contains(page!.Items, r => r.Id == mine.Id);
        Assert.DoesNotContain(page.Items, r => r.Id == theirs.Id);
        Assert.All(page.Items, r => Assert.Equal(idA, r.Requester!.Id));
    }

    [Fact]
    public async Task List_Coordinator_SeesEveryonesRequests()
    {
        var token = NewToken();
        var (clientA, _) = await NewCitizenAsync();
        var (clientB, _) = await NewCitizenAsync();
        var a = await CreateAsync(clientA, GoldenPayload(token));
        var b = await CreateAsync(clientB, LowPayload(token));
        var coordinator = await factory.CoordinatorAsync();

        var page = await coordinator.GetFromJsonAsync<PagedResult<ReliefRequestDto>>($"/api/relief-requests?search={token}");

        Assert.Contains(page!.Items, r => r.Id == a.Id);
        Assert.Contains(page.Items, r => r.Id == b.Id);
    }

    [Fact]
    public async Task List_Volunteer_Returns403_BecauseRequestsContainPrivateLocationData()
    {
        var volunteer = await factory.VolunteerAsync();

        var response = await volunteer.GetAsync("/api/relief-requests");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/relief-requests")).StatusCode);
    }

    [Fact]
    public async Task List_DefaultSort_PutsMostUrgentFirst()
    {
        var token = NewToken();
        var citizen = await factory.CitizenAsync();
        var low = await CreateAsync(citizen, LowPayload(token));
        var critical = await CreateAsync(citizen, GoldenPayload(token));

        var page = await citizen.GetFromJsonAsync<PagedResult<ReliefRequestDto>>($"/api/relief-requests?search={token}");

        Assert.Equal(critical.Id, page!.Items[0].Id);
        Assert.Equal(low.Id, page.Items[1].Id);
    }

    [Fact]
    public async Task List_SortNewest_PutsLatestFirst()
    {
        var token = NewToken();
        var citizen = await factory.CitizenAsync();
        var first = await CreateAsync(citizen, LowPayload(token));
        await Task.Delay(30);
        var second = await CreateAsync(citizen, LowPayload(token));

        var page = await citizen.GetFromJsonAsync<PagedResult<ReliefRequestDto>>(
            $"/api/relief-requests?search={token}&sort=newest");

        Assert.Equal(second.Id, page!.Items[0].Id);
        Assert.Equal(first.Id, page.Items[1].Id);
    }

    [Fact]
    public async Task List_FiltersByUrgencyStatusAndDistrict()
    {
        var token = NewToken();
        var citizen = await factory.CitizenAsync();
        var critical = await CreateAsync(citizen, GoldenPayload(token, "Kalutara"));
        var low = await CreateAsync(citizen, LowPayload(token, "Jaffna"));
        await citizen.PostAsJsonAsync($"/api/relief-requests/{low.Id}/cancel", new { reason = "Resolved" });

        async Task<PagedResult<ReliefRequestDto>> Get(string query) =>
            (await citizen.GetFromJsonAsync<PagedResult<ReliefRequestDto>>($"/api/relief-requests?search={token}&{query}"))!;

        var byUrgency = await Get("urgency=Critical");
        Assert.Single(byUrgency.Items);
        Assert.Equal(critical.Id, byUrgency.Items[0].Id);

        var byDistrict = await Get("district=jaffna");
        Assert.Single(byDistrict.Items);
        Assert.Equal(low.Id, byDistrict.Items[0].Id);

        var byStatus = await Get("status=Cancelled");
        Assert.Single(byStatus.Items);
        Assert.Equal(low.Id, byStatus.Items[0].Id);

        Assert.Empty((await Get("status=Fulfilled")).Items);
    }

    [Fact]
    public async Task List_Paging_SplitsResultsAndReportsTotals()
    {
        var token = NewToken();
        var citizen = await factory.CitizenAsync();
        for (var i = 0; i < 3; i++) await CreateAsync(citizen, LowPayload(token));

        var page1 = (await citizen.GetFromJsonAsync<PagedResult<ReliefRequestDto>>(
            $"/api/relief-requests?search={token}&pageSize=2&page=1"))!;
        var page2 = (await citizen.GetFromJsonAsync<PagedResult<ReliefRequestDto>>(
            $"/api/relief-requests?search={token}&pageSize=2&page=2"))!;

        Assert.Equal(2, page1.Items.Count);
        Assert.Single(page2.Items);
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Empty(page1.Items.Select(i => i.Id).Intersect(page2.Items.Select(i => i.Id)));
    }

    [Fact]
    public async Task List_UnknownDistrictFilter_Returns400()
    {
        var citizen = await factory.CitizenAsync();

        var response = await citizen.GetAsync("/api/relief-requests?district=Atlantis");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_PageSizeIsCapped()
    {
        var citizen = await factory.CitizenAsync();

        var page = await citizen.GetFromJsonAsync<PagedResult<ReliefRequestDto>>("/api/relief-requests?pageSize=100000");

        Assert.Equal(100, page!.PageSize);
    }

    [Fact]
    public async Task List_SearchMatchesAddressText_CaseInsensitively()
    {
        var token = NewToken();
        var citizen = await factory.CitizenAsync();
        var dto = await CreateAsync(citizen, GoldenPayload(token));

        var page = await citizen.GetFromJsonAsync<PagedResult<ReliefRequestDto>>(
            $"/api/relief-requests?search={token.ToUpperInvariant()}");

        Assert.Contains(page!.Items, r => r.Id == dto.Id);
    }

    // ---------- Get one ----------

    [Fact]
    public async Task Get_Owner_Returns200WithItems()
    {
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, GoldenPayload(NewToken()));

        var response = await citizen.GetAsync($"/api/relief-requests/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<ReliefRequestDto>())!;
        Assert.Equal(3, dto.Items.Count);
        Assert.Contains(dto.Items, i => i.Category == "Water" && i.Quantity == 12);
    }

    [Fact]
    public async Task Get_AnotherCitizensRequest_Returns404_NotRevealingItExists()
    {
        var (owner, _) = await NewCitizenAsync();
        var (stranger, _) = await NewCitizenAsync();
        var created = await CreateAsync(owner, GoldenPayload(NewToken()));

        var response = await stranger.GetAsync($"/api/relief-requests/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_Coordinator_CanOpenAnyRequest()
    {
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, GoldenPayload(NewToken()));
        var coordinator = await factory.CoordinatorAsync();

        Assert.Equal(HttpStatusCode.OK, (await coordinator.GetAsync($"/api/relief-requests/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var citizen = await factory.CitizenAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await citizen.GetAsync($"/api/relief-requests/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().GetAsync($"/api/relief-requests/{Guid.NewGuid()}")).StatusCode);
    }

    // ---------- Update ----------

    [Fact]
    public async Task Update_Owner_RecomputesTriage_AndReplacesNeeds()
    {
        var token = NewToken();
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, LowPayload(token));
        Assert.Equal("Low", created.Urgency);

        var response = await citizen.PutAsJsonAsync($"/api/relief-requests/{created.Id}", GoldenPayload(token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ReliefRequestDto>())!;
        Assert.Equal(71, updated.TriageScore);
        Assert.Equal("Critical", updated.Urgency);
        Assert.Equal(["Food", "Shelter", "Water"], updated.Items.Select(i => i.Category).OrderBy(c => c).ToArray());
        Assert.DoesNotContain(updated.Items, i => i.Category == "Clothing");
    }

    [Fact]
    public async Task Update_KeepingTheSameCategory_ChangesQuantityWithoutViolatingTheUniqueIndex()
    {
        var token = NewToken();
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, GoldenPayload(token));
        var changed = new
        {
            householdSize = 6, elderlyCount = 2, childrenCount = 2, homeFlooded = true,
            district = "Kalutara", addressText = $"12 River Road {token}", latitude = 6.58, longitude = 79.96,
            items = new object[]
            {
                new { category = "Food", quantity = 40, description = "Dry rations" },
                new { category = "Water", quantity = 12 },
                new { category = "Shelter", quantity = 1 }
            }
        };

        var response = await citizen.PutAsJsonAsync($"/api/relief-requests/{created.Id}", changed);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<ReliefRequestDto>())!;
        Assert.Equal(40, dto.Items.Single(i => i.Category == "Food").Quantity);
        Assert.Equal(3, dto.Items.Count);
    }

    [Fact]
    public async Task Update_AnotherCitizensRequest_Returns404()
    {
        var token = NewToken();
        var (owner, _) = await NewCitizenAsync();
        var (stranger, _) = await NewCitizenAsync();
        var created = await CreateAsync(owner, LowPayload(token));

        var response = await stranger.PutAsJsonAsync($"/api/relief-requests/{created.Id}", GoldenPayload(token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ByCoordinator_Returns403()
    {
        var token = NewToken();
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, LowPayload(token));
        var coordinator = await factory.CoordinatorAsync();

        var response = await coordinator.PutAsJsonAsync($"/api/relief-requests/{created.Id}", GoldenPayload(token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_AfterReviewHasStarted_Returns409()
    {
        var token = NewToken();
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, LowPayload(token));
        await SetStatusInDbAsync(created.Id, RequestStatus.UnderReview);

        var response = await citizen.PutAsJsonAsync($"/api/relief-requests/{created.Id}", GoldenPayload(token));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_Invalid_Returns400_AndChangesNothing()
    {
        var token = NewToken();
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, LowPayload(token));
        var bad = new { householdSize = 0, district = "Atlantis", addressText = "x", items = Array.Empty<object>() };

        var response = await citizen.PutAsJsonAsync($"/api/relief-requests/{created.Id}", bad);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var unchanged = (await citizen.GetFromJsonAsync<ReliefRequestDto>($"/api/relief-requests/{created.Id}"))!;
        Assert.Equal(1, unchanged.HouseholdSize);
        Assert.Equal("Jaffna", unchanged.District);
    }

    // ---------- Cancel ----------

    [Fact]
    public async Task Cancel_Owner_SetsCancelled_AndStoresReason()
    {
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, LowPayload(NewToken()));

        var response = await citizen.PostAsJsonAsync($"/api/relief-requests/{created.Id}/cancel", new { reason = "Help arrived" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<ReliefRequestDto>())!;
        Assert.Equal("Cancelled", dto.Status);
        Assert.Equal("Help arrived", dto.CancellationReason);
    }

    [Fact]
    public async Task Cancel_Twice_Returns409()
    {
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, LowPayload(NewToken()));
        await citizen.PostAsJsonAsync($"/api/relief-requests/{created.Id}/cancel", new { });

        var second = await citizen.PostAsJsonAsync($"/api/relief-requests/{created.Id}/cancel", new { });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.InProgress)]
    [InlineData(RequestStatus.Fulfilled)]
    public async Task Cancel_AfterWorkHasStarted_Returns409(RequestStatus status)
    {
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, LowPayload(NewToken()));
        await SetStatusInDbAsync(created.Id, status);

        var response = await citizen.PostAsJsonAsync($"/api/relief-requests/{created.Id}/cancel", new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_AnotherCitizensRequest_Returns404()
    {
        var (owner, _) = await NewCitizenAsync();
        var (stranger, _) = await NewCitizenAsync();
        var created = await CreateAsync(owner, LowPayload(NewToken()));

        var response = await stranger.PostAsJsonAsync($"/api/relief-requests/{created.Id}/cancel", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_ByCoordinator_IsAllowed_AndAudited()
    {
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, LowPayload(NewToken()));
        var coordinator = await factory.CoordinatorAsync();

        var response = await coordinator.PostAsJsonAsync($"/api/relief-requests/{created.Id}/cancel", new { reason = "Duplicate" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await factory.WithDbAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == "request.cancelled" && a.EntityId == created.Id.ToString())));
    }

    [Fact]
    public async Task Cancel_WithOverlongReason_Returns400()
    {
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, LowPayload(NewToken()));

        var response = await citizen.PostAsJsonAsync($"/api/relief-requests/{created.Id}/cancel", new { reason = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Triage (staff) ----------

    [Fact]
    public async Task Triage_OverrideWithoutReason_Returns400()
    {
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, GoldenPayload(NewToken()));
        var coordinator = await factory.CoordinatorAsync();

        var response = await coordinator.PostAsJsonAsync($"/api/relief-requests/{created.Id}/triage", new { urgency = "Low" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Triage_OverrideThenEditThenRecompute_FollowsTheRules()
    {
        var token = NewToken();
        var citizen = await factory.CitizenAsync();
        var coordinator = await factory.CoordinatorAsync();
        var created = await CreateAsync(citizen, GoldenPayload(token));
        Assert.Equal("Critical", created.Urgency);

        // 1. Coordinator overrides urgency with a reason. The computed score does not change.
        var overridden = (await (await coordinator.PostAsJsonAsync($"/api/relief-requests/{created.Id}/triage",
            new { urgency = "Low", reason = "Neighbours already helping" })).Content.ReadFromJsonAsync<ReliefRequestDto>())!;
        Assert.Equal("Low", overridden.Urgency);
        Assert.True(overridden.UrgencyOverridden);
        Assert.Equal("Neighbours already helping", overridden.UrgencyOverrideReason);
        Assert.Equal(71, overridden.TriageScore);

        // 2. The citizen edits the request. The score is recomputed but the manual urgency is kept.
        var edited = (await (await citizen.PutAsJsonAsync($"/api/relief-requests/{created.Id}", LowPayload(token)))
            .Content.ReadFromJsonAsync<ReliefRequestDto>())!;
        Assert.Equal(3, edited.TriageScore);
        Assert.Equal("Low", edited.Urgency);
        Assert.True(edited.UrgencyOverridden);

        // 3. Recompute with no body clears the override and goes back to the computed level.
        var recomputed = (await (await coordinator.PostAsJsonAsync($"/api/relief-requests/{created.Id}/triage", new { }))
            .Content.ReadFromJsonAsync<ReliefRequestDto>())!;
        Assert.False(recomputed.UrgencyOverridden);
        Assert.Null(recomputed.UrgencyOverrideReason);
        Assert.Equal("Low", recomputed.Urgency); // score 3 is Low by calculation
        Assert.Equal(3, recomputed.TriageScore);
    }

    [Fact]
    public async Task Triage_ByCitizen_Returns403()
    {
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, GoldenPayload(NewToken()));

        var response = await citizen.PostAsJsonAsync($"/api/relief-requests/{created.Id}/triage",
            new { urgency = "Low", reason = "I want to look less urgent" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Triage_AfterApproval_Returns409()
    {
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, GoldenPayload(NewToken()));
        await SetStatusInDbAsync(created.Id, RequestStatus.Approved);
        var coordinator = await factory.CoordinatorAsync();

        var response = await coordinator.PostAsJsonAsync($"/api/relief-requests/{created.Id}/triage", new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Triage_UnknownRequest_Returns404()
    {
        var coordinator = await factory.CoordinatorAsync();

        var response = await coordinator.PostAsJsonAsync($"/api/relief-requests/{Guid.NewGuid()}/triage", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Triage_Override_IsAudited()
    {
        var citizen = await factory.CitizenAsync();
        var created = await CreateAsync(citizen, GoldenPayload(NewToken()));
        var coordinator = await factory.CoordinatorAsync();
        await coordinator.PostAsJsonAsync($"/api/relief-requests/{created.Id}/triage",
            new { urgency = "High", reason = "Road access is blocked" });

        var logged = await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a =>
            a.Action == "request.urgency_overridden" && a.EntityId == created.Id.ToString()));

        Assert.True(logged);
    }

    // ---------- Summary (staff) ----------

    [Fact]
    public async Task Summary_Coordinator_ReturnsCountsThatMatchTheDatabase()
    {
        var citizen = await factory.CitizenAsync();
        await CreateAsync(citizen, GoldenPayload(NewToken()));
        var coordinator = await factory.CoordinatorAsync();

        var summary = (await coordinator.GetFromJsonAsync<RequestSummaryDto>("/api/relief-requests/summary"))!;

        var (total, critical) = await factory.WithDbAsync(async db =>
            (await db.ReliefRequests.CountAsync(),
             await db.ReliefRequests.CountAsync(r => r.Urgency == UrgencyLevel.Critical &&
                 r.Status != RequestStatus.Cancelled && r.Status != RequestStatus.Rejected && r.Status != RequestStatus.Fulfilled)));
        Assert.Equal(total, summary.Total);
        Assert.Equal(critical, summary.CriticalOpen);
        Assert.Equal(summary.Total, summary.ByStatus.Values.Sum());
        Assert.Equal(summary.Total, summary.ByUrgency.Values.Sum());
        Assert.Equal(summary.Total, summary.ByDistrict.Values.Sum());
    }

    [Theory]
    [InlineData("citizen")]
    [InlineData("volunteer")]
    public async Task Summary_ByNonStaff_Returns403(string who)
    {
        var client = who == "citizen" ? await factory.CitizenAsync() : await factory.VolunteerAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/relief-requests/summary")).StatusCode);
    }

    // ---------- Database guarantees (these hold even if application code has a bug) ----------

    private async Task<Guid> SeededCitizenIdAsync() =>
        await factory.WithDbAsync(db => db.Users.Where(u => u.Email == "citizen@relieflink.lk").Select(u => u.Id).SingleAsync());

    private ReliefRequest RawRequest(Guid requesterId) => new()
    {
        RequesterId = requesterId,
        HouseholdSize = 4,
        ElderlyCount = 1,
        ChildrenCount = 1,
        District = "Kalutara",
        AddressText = "1 Test Road",
        Latitude = 6.5,
        Longitude = 80.0,
        TriageScore = 10
    };

    [Theory]
    [InlineData(0, 0, 0, 10)]      // household too small
    [InlineData(51, 0, 0, 10)]     // household too large
    [InlineData(4, 3, 3, 10)]      // elderly + children more than the household
    [InlineData(4, -1, 0, 10)]     // negative count
    [InlineData(4, 1, 1, 101)]     // score above 100
    public async Task Database_RejectsImpossibleRequests(int household, int elderly, int children, int score)
    {
        var requester = await SeededCitizenIdAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            var r = RawRequest(requester);
            r.HouseholdSize = household; r.ElderlyCount = elderly; r.ChildrenCount = children; r.TriageScore = score;
            db.ReliefRequests.Add(r);
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Database_RejectsDuplicateNeedCategoryOnOneRequest()
    {
        var requester = await SeededCitizenIdAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            var r = RawRequest(requester);
            r.Items.Add(new ReliefRequestItem { Category = NeedCategory.Food, Quantity = 1 });
            r.Items.Add(new ReliefRequestItem { Category = NeedCategory.Food, Quantity = 2 });
            db.ReliefRequests.Add(r);
            return await db.SaveChangesAsync();
        }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task Database_RejectsBadItemQuantity(int quantity)
    {
        var requester = await SeededCitizenIdAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            var r = RawRequest(requester);
            r.Items.Add(new ReliefRequestItem { Category = NeedCategory.Food, Quantity = quantity });
            db.ReliefRequests.Add(r);
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Database_WorkflowMustPointAtARealRequest()
    {
        var requester = await SeededCitizenIdAsync();

        // A workflow for a request that does not exist is refused.
        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.AgentWorkflows.Add(new AgentWorkflow
            {
                Objective = "Orphan", CreatedByUserId = requester, ReliefRequestId = Guid.NewGuid()
            });
            return await db.SaveChangesAsync();
        }));

        // A workflow for a real request is accepted.
        var citizen = await factory.CitizenAsync();
        var real = await CreateAsync(citizen, LowPayload(NewToken()));
        var saved = await factory.WithDbAsync(async db =>
        {
            db.AgentWorkflows.Add(new AgentWorkflow
            {
                Objective = "Linked", CreatedByUserId = requester, ReliefRequestId = real.Id
            });
            return await db.SaveChangesAsync();
        });
        Assert.Equal(1, saved);
    }

    [Fact]
    public async Task Database_RequestWithAWorkflowCannotBeDeleted()
    {
        var requester = await SeededCitizenIdAsync();
        var citizen = await factory.CitizenAsync();
        var real = await CreateAsync(citizen, LowPayload(NewToken()));
        await factory.WithDbAsync(async db =>
        {
            db.AgentWorkflows.Add(new AgentWorkflow { Objective = "Linked", CreatedByUserId = requester, ReliefRequestId = real.Id });
            return await db.SaveChangesAsync();
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            var r = await db.ReliefRequests.SingleAsync(x => x.Id == real.Id);
            db.ReliefRequests.Remove(r);
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Database_DeletingARequestAlsoDeletesItsItems()
    {
        var requester = await SeededCitizenIdAsync();
        var id = await factory.WithDbAsync(async db =>
        {
            var r = RawRequest(requester);
            r.Items.Add(new ReliefRequestItem { Category = NeedCategory.Food, Quantity = 1 });
            db.ReliefRequests.Add(r);
            await db.SaveChangesAsync();
            return r.Id;
        });

        await factory.WithDbAsync(async db =>
        {
            db.ReliefRequests.Remove(await db.ReliefRequests.SingleAsync(x => x.Id == id));
            return await db.SaveChangesAsync();
        });

        Assert.False(await factory.WithDbAsync(db => db.ReliefRequestItems.AnyAsync(i => i.ReliefRequestId == id)));
    }
}
