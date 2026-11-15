using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class LeaveMovementConfiguration : IEntityTypeConfiguration<LeaveBalanceMovement>
{
    public void Configure(EntityTypeBuilder<LeaveBalanceMovement> b)
    {
        b.ToTable("HR_LEAVE_MOVEMENT");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(e => e.BalanceId).HasColumnName("BALANCE_ID");
        b.HasOne(e => e.Balance).WithMany().HasForeignKey(e => e.BalanceId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_HR_LEAVE_MOVEMENT_BAL");
        b.Property(e => e.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        b.Property(e => e.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(100).IsRequired();
        b.Property(e => e.CycleStartDate).HasColumnName("CYCLE_START_DATE");
        b.Property(e => e.SequenceNo).HasColumnName("SEQUENCE_NO");
        b.Property(e => e.Kind).HasColumnName("KIND").HasMaxLength(40).IsRequired();
        b.Property(e => e.OperationKey).HasColumnName("OPERATION_KEY").HasMaxLength(64).IsRequired();
        b.Property(e => e.PayloadHash).HasColumnName("PAYLOAD_HASH").HasMaxLength(64);
        b.Property(e => e.Source).HasColumnName("SOURCE_REF").HasMaxLength(300).IsRequired();
        b.Property(e => e.Reason).HasColumnName("REASON").HasMaxLength(1000).IsRequired();
        b.Property(e => e.Actor).HasColumnName("ACTOR").HasMaxLength(200).IsRequired();
        b.Property(e => e.CreatedDate).HasColumnName("CREATED_DATE");
        b.Property(e => e.AccruedDelta).HasColumnName("ACCRUED_DELTA").HasPrecision(12, 4);
        b.Property(e => e.UsedDelta).HasColumnName("USED_DELTA").HasPrecision(12, 4);
        b.Property(e => e.CarriedDelta).HasColumnName("CARRIED_DELTA").HasPrecision(12, 4);
        b.Property(e => e.AccruedAfter).HasColumnName("ACCRUED_AFTER").HasPrecision(12, 4);
        b.Property(e => e.UsedAfter).HasColumnName("USED_AFTER").HasPrecision(12, 4);
        b.Property(e => e.CarriedAfter).HasColumnName("CARRIED_AFTER").HasPrecision(12, 4);
        b.Property(e => e.ExpiredDays).HasColumnName("EXPIRED_DAYS").HasPrecision(12, 4);
        b.Property(e => e.TransferredDays).HasColumnName("TRANSFERRED_DAYS").HasPrecision(12, 4);
        b.Property(e => e.ReversesMovementId).HasColumnName("REVERSES_MOVEMENT_ID");
        b.HasOne<LeaveBalanceMovement>().WithMany().HasForeignKey(e => e.ReversesMovementId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_HR_LEAVE_MOVEMENT_REV");
        b.HasIndex(e => e.OperationKey).IsUnique().HasDatabaseName("UX_HR_LEAVE_MOVEMENT_OP");
        b.HasIndex(e => new { e.BalanceId, e.SequenceNo }).IsUnique().HasDatabaseName("UX_HR_LEAVE_MOVEMENT_SEQ");
        b.HasIndex(e => e.ReversesMovementId).IsUnique().HasDatabaseName("UX_HR_LEAVE_MOVEMENT_REV");
    }
}
