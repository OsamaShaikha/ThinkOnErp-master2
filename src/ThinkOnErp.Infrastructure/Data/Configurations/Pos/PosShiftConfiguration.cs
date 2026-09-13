using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosShiftConfiguration : IEntityTypeConfiguration<PosShift>
{
    public void Configure(EntityTypeBuilder<PosShift> builder)
    {
        builder.ToTable("POS_SHIFT");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(s => s.TillId).HasColumnName("TILL_ID").IsRequired();
        builder.Property(s => s.CashierUserId).HasColumnName("CASHIER_USER_ID").IsRequired();
        builder.Property(s => s.ShiftNumber).HasColumnName("SHIFT_NUMBER").HasMaxLength(50).IsRequired();
        builder.Property(s => s.Status).HasColumnName("STATUS").HasConversion<int>().IsRequired();

        builder.Property(s => s.OpenedAt).HasColumnName("OPENED_AT").HasColumnType("TIMESTAMP").IsRequired();
        builder.Property(s => s.BlindClosedAt).HasColumnName("BLIND_CLOSED_AT").HasColumnType("TIMESTAMP");
        builder.Property(s => s.ClosedAt).HasColumnName("CLOSED_AT").HasColumnType("TIMESTAMP");

        builder.Property(s => s.OpeningFloat).HasColumnName("OPENING_FLOAT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(s => s.TotalCashSales).HasColumnName("TOTAL_CASH_SALES").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(s => s.TotalCardSales).HasColumnName("TOTAL_CARD_SALES").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(s => s.TotalOtherSales).HasColumnName("TOTAL_OTHER_SALES").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(s => s.TotalCashRefunds).HasColumnName("TOTAL_CASH_REFUNDS").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(s => s.TotalCardRefunds).HasColumnName("TOTAL_CARD_REFUNDS").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(s => s.TotalFloatIn).HasColumnName("TOTAL_FLOAT_IN").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(s => s.TotalCashDrop).HasColumnName("TOTAL_CASH_DROP").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(s => s.TotalPayOut).HasColumnName("TOTAL_PAY_OUT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(s => s.TotalTipPayout).HasColumnName("TOTAL_TIP_PAYOUT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);

        builder.Property(s => s.ExpectedCashInDrawer).HasColumnName("EXPECTED_CASH_IN_DRAWER").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(s => s.CountedCashAmount).HasColumnName("COUNTED_CASH_AMOUNT").HasColumnType("NUMBER(18,4)");
        builder.Property(s => s.VarianceAmount).HasColumnName("VARIANCE_AMOUNT").HasColumnType("NUMBER(18,4)");

        builder.Property(s => s.IsAudited).HasColumnName("IS_AUDITED").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(s => s.AuditedBy).HasColumnName("AUDITED_BY").HasMaxLength(100);
        builder.Property(s => s.AuditedAt).HasColumnName("AUDITED_AT").HasColumnType("TIMESTAMP");
        builder.Property(s => s.AuditNotes).HasColumnName("AUDIT_NOTES").HasMaxLength(1000);

        builder.Property(s => s.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(s => s.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(s => s.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(s => s.UpdateDate).HasColumnName("UPDATE_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(s => new { s.BranchId, s.ShiftNumber }).IsUnique();
        builder.HasOne(s => s.Till).WithMany().HasForeignKey(s => s.TillId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.CashierUser).WithMany().HasForeignKey(s => s.CashierUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Branch).WithMany().HasForeignKey(s => s.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}
