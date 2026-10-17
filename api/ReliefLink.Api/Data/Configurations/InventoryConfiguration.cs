using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Data.Configurations;

public class DepotConfiguration : IEntityTypeConfiguration<Depot>
{
    public void Configure(EntityTypeBuilder<Depot> e)
    {
        e.ToTable("Depots");

        e.Property(x => x.Code).HasMaxLength(20).IsRequired();
        e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        e.Property(x => x.District).HasMaxLength(40).IsRequired();
        e.Property(x => x.AddressText).HasMaxLength(250).IsRequired();
        e.Property(x => x.ContactPerson).HasMaxLength(100).IsRequired();
        e.Property(x => x.ContactPhone).HasMaxLength(30).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(500);

        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => x.District);
        e.HasIndex(x => x.IsActive);
    }
}

public class SupplyItemConfiguration : IEntityTypeConfiguration<SupplyItem>
{
    public void Configure(EntityTypeBuilder<SupplyItem> e)
    {
        e.ToTable("SupplyItems", t =>
        {
            t.HasCheckConstraint("CK_SupplyItems_ReorderThreshold", "\"ReorderThreshold\" >= 0");
            t.HasCheckConstraint("CK_SupplyItems_StandardAllocation", "\"StandardFamilyAllocation\" >= 1");
        });

        e.Property(x => x.Code).HasMaxLength(30).IsRequired();
        e.Property(x => x.Name).HasMaxLength(150).IsRequired();
        e.Property(x => x.Description).HasMaxLength(500);
        e.Property(x => x.Category).HasConversion<string>().HasMaxLength(30).IsRequired();
        e.Property(x => x.UnitOfMeasure).HasMaxLength(30).IsRequired();

        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => x.Category);
        e.HasIndex(x => x.IsActive);
    }
}

public class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> e)
    {
        e.ToTable("StockLevels", t =>
        {
            t.HasCheckConstraint("CK_StockLevels_OnHand_NonNegative", "\"QuantityOnHand\" >= 0");
            t.HasCheckConstraint("CK_StockLevels_Reserved_NonNegative", "\"QuantityReserved\" >= 0");
            t.HasCheckConstraint("CK_StockLevels_OnHand_GTE_Reserved", "\"QuantityOnHand\" >= \"QuantityReserved\"");
        });

        e.Property(x => x.StorageBin).HasMaxLength(50);

        e.HasOne(x => x.Depot).WithMany(d => d.StockLevels).HasForeignKey(x => x.DepotId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.SupplyItem).WithMany(i => i.StockLevels).HasForeignKey(x => x.SupplyItemId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => new { x.DepotId, x.SupplyItemId }).IsUnique();
        e.HasIndex(x => x.QuantityOnHand);
    }
}

public class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> e)
    {
        e.ToTable("StockReservations", t =>
        {
            t.HasCheckConstraint("CK_StockReservations_Quantity_Positive", "\"Quantity\" > 0");
        });

        e.Property(x => x.ReservationNumber).HasMaxLength(40).IsRequired();
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(500);

        e.HasOne(x => x.Depot).WithMany(d => d.Reservations).HasForeignKey(x => x.DepotId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.SupplyItem).WithMany().HasForeignKey(x => x.SupplyItemId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.ReliefRequest).WithMany().HasForeignKey(x => x.ReliefRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        e.HasOne(x => x.ReservedByUser).WithMany().HasForeignKey(x => x.ReservedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => x.ReservationNumber).IsUnique();
        e.HasIndex(x => x.Status);
        e.HasIndex(x => x.ExpiresAt);
        e.HasIndex(x => x.ReliefRequestId);
    }
}

public class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> e)
    {
        e.ToTable("StockTransactions");

        e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        e.Property(x => x.ReferenceNumber).HasMaxLength(60);
        e.Property(x => x.Notes).HasMaxLength(500);

        e.HasOne(x => x.Depot).WithMany(d => d.Transactions).HasForeignKey(x => x.DepotId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.SupplyItem).WithMany().HasForeignKey(x => x.SupplyItemId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.PerformedByUser).WithMany().HasForeignKey(x => x.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.DepotId, x.SupplyItemId });
        e.HasIndex(x => x.CreatedAt);
    }
}
