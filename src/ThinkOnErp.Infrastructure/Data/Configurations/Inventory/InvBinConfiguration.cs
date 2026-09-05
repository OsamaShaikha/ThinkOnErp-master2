using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvBinConfiguration : IEntityTypeConfiguration<InvBin>
{
    public void Configure(EntityTypeBuilder<InvBin> builder)
    {
        builder.ToTable("INV_BIN");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ZoneId)
            .HasColumnName("ZONE_ID")
            .IsRequired();

        builder.Property(e => e.BinCode)
            .HasColumnName("BIN_CODE")
            .HasColumnType("NUMBER(6)")
            .IsRequired();

        builder.Property(e => e.MaxWeight)
            .HasColumnName("MAX_WEIGHT")
            .HasColumnType("NUMBER(14,4)");

        builder.Property(e => e.MaxVolume)
            .HasColumnName("MAX_VOLUME")
            .HasColumnType("NUMBER(18,4)");

        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false)
            .IsRequired();

    }
}