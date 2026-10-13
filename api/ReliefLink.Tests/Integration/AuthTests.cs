using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ReliefLink.Tests.Infrastructure;

namespace ReliefLink.Tests.Integration;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"user_{Guid.NewGuid():N}@example.com";

    private static object ValidRegistration(string? email = null, string role = "Citizen") => new
    {
        fullName = "Test Person",
        email = email ?? UniqueEmail(),
        password = "Passw0rdOK",
        phone = "+94771234567",
        district = "Kalutara",
        role
    };

    [Fact]
    public async Task Register_ValidCitizen_Returns201WithTokenAndCitizenRole()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiFactory.AuthPayload>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.Equal("Citizen", body.User.Role);
        Assert.True(body.User.IsActive);
    }

    [Fact]
    public async Task Register_Volunteer_GetsVolunteerRole()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration(role: "Volunteer"));

        var body = await response.Content.ReadFromJsonAsync<ApiFactory.AuthPayload>();
        Assert.Equal("Volunteer", body!.User.Role);
    }

    [Fact]
    public async Task Register_DoesNotReturnPasswordHash()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration());
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$2", raw); // BCrypt hashes start with $2
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409_CaseInsensitive()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", ValidRegistration(email));

        var second = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration(email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("short1")]            // too short
    [InlineData("onlylettersnodigit")] // no digit
    [InlineData("1234567890")]         // no letter
    public async Task Register_WeakPassword_Returns400(string password)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Weak Pass",
            email = UniqueEmail(),
            password,
            role = "Citizen"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public async Task Register_InvalidEmail_Returns400(string email)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Bad Email",
            email,
            password = "Passw0rdOK",
            role = "Citizen"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Coordinator")]
    public async Task Register_StaffRole_IsRejected_NoPrivilegeEscalation(string role)
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration(email, role));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var exists = await factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == email));
        Assert.False(exists);
    }

    [Fact]
    public async Task Login_CorrectCredentials_ReturnsToken()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "citizen@relieflink.lk", password = ApiFactory.DemoPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiFactory.AuthPayload>();
        Assert.Equal("Citizen", body!.User.Role);
        Assert.True(body.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "  CITIZEN@RELIEFLINK.LK ", password = ApiFactory.DemoPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "citizen@relieflink.lk", password = "WrongPassw0rd" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401_WithSameMessageAsWrongPassword()
    {
        var client = factory.CreateClient();

        var unknown = await client.PostAsJsonAsync("/api/auth/login",
            new { email = UniqueEmail(), password = "Passw0rdOK" });
        var wrong = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "citizen@relieflink.lk", password = "WrongPassw0rd" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        // The response must not reveal whether the email exists.
        Assert.Contains("Invalid email or password", await unknown.Content.ReadAsStringAsync());
        Assert.Contains("Invalid email or password", await wrong.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentUser()
    {
        var client = await factory.CitizenAsync();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<ApiFactory.UserPayload>();
        Assert.Equal("citizen@relieflink.lk", me!.Email);
    }

    [Fact]
    public async Task Me_WithTamperedToken_Returns401()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "citizen@relieflink.lk", password = ApiFactory.DemoPassword });
        var token = (await login.Content.ReadFromJsonAsync<ApiFactory.AuthPayload>())!.Token;

        // Change a character in the middle of the signature segment.
        var parts = token.Split('.');
        var sig = parts[2].ToCharArray();
        sig[10] = sig[10] == 'A' ? 'B' : 'A';
        var tampered = $"{parts[0]}.{parts[1]}.{new string(sig)}";

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithTokenSignedByDifferentKey_Returns401()
    {
        var forged = ForgeToken("a-completely-different-signing-key-0123456789", "Admin");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_ThenLoginWithSameCredentials_Works()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", ValidRegistration(email));

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Passw0rdOK" });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task PasswordIsStoredAsBCryptHash_NotPlainText()
    {
        var email = UniqueEmail();
        await factory.CreateClient().PostAsJsonAsync("/api/auth/register", ValidRegistration(email));

        var hash = await factory.WithDbAsync(db =>
            db.Users.Where(u => u.Email == email).Select(u => u.PasswordHash).SingleAsync());

        Assert.StartsWith("$2", hash);
        Assert.DoesNotContain("Passw0rdOK", hash);
    }

    [Fact]
    public async Task Register_WritesAuditLogRow()
    {
        var email = UniqueEmail();
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", ValidRegistration(email));
        var userId = (await response.Content.ReadFromJsonAsync<ApiFactory.AuthPayload>())!.User.Id.ToString();

        var audit = await factory.WithDbAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == "user.registered" && a.EntityId == userId));

        Assert.True(audit);
    }

    [Fact]
    public async Task Health_ReturnsHealthyWithDatabaseUp()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("healthy", await response.Content.ReadAsStringAsync());
    }

    /// <summary>Builds a structurally valid JWT signed with a key the API does not trust.</summary>
    private static string ForgeToken(string key, string role)
    {
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "ReliefLink",
            audience: "ReliefLinkClients",
            claims:
            [
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role)
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}
