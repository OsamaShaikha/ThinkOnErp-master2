using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosShiftCashMovementConfiguration : IEntityTypeConfiguration<PosShiftCashMovement>
{
    public void Configure(EntityTypeBuilder<PosShiftCashMovement> builder)
    {
        builder.ToTable("POS_SHIFT_CASH_MOVEMENT");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(m => m.ShiftId).HasColumnName("SHIFT_ID").IsRequired();
        builder.Property(m => m.MovementType).HasColumnName("MOVEMENT_TYPE").HasConversion<int>().IsRequired();
        builder.Property(m => m.Amount).HasColumnName("AMOUNT").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(m => m.Reason).HasColumnName("REASON").HasMaxLength(500).IsRequired();
        builder.Property(m => m.ApprovedBy).HasColumnName("APPROVED_BY").HasMaxLength(100);

        builder.Property(m => m.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(m => m.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");

        builder.HasOne(m => m.Shift).WithMany(s => s.CashMovements).HasForeignKey(m => m.ShiftId).OnDelete(DeleteBehavior.Cascade);
    }
}
