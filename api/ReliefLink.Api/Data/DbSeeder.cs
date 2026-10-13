using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Data;

/// <summary>
/// Creates one demo account per role when Seed:Enabled is true and the Users table is empty.
/// The shared demo password comes from configuration (Seed:DemoPassword), never from source code.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        if (!config.GetValue("Seed:Enabled", false)) return;
        if (await db.Users.AnyAsync()) return;

        var password = config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:Enabled is true but Seed:DemoPassword is not set; skipping seed.");
            return;
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(password, 11);
        db.Users.AddRange(
            new User { FullName = "System Admin", Email = "admin@relieflink.lk", PasswordHash = hash, Role = UserRole.Admin, District = "Colombo" },
            new User { FullName = "Kamal Coordinator", Email = "coordinator@relieflink.lk", PasswordHash = hash, Role = UserRole.Coordinator, District = "Kalutara" },
            new User { FullName = "Nimali Volunteer", Email = "volunteer@relieflink.lk", PasswordHash = hash, Role = UserRole.Volunteer, District = "Kalutara" },
            new User { FullName = "Saman Citizen", Email = "citizen@relieflink.lk", PasswordHash = hash, Role = UserRole.Citizen, District = "Kalutara" });

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded demo users.");
    }
}
