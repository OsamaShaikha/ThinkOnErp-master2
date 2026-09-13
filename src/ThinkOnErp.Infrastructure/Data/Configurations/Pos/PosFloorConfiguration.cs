using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosFloorConfiguration : IEntityTypeConfiguration<PosFloor>
{
    public void Configure(EntityTypeBuilder<PosFloor> builder)
    {
        builder.ToTable("POS_FLOOR");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(f => f.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(f => f.FloorCode).HasColumnName("FLOOR_CODE").HasMaxLength(30).IsRequired();
        builder.Property(f => f.FloorName).HasColumnName("FLOOR_NAME").HasMaxLength(100).IsRequired();
        builder.Property(f => f.SortOrder).HasColumnName("SORT_ORDER").HasDefaultValue(0);
        builder.Property(f => f.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);

        builder.HasIndex(f => new { f.BranchId, f.FloorCode }).IsUnique();
        builder.HasOne(f => f.Branch).WithMany().HasForeignKey(f => f.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PosTableConfiguration : IEntityTypeConfiguration<PosTable>
{
    public void Configure(EntityTypeBuilder<PosTable> builder)
    {
        builder.ToTable("POS_TABLE");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(t => t.FloorId).HasColumnName("FLOOR_ID").IsRequired();
        builder.Property(t => t.TableNumber).HasColumnName("TABLE_NUMBER").HasMaxLength(30).IsRequired();
        builder.Property(t => t.TableName).HasColumnName("TABLE_NAME").HasMaxLength(100);
        builder.Property(t => t.Capacity).HasColumnName("CAPACITY").HasDefaultValue(4);
        builder.Property(t => t.Status).HasColumnName("STATUS").HasConversion<int>().IsRequired();

        builder.Property(t => t.PositionX).HasColumnName("POSITION_X").HasColumnType("NUMBER(10,2)").HasDefaultValue(0m);
        builder.Property(t => t.PositionY).HasColumnName("POSITION_Y").HasColumnType("NUMBER(10,2)").HasDefaultValue(0m);
        builder.Property(t => t.Width).HasColumnName("WIDTH").HasColumnType("NUMBER(10,2)").HasDefaultValue(80m);
        builder.Property(t => t.Height).HasColumnName("HEIGHT").HasColumnType("NUMBER(10,2)").HasDefaultValue(80m);
        builder.Property(t => t.Shape).HasColumnName("SHAPE").HasMaxLength(30).HasDefaultValue("Square");

        builder.Property(t => t.ActiveOrderId).HasColumnName("ACTIVE_ORDER_ID");
        builder.Property(t => t.StatusChangedAt).HasColumnName("STATUS_CHANGED_AT").HasColumnType("TIMESTAMP");
        builder.Property(t => t.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);

        builder.HasIndex(t => new { t.FloorId, t.TableNumber }).IsUnique();
        builder.HasOne(t => t.Floor).WithMany(f => f.Tables).HasForeignKey(t => t.FloorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(t => t.ActiveOrder).WithMany().HasForeignKey(t => t.ActiveOrderId).OnDelete(DeleteBehavior.SetNull);
    }
}
