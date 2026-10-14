using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Data;

// Component C (Volunteer Management) tables. Each component adds its own partial file like this one.
public partial class AppDbContext
{
    public DbSet<Volunteer> Volunteers => Set<Volunteer>();
    public DbSet<VolunteerSkill> VolunteerSkills => Set<VolunteerSkill>();
    public DbSet<VolunteerAvailability> VolunteerAvailabilities => Set<VolunteerAvailability>();
    public DbSet<VolunteerAssignment> VolunteerAssignments => Set<VolunteerAssignment>();
}
