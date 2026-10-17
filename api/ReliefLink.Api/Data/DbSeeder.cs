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

        if (!await db.Depots.AnyAsync())
        {
            var klu = new Depot
            {
                Code = "DEP-KLU-01",
                Name = "Kalutara Disaster Relief Depot",
                District = "Kalutara",
                AddressText = "Main Street, Nagoda, Kalutara",
                Latitude = 6.5854,
                Longitude = 79.9607,
                ContactPerson = "Sunil Perera",
                ContactPhone = "0342223344",
                CapacitySquareMeters = 500,
                IsActive = true
            };
            var col = new Depot
            {
                Code = "DEP-COL-01",
                Name = "Colombo Central Logistics Hub",
                District = "Colombo",
                AddressText = "Orugodawatta Warehouse Complex, Colombo",
                Latitude = 6.9428,
                Longitude = 79.8821,
                ContactPerson = "Anura Silva",
                ContactPhone = "0112334455",
                CapacitySquareMeters = 2000,
                IsActive = true
            };
            var rat = new Depot
            {
                Code = "DEP-RAT-01",
                Name = "Ratnapura Emergency Store",
                District = "Ratnapura",
                AddressText = "New Town Road, Ratnapura",
                Latitude = 6.6828,
                Longitude = 80.4034,
                ContactPerson = "Bandara Jayasundara",
                ContactPhone = "0452221122",
                CapacitySquareMeters = 600,
                IsActive = true
            };
            var gam = new Depot
            {
                Code = "DEP-GAM-01",
                Name = "Gampaha Flood Response Depot",
                District = "Gampaha",
                AddressText = "Ja-Ela Road, Gampaha",
                Latitude = 7.0840,
                Longitude = 79.9939,
                ContactPerson = "Chaminda Fernando",
                ContactPhone = "0332211443",
                CapacitySquareMeters = 800,
                IsActive = true
            };

            db.Depots.AddRange(klu, col, rat, gam);

            var wtr = new SupplyItem
            {
                Code = "SKU-WTR-5L",
                Name = "Bottled Drinking Water (5L)",
                Description = "5-liter sealed purified drinking water container",
                Category = NeedCategory.Water,
                UnitOfMeasure = "Bottle",
                WeightKg = 5.0,
                ReorderThreshold = 100,
                StandardFamilyAllocation = 4
            };
            var food = new SupplyItem
            {
                Code = "SKU-RAT-DRY",
                Name = "Standard Dry Ration Pack (Family 4-6)",
                Description = "Rice (5kg), Dhal (2kg), Canned Fish, Sugar, Tea, Biscuits",
                Category = NeedCategory.Food,
                UnitOfMeasure = "Pack",
                WeightKg = 10.0,
                ReorderThreshold = 80,
                StandardFamilyAllocation = 1
            };
            var med = new SupplyItem
            {
                Code = "SKU-MED-FAK",
                Name = "First Aid & Emergency Medical Kit",
                Description = "Bandages, antiseptics, rehydration salts, basic medicines",
                Category = NeedCategory.Medical,
                UnitOfMeasure = "Kit",
                WeightKg = 1.5,
                ReorderThreshold = 30,
                StandardFamilyAllocation = 1
            };
            var san = new SupplyItem
            {
                Code = "SKU-SAN-DIG",
                Name = "Family Dignity & Hygiene Pack",
                Description = "Soap, sanitary napkins, toothbrush/paste, towels, detergent",
                Category = NeedCategory.Sanitation,
                UnitOfMeasure = "Pack",
                WeightKg = 2.5,
                ReorderThreshold = 40,
                StandardFamilyAllocation = 1
            };
            var shl = new SupplyItem
            {
                Code = "SKU-SHL-TAR",
                Name = "Heavy Duty Tarpaulin Sheet (12x15ft)",
                Description = "Waterproof reinforced eyelet sheet for temporary roof cover",
                Category = NeedCategory.Shelter,
                UnitOfMeasure = "Sheet",
                WeightKg = 3.5,
                ReorderThreshold = 25,
                StandardFamilyAllocation = 2
            };
            var clo = new SupplyItem
            {
                Code = "SKU-CLO-BLA",
                Name = "Fleece Emergency Blankets (Pack of 2)",
                Description = "Thermal warmth blankets for displaced families",
                Category = NeedCategory.Clothing,
                UnitOfMeasure = "Pack",
                WeightKg = 1.8,
                ReorderThreshold = 50,
                StandardFamilyAllocation = 2
            };

            db.SupplyItems.AddRange(wtr, food, med, san, shl, clo);

            // Seed stock levels
            var now = DateTime.UtcNow;
            db.StockLevels.AddRange(
                // Kalutara Depot
                new StockLevel { Depot = klu, SupplyItem = wtr, QuantityOnHand = 450, QuantityReserved = 50, LastRestockedAt = now, StorageBin = "A-01" },
                new StockLevel { Depot = klu, SupplyItem = food, QuantityOnHand = 200, QuantityReserved = 20, LastRestockedAt = now, StorageBin = "A-02" },
                new StockLevel { Depot = klu, SupplyItem = med, QuantityOnHand = 75, QuantityReserved = 10, LastRestockedAt = now, StorageBin = "B-01" },
                new StockLevel { Depot = klu, SupplyItem = san, QuantityOnHand = 120, QuantityReserved = 15, LastRestockedAt = now, StorageBin = "B-02" },
                new StockLevel { Depot = klu, SupplyItem = shl, QuantityOnHand = 80, QuantityReserved = 5, LastRestockedAt = now, StorageBin = "C-01" },
                new StockLevel { Depot = klu, SupplyItem = clo, QuantityOnHand = 150, QuantityReserved = 20, LastRestockedAt = now, StorageBin = "C-02" },

                // Colombo Central Hub (High capacity)
                new StockLevel { Depot = col, SupplyItem = wtr, QuantityOnHand = 2000, QuantityReserved = 100, LastRestockedAt = now, StorageBin = "W-10" },
                new StockLevel { Depot = col, SupplyItem = food, QuantityOnHand = 1200, QuantityReserved = 50, LastRestockedAt = now, StorageBin = "F-01" },
                new StockLevel { Depot = col, SupplyItem = med, QuantityOnHand = 400, QuantityReserved = 25, LastRestockedAt = now, StorageBin = "M-01" },
                new StockLevel { Depot = col, SupplyItem = san, QuantityOnHand = 600, QuantityReserved = 30, LastRestockedAt = now, StorageBin = "S-01" },
                new StockLevel { Depot = col, SupplyItem = shl, QuantityOnHand = 500, QuantityReserved = 40, LastRestockedAt = now, StorageBin = "T-01" },
                new StockLevel { Depot = col, SupplyItem = clo, QuantityOnHand = 800, QuantityReserved = 60, LastRestockedAt = now, StorageBin = "B-01" },

                // Ratnapura Emergency Store
                new StockLevel { Depot = rat, SupplyItem = wtr, QuantityOnHand = 300, QuantityReserved = 10, LastRestockedAt = now, StorageBin = "R-01" },
                new StockLevel { Depot = rat, SupplyItem = food, QuantityOnHand = 150, QuantityReserved = 15, LastRestockedAt = now, StorageBin = "R-02" },
                new StockLevel { Depot = rat, SupplyItem = shl, QuantityOnHand = 120, QuantityReserved = 30, LastRestockedAt = now, StorageBin = "R-03" },

                // Gampaha Flood Response
                new StockLevel { Depot = gam, SupplyItem = wtr, QuantityOnHand = 400, QuantityReserved = 40, LastRestockedAt = now, StorageBin = "G-01" },
                new StockLevel { Depot = gam, SupplyItem = food, QuantityOnHand = 220, QuantityReserved = 20, LastRestockedAt = now, StorageBin = "G-02" },
                new StockLevel { Depot = gam, SupplyItem = san, QuantityOnHand = 90, QuantityReserved = 10, LastRestockedAt = now, StorageBin = "G-03" }
            );

            await db.SaveChangesAsync();
            logger.LogInformation("Seeded demo inventory (depots, supply items, and baseline stock).");
        }
    }
}
