using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ReliefLink.Api.Data;

namespace ReliefLink.Tests.Infrastructure;

/// <summary>
/// Hosts the real API against a real PostgreSQL database. A fresh, uniquely named database is created
/// for each test run, the real EF Core migrations are applied, and the database is dropped afterwards.
/// Set ConnectionStrings__TestDb to point at any PostgreSQL server (CI uses a service container).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "Demo#Pass123";

    private readonly string _serverConnection;
    private readonly string _dbName = $"relieflink_test_{Guid.NewGuid():N}";

    public ApiFactory()
    {
        _serverConnection = Environment.GetEnvironmentVariable("ConnectionStrings__TestDb")
            ?? "Host=localhost;Port=5433;Database=postgres;Username=postgres";

        var builder = new NpgsqlConnectionStringBuilder(_serverConnection) { Database = _dbName };

        // Program.cs reads configuration eagerly, so environment variables are the reliable override.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", builder.ConnectionString);
        Environment.SetEnvironmentVariable("Jwt__Key", "test-only-signing-key-0123456789-abcdefghij");
        Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "true");
        Environment.SetEnvironmentVariable("Seed__Enabled", "true");
        Environment.SetEnvironmentVariable("Seed__DemoPassword", DemoPassword);
    }

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(_serverConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_dbName}\"", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(_serverConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_dbName}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Runs a unit of work directly against the test database.</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Logs in as one of the seeded demo users and returns a client carrying the bearer token.</summary>
    public async Task<HttpClient> ClientForAsync(string email, string password = DemoPassword)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthPayload>()
                   ?? throw new InvalidOperationException("Empty login response.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    public Task<HttpClient> AdminAsync() => ClientForAsync("admin@relieflink.lk");
    public Task<HttpClient> CoordinatorAsync() => ClientForAsync("coordinator@relieflink.lk");
    public Task<HttpClient> VolunteerAsync() => ClientForAsync("volunteer@relieflink.lk");
    public Task<HttpClient> CitizenAsync() => ClientForAsync("citizen@relieflink.lk");

    public record AuthPayload(string Token, DateTime ExpiresAtUtc, UserPayload User);
    public record UserPayload(Guid Id, string FullName, string Email, string Role, bool IsActive);
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
