using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Data;

// Component A (Relief Request Management) tables. Each component adds its own partial file like this one.
public partial class AppDbContext
{
    public DbSet<ReliefRequest> ReliefRequests => Set<ReliefRequest>();
    public DbSet<ReliefRequestItem> ReliefRequestItems => Set<ReliefRequestItem>();
}
