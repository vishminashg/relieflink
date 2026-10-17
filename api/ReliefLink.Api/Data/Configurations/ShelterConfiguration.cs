using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Data.Configurations;

public sealed class ShelterConfiguration : IEntityTypeConfiguration<Shelter>
{
    public void Configure(EntityTypeBuilder<Shelter> e)
    {
        e.ToTable("Shelters", t =>
        {
            t.HasCheckConstraint("CK_Shelters_Capacity", "\"Capacity\" BETWEEN 1 AND 100000");
            t.HasCheckConstraint("CK_Shelters_Latitude", "\"Latitude\" BETWEEN 5.5 AND 10.0");
            t.HasCheckConstraint("CK_Shelters_Longitude", "\"Longitude\" BETWEEN 79.4 AND 82.0");
        });

        e.Property(x => x.Name).HasMaxLength(160).IsRequired();
        e.Property(x => x.District).HasMaxLength(60).IsRequired();
        e.Property(x => x.Address).HasMaxLength(300).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(1000);
        e.HasIndex(x => x.District);
        e.HasIndex(x => x.IsActive);
    }
}

public sealed class ShelterCheckInConfiguration : IEntityTypeConfiguration<ShelterCheckIn>
{
    public void Configure(EntityTypeBuilder<ShelterCheckIn> e)
    {
        e.ToTable("ShelterCheckIns", t =>
            t.HasCheckConstraint("CK_ShelterCheckIns_HouseholdSize", "\"HouseholdSize\" BETWEEN 1 AND 1000"));

        e.HasOne(x => x.Shelter).WithMany(x => x.CheckIns).HasForeignKey(x => x.ShelterId)
            .OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.ReliefRequest).WithMany().HasForeignKey(x => x.ReliefRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.CheckedInByUser).WithMany().HasForeignKey(x => x.CheckedInByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.ShelterId, x.CheckedOutAt });
        e.HasIndex(x => x.ReliefRequestId).IsUnique().HasFilter("\"ReliefRequestId\" IS NOT NULL");
    }
}
