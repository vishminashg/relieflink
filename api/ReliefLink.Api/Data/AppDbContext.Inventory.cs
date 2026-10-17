using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Data;

// Component B (Supply and Inventory Management) tables.
public partial class AppDbContext
{
    public DbSet<Depot> Depots => Set<Depot>();
    public DbSet<SupplyItem> SupplyItems => Set<SupplyItem>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
}
