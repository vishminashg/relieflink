using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Data;

public partial class AppDbContext
{
    public DbSet<Shelter> Shelters => Set<Shelter>();
    public DbSet<ShelterCheckIn> ShelterCheckIns => Set<ShelterCheckIn>();
}
