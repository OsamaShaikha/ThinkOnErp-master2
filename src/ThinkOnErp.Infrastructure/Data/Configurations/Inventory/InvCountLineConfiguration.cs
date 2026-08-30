using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvCountLineConfiguration : IEntityTypeConfiguration<InvCountLine>
{
    public void Configure(EntityTypeBuilder<InvCountLine> builder)
    {
        builder.ToTable("INV_COUNT_LINE");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.CountSessionId)
            .HasColumnName("COUNT_SESSION_ID")
            .IsRequired();

        builder.Property(e => e.ItemId)
            .HasColumnName("ITEM_ID")
            .IsRequired();

        builder.Property(e => e.BinId)
            .HasColumnName("BIN_ID");

        builder.Property(e => e.LotId)
            .HasColumnName("LOT_ID");

        builder.Property(e => e.SystemQty)
            .HasColumnName("SYSTEM_QTY")
            .HasColumnType("NUMBER(14,4)")
            .IsRequired();

        builder.Property(e => e.CountedQty)
            .HasColumnName("COUNTED_QTY")
            .HasColumnType("NUMBER(14,4)")
            .IsRequired();

        builder.Property(e => e.VarianceQty)
            .HasColumnName("VARIANCE_QTY")
            .HasColumnType("NUMBER(14,4)")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(100);

        builder.Property(e => e.CountedBy)
            .HasColumnName("COUNTED_BY")
            .HasMaxLength(100);

        builder.Property(e => e.CountedAt)
            .HasColumnName("COUNTED_AT");

    }
}