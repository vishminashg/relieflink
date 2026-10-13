using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Tests.Infrastructure;

namespace ReliefLink.Tests.Integration;

[Collection(ApiCollection.Name)]
public class UsersTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"staff_{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task ListUsers_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("citizen")]
    [InlineData("volunteer")]
    [InlineData("coordinator")]
    public async Task ListUsers_NonAdminRoles_Return403(string who)
    {
        var client = who switch
        {
            "citizen" => await factory.CitizenAsync(),
            "volunteer" => await factory.VolunteerAsync(),
            _ => await factory.CoordinatorAsync()
        };

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_AsAdmin_ReturnsSeededUsersWithPaging()
    {
        var admin = await factory.AdminAsync();

        var response = await admin.GetAsync("/api/users?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<UserDto>>();
        Assert.Equal(2, page!.Items.Count);
        Assert.True(page.TotalCount >= 4);
        Assert.Equal(2, page.PageSize);
    }

    [Fact]
    public async Task ListUsers_FilterByRole_ReturnsOnlyThatRole()
    {
        var admin = await factory.AdminAsync();

        var page = await admin.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?role=Coordinator");

        Assert.NotEmpty(page!.Items);
        Assert.All(page.Items, u => Assert.Equal("Coordinator", u.Role));
    }

    [Fact]
    public async Task ListUsers_Search_MatchesEmailOrName()
    {
        var admin = await factory.AdminAsync();

        var page = await admin.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?search=nimali");

        Assert.Contains(page!.Items, u => u.Email == "volunteer@relieflink.lk");
    }

    [Fact]
    public async Task ListUsers_PageSizeIsCapped()
    {
        var admin = await factory.AdminAsync();

        var page = await admin.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?pageSize=100000");

        Assert.Equal(100, page!.PageSize);
    }

    [Fact]
    public async Task CreateUser_AsAdmin_CanCreateCoordinator_WhoCanThenLogIn()
    {
        var admin = await factory.AdminAsync();
        var email = UniqueEmail();

        var create = await admin.PostAsJsonAsync("/api/users", new
        {
            fullName = "New Coordinator",
            email,
            password = "Coord1nator!",
            role = "Coordinator",
            district = "Gampaha"
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email, password = "Coord1nator!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<ApiFactory.AuthPayload>();
        Assert.Equal("Coordinator", body!.User.Role);
    }

    [Fact]
    public async Task CreateUser_AsCitizen_Returns403_AndCreatesNothing()
    {
        var citizen = await factory.CitizenAsync();
        var email = UniqueEmail();

        var response = await citizen.PostAsJsonAsync("/api/users", new
        {
            fullName = "Sneaky Admin",
            email,
            password = "Passw0rdOK",
            role = "Admin"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == email)));
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_Returns409()
    {
        var admin = await factory.AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/users", new
        {
            fullName = "Duplicate",
            email = "citizen@relieflink.lk",
            password = "Passw0rdOK",
            role = "Citizen"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WeakPassword_Returns400()
    {
        var admin = await factory.AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/users", new
        {
            fullName = "Weak",
            email = UniqueEmail(),
            password = "weak",
            role = "Citizen"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DisableUser_BlocksLogin_AndInvalidatesExistingToken()
    {
        var admin = await factory.AdminAsync();
        var email = UniqueEmail();
        // A second administrator, so the endpoint under test is one that has no active-user check of its own.
        await admin.PostAsJsonAsync("/api/users", new
        {
            fullName = "Soon Disabled",
            email,
            password = "Passw0rdOK",
            role = "Admin"
        });

        // Log in first, so we hold a token that was valid before the account is disabled.
        var userClient = await factory.ClientForAsync(email, "Passw0rdOK");
        Assert.Equal(HttpStatusCode.OK, (await userClient.GetAsync("/api/users")).StatusCode);

        var userId = await factory.WithDbAsync(db =>
            db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        var patch = await admin.PatchAsJsonAsync($"/api/users/{userId}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        // The old token stops working immediately on a protected endpoint, and a fresh login is refused.
        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("/api/auth/me")).StatusCode);
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email, password = "Passw0rdOK" });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotDisableOwnAccount()
    {
        var admin = await factory.AdminAsync();
        var me = await admin.GetFromJsonAsync<ApiFactory.UserPayload>("/api/auth/me");

        var response = await admin.PatchAsJsonAsync($"/api/users/{me!.Id}/active", new { isActive = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetActive_UnknownUser_Returns404()
    {
        var admin = await factory.AdminAsync();

        var response = await admin.PatchAsJsonAsync($"/api/users/{Guid.NewGuid()}/active", new { isActive = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Database_EnforcesUniqueEmail_EvenWithoutTheApplicationCheck()
    {
        var email = UniqueEmail();
        await factory.WithDbAsync(async db =>
        {
            db.Users.Add(new Api.Domain.User { FullName = "A", Email = email, PasswordHash = "x" });
            return await db.SaveChangesAsync();
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.Users.Add(new Api.Domain.User { FullName = "B", Email = email, PasswordHash = "y" });
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Timestamps_AreStampedOnCreate_AndUpdatedAtMovesOnChange()
    {
        var email = UniqueEmail();
        var id = await factory.WithDbAsync(async db =>
        {
            var u = new Api.Domain.User { FullName = "Stamp", Email = email, PasswordHash = "x" };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            return u.Id;
        });

        var (created, firstUpdated) = await factory.WithDbAsync(async db =>
        {
            var u = await db.Users.SingleAsync(x => x.Id == id);
            return (u.CreatedAt, u.UpdatedAt);
        });
        Assert.True(created > DateTime.UtcNow.AddMinutes(-1));

        await Task.Delay(20);
        var secondUpdated = await factory.WithDbAsync(async db =>
        {
            var u = await db.Users.SingleAsync(x => x.Id == id);
            u.FullName = "Stamp Changed";
            await db.SaveChangesAsync();
            return u.UpdatedAt;
        });

        Assert.True(secondUpdated > firstUpdated);
    }
}
