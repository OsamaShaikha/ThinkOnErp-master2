using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Inventory;

public sealed class InvCountSessionConfiguration : IEntityTypeConfiguration<InvCountSession>
{
    public void Configure(EntityTypeBuilder<InvCountSession> builder)
    {
        builder.ToTable("INV_COUNT_SESSION");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.SessionNo)
            .HasColumnName("SESSION_NO")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.BranchId)
            .HasColumnName("BRANCH_ID")
            .IsRequired();

        builder.Property(e => e.WarehouseId)
            .HasColumnName("WAREHOUSE_ID")
            .IsRequired();

        builder.Property(e => e.CountType)
            .HasColumnName("COUNT_TYPE")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("STATUS")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.IsBlindCount)
            .HasColumnName("IS_BLIND_COUNT")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.FreezeStock)
            .HasColumnName("FREEZE_STOCK")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.StartDate)
            .HasColumnName("START_DATE")
            .IsRequired();

        builder.Property(e => e.EndDate)
            .HasColumnName("END_DATE");

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE");

    }
}