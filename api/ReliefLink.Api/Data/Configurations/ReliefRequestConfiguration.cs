using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Data.Configurations;

/// <summary>
/// Database mapping for Component A. Kept in its own file (and picked up automatically by
/// ApplyConfigurationsFromAssembly) so that each student's component does not edit AppDbContext.cs.
/// </summary>
public class ReliefRequestConfiguration : IEntityTypeConfiguration<ReliefRequest>
{
    public void Configure(EntityTypeBuilder<ReliefRequest> e)
    {
        e.ToTable("ReliefRequests", t =>
        {
            // The database refuses impossible data even if application code has a bug.
            t.HasCheckConstraint("CK_ReliefRequests_HouseholdSize", "\"HouseholdSize\" BETWEEN 1 AND 50");
            t.HasCheckConstraint("CK_ReliefRequests_Counts", "\"ElderlyCount\" >= 0 AND \"ChildrenCount\" >= 0 AND \"ElderlyCount\" + \"ChildrenCount\" <= \"HouseholdSize\"");
            t.HasCheckConstraint("CK_ReliefRequests_TriageScore", "\"TriageScore\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_ReliefRequests_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
            t.HasCheckConstraint("CK_ReliefRequests_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
        });

        e.Property(x => x.District).HasMaxLength(40).IsRequired();
        e.Property(x => x.AddressText).HasMaxLength(300).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(1000);
        e.Property(x => x.PhotoUrl).HasMaxLength(500);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Urgency).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.UrgencyOverrideReason).HasMaxLength(500);
        e.Property(x => x.CancellationReason).HasMaxLength(500);
        e.Property(x => x.TriageReasonsJson).HasColumnType("jsonb");

        e.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasMany(x => x.Items).WithOne(i => i.ReliefRequest).HasForeignKey(i => i.ReliefRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => x.Status);
        e.HasIndex(x => x.Urgency);
        e.HasIndex(x => x.District);
        e.HasIndex(x => x.RequesterId);
        e.HasIndex(x => x.CreatedAt);

        // A workflow points at the request it is planning for. Restrict: a request with workflows cannot be deleted.
        e.HasMany<AgentWorkflow>().WithOne().HasForeignKey(w => w.ReliefRequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ReliefRequestItemConfiguration : IEntityTypeConfiguration<ReliefRequestItem>
{
    public void Configure(EntityTypeBuilder<ReliefRequestItem> e)
    {
        e.ToTable("ReliefRequestItems", t =>
            t.HasCheckConstraint("CK_ReliefRequestItems_Quantity", "\"Quantity\" BETWEEN 1 AND 1000"));

        e.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Description).HasMaxLength(300);

        // One line per category per request.
        e.HasIndex(x => new { x.ReliefRequestId, x.Category }).IsUnique();
    }
}
