using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Data.Configurations;

/// <summary>
/// Database mappings for Component C (Volunteer Management).
/// Registered automatically through ApplyConfigurationsFromAssembly in AppDbContext.
/// </summary>
public class VolunteerConfiguration : IEntityTypeConfiguration<Volunteer>
{
    public void Configure(EntityTypeBuilder<Volunteer> e)
    {
        e.ToTable("Volunteers", t =>
        {
            t.HasCheckConstraint("CK_Volunteers_MaxHoursPerWeek", "\"MaxHoursPerWeek\" BETWEEN 1 AND 168");
        });

        e.Property(x => x.District).HasMaxLength(40).IsRequired();
        e.Property(x => x.EmergencyContactName).HasMaxLength(120).IsRequired();
        e.Property(x => x.EmergencyContactPhone).HasMaxLength(30).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(1000);

        // One-to-one link to User. A user can only have one volunteer profile.
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.UserId).IsUnique();

        e.HasMany(x => x.Skills).WithOne(s => s.Volunteer).HasForeignKey(s => s.VolunteerId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasMany(x => x.Availabilities).WithOne(a => a.Volunteer).HasForeignKey(a => a.VolunteerId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasMany(x => x.Assignments).WithOne(a => a.Volunteer).HasForeignKey(a => a.VolunteerId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => x.District);
        e.HasIndex(x => x.IsActive);
        e.HasIndex(x => x.CreatedAt);
    }
}

public class VolunteerSkillConfiguration : IEntityTypeConfiguration<VolunteerSkill>
{
    public void Configure(EntityTypeBuilder<VolunteerSkill> e)
    {
        e.ToTable("VolunteerSkills");

        e.Property(x => x.Skill).HasConversion<string>().HasMaxLength(40).IsRequired();
        e.Property(x => x.Proficiency).HasConversion<string>().HasMaxLength(20).IsRequired();
        e.Property(x => x.CertificateReference).HasMaxLength(100);

        // A volunteer can only have each skill registered once.
        e.HasIndex(x => new { x.VolunteerId, x.Skill }).IsUnique();
    }
}

public class VolunteerAvailabilityConfiguration : IEntityTypeConfiguration<VolunteerAvailability>
{
    public void Configure(EntityTypeBuilder<VolunteerAvailability> e)
    {
        e.ToTable("VolunteerAvailabilities", t =>
        {
            t.HasCheckConstraint("CK_VolunteerAvailabilities_TimeOrder",
                "\"EndTime\" IS NULL OR \"StartTime\" IS NULL OR \"EndTime\" > \"StartTime\"");
            t.HasCheckConstraint("CK_VolunteerAvailabilities_DateOrder",
                "\"SpecificEndUtc\" IS NULL OR \"SpecificStartUtc\" IS NULL OR \"SpecificEndUtc\" > \"SpecificStartUtc\"");
        });

        e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();

        e.HasIndex(x => new { x.VolunteerId, x.DayOfWeek });
        e.HasIndex(x => new { x.VolunteerId, x.SpecificStartUtc, x.SpecificEndUtc });
    }
}

public class VolunteerAssignmentConfiguration : IEntityTypeConfiguration<VolunteerAssignment>
{
    public void Configure(EntityTypeBuilder<VolunteerAssignment> e)
    {
        e.ToTable("VolunteerAssignments", t =>
        {
            t.HasCheckConstraint("CK_VolunteerAssignments_TimeOrder", "\"EndTime\" > \"StartTime\"");
        });

        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        e.Property(x => x.RoleOrTask).HasMaxLength(200);
        e.Property(x => x.Notes).HasMaxLength(1000);

        e.HasOne(x => x.ReliefRequest).WithMany().HasForeignKey(x => x.ReliefRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => x.VolunteerId);
        e.HasIndex(x => x.ReliefRequestId);
        e.HasIndex(x => x.Status);
        e.HasIndex(x => new { x.StartTime, x.EndTime });
        e.HasIndex(x => x.CreatedAt);
    }
}
