using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("HR_LEAVE_TYPE");

        builder.HasKey(e => e.LeaveTypeCode);

        builder.Property(e => e.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.NameLocal).HasColumnName("NAME_LOCAL").HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(e => e.IsPaid).HasColumnName("IS_PAID").HasConversion<int>().IsRequired();
        builder.Property(e => e.TracksBalance).HasColumnName("TRACKS_BALANCE").HasConversion<int>().IsRequired();
        builder.Property(e => e.IsStatutory).HasColumnName("IS_STATUTORY").HasConversion<int>().IsRequired();
        builder.Property(e => e.MaxDaysPerYear).HasColumnName("MAX_DAYS_PER_YEAR").HasPrecision(6, 2).IsRequired();
        builder.Property(e => e.CarryForwardAllowed).HasColumnName("CARRY_FORWARD_ALLOWED").HasConversion<int>().IsRequired();
        builder.Property(e => e.CarryForwardCapDays).HasColumnName("CARRY_FORWARD_CAP_DAYS").HasPrecision(6, 2).IsRequired();
        builder.Property(e => e.YearEndExcessAction).HasColumnName("YEAR_END_EXCESS_ACTION").HasMaxLength(20).IsRequired();
        builder.Property(e => e.RequiresDocumentation).HasColumnName("REQUIRES_DOCUMENTATION").HasConversion<int>().IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<int>().IsRequired();

        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("HR_LEAVE_REQUEST");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.StartDate).HasColumnName("START_DATE").IsRequired();
        builder.Property(e => e.EndDate).HasColumnName("END_DATE").IsRequired();
        builder.Property(e => e.DaysRequested).HasColumnName("DAYS_REQUESTED").HasPrecision(6, 2).IsRequired();
        builder.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(30).IsRequired();
        builder.Property(e => e.Reason).HasColumnName("REASON").HasMaxLength(500);
        builder.Property(e => e.ApprovedBy).HasColumnName("APPROVED_BY").HasMaxLength(100);
        builder.Property(e => e.ApprovalDate).HasColumnName("APPROVAL_DATE");
        builder.Property(e => e.RejectionReason).HasColumnName("REJECTION_REASON").HasMaxLength(500);
        builder.Property(e => e.AttachmentFileRef).HasColumnName("ATTACHMENT_FILE_REF").HasMaxLength(500);
        builder.Property(e => e.CancelledBy).HasColumnName("CANCELLED_BY").HasMaxLength(100);
        builder.Property(e => e.CancellationDate).HasColumnName("CANCELLATION_DATE");
        builder.Property(e => e.CancellationReason).HasColumnName("CANCELLATION_REASON").HasMaxLength(500);

        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.LeaveType)
            .WithMany(t => t.Requests)
            .HasForeignKey(e => e.LeaveTypeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.ToTable("HR_LEAVE_BALANCE");
        builder.HasIndex(e => new { e.EmployeeCode, e.LeaveTypeCode, e.CycleStartDate }).IsUnique().HasDatabaseName("UX_HR_LEAVE_BALANCE_CYCLE");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.YearNo).HasColumnName("YEAR_NO").IsRequired();
        builder.Property(e => e.CycleStartDate).HasColumnName("CYCLE_START_DATE");
        builder.Property(e => e.CycleEndDate).HasColumnName("CYCLE_END_DATE");
        builder.Property(e => e.AccruedDays).HasColumnName("ACCRUED_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.UsedDays).HasColumnName("USED_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.CarriedForwardDays).HasColumnName("CARRIED_FORWARD_DAYS").HasPrecision(8, 4).IsRequired();

        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.LeaveType)
            .WithMany(t => t.Balances)
            .HasForeignKey(e => e.LeaveTypeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class HrLeaveSettingsConfiguration : IEntityTypeConfiguration<HrLeaveSettings>
{
    public void Configure(EntityTypeBuilder<HrLeaveSettings> builder)
    {
        builder.ToTable("HR_LEAVE_SETTINGS", table => table.ExcludeFromMigrations());
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(e => e.LeaveYearStartMonth).HasColumnName("LEAVE_YEAR_START_MONTH").IsRequired();
        builder.Property(e => e.LeaveYearStartDay).HasColumnName("LEAVE_YEAR_START_DAY").IsRequired();
        builder.Property(e => e.BalanceDisplayFormat).HasColumnName("BALANCE_DISPLAY_FORMAT").HasMaxLength(20).IsRequired();
        builder.Property(e => e.StandardWorkdayHours).HasColumnName("STANDARD_WORKDAY_HOURS").HasPrecision(6, 2);
        builder.Property(e => e.AutomaticAccrualEnabled).HasColumnName("ACCRUAL_AUTO_ENABLED").HasConversion<int>().IsRequired();
        builder.Property(e => e.AccrualTimeZoneId).HasColumnName("ACCRUAL_TIME_ZONE_ID").HasMaxLength(120);
        builder.Property(e => e.EligibleCitizenshipCountryCode).HasColumnName("ELIGIBLE_CITIZENSHIP_COUNTRY_CODE").HasColumnType("VARCHAR2(2 CHAR)").HasMaxLength(2);
        builder.Property(e => e.LeaveCashoutWageBasisCode).HasColumnName("LEAVE_CASHOUT_WAGE_BASIS_CODE").HasMaxLength(30);
        builder.Property(e => e.LeaveCashoutBasisComponentCode).HasColumnName("LEAVE_CASHOUT_BASIS_COMPONENT_CODE").HasMaxLength(100);
        builder.Property(e => e.LeaveCashoutDayDivisor).HasColumnName("LEAVE_CASHOUT_DAY_DIVISOR").HasPrecision(10, 4);
        builder.Property(e => e.LeaveCashoutPayComponentCode).HasColumnName("LEAVE_CASHOUT_PAY_COMPONENT_CODE").HasMaxLength(100);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public sealed class LeaveYearCloseConfiguration : IEntityTypeConfiguration<LeaveYearClose>
{
    public void Configure(EntityTypeBuilder<LeaveYearClose> builder)
    {
        builder.ToTable("HR_LEAVE_YEAR_CLOSE", table => table.ExcludeFromMigrations());
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(e => e.CloseKey).HasColumnName("CLOSE_KEY").HasMaxLength(300).IsRequired();
        builder.HasIndex(e => e.CloseKey).IsUnique().HasDatabaseName("UX_HR_LEAVE_CLOSE_KEY");
        builder.Property(e => e.FromYear).HasColumnName("FROM_YEAR").IsRequired();
        builder.Property(e => e.ToYear).HasColumnName("TO_YEAR").IsRequired();
        builder.Property(e => e.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.PostedBy).HasColumnName("POSTED_BY").HasMaxLength(200);
        builder.Property(e => e.PostedDate).HasColumnName("POSTED_DATE");
        builder.Property(e => e.UnpostedBy).HasColumnName("UNPOSTED_BY").HasMaxLength(200);
        builder.Property(e => e.UnpostedDate).HasColumnName("UNPOSTED_DATE");
        builder.HasMany(e => e.Lines).WithOne(e => e.Close).HasForeignKey(e => e.CloseId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LeaveYearCloseLineConfiguration : IEntityTypeConfiguration<LeaveYearCloseLine>
{
    public void Configure(EntityTypeBuilder<LeaveYearCloseLine> builder)
    {
        builder.ToTable("HR_LEAVE_YEAR_CLOSE_LINE", table => table.ExcludeFromMigrations());
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.HasIndex(e => new { e.CloseId, e.SourceBalanceId }).IsUnique().HasDatabaseName("UX_HR_LEAVE_CLOSE_LINE");
        builder.Property(e => e.CloseId).HasColumnName("CLOSE_ID").IsRequired();
        builder.Property(e => e.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.SourceYear).HasColumnName("SOURCE_YEAR").IsRequired();
        builder.Property(e => e.TargetYear).HasColumnName("TARGET_YEAR").IsRequired();
        builder.Property(e => e.SourceBalanceId).HasColumnName("SOURCE_BALANCE_ID").IsRequired();
        builder.Property(e => e.SourceCycleStartDate).HasColumnName("SOURCE_CYCLE_START_DATE");
        builder.Property(e => e.SourceCycleEndDate).HasColumnName("SOURCE_CYCLE_END_DATE");
        builder.Property(e => e.TargetCycleStartDate).HasColumnName("TARGET_CYCLE_START_DATE").IsRequired();
        builder.Property(e => e.TargetCycleEndDate).HasColumnName("TARGET_CYCLE_END_DATE").IsRequired();
        builder.Property(e => e.SourceAccruedBefore).HasColumnName("SOURCE_ACCRUED_BEFORE").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.SourceUsedDays).HasColumnName("SOURCE_USED_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.SourceCarriedBefore).HasColumnName("SOURCE_CARRIED_BEFORE").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.TargetAccruedBefore).HasColumnName("TARGET_ACCRUED_BEFORE").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.TargetCarriedBefore).HasColumnName("TARGET_CARRIED_BEFORE").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.TargetUsedBefore).HasColumnName("TARGET_USED_BEFORE").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.UnusedDays).HasColumnName("UNUSED_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.CarriedDays).HasColumnName("CARRIED_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.ExpiredDays).HasColumnName("EXPIRED_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.CashoutDays).HasColumnName("CASHOUT_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.TargetBalanceId).HasColumnName("TARGET_BALANCE_ID");
        builder.Property(e => e.TracksBalanceSnapshot).HasColumnName("TRACKS_BALANCE_SNAPSHOT").HasConversion<int>();
        builder.Property(e => e.CarryForwardAllowedSnapshot).HasColumnName("CARRY_FORWARD_ALLOWED_SNAPSHOT").HasConversion<int>();
        builder.Property(e => e.CarryForwardCapDaysSnapshot).HasColumnName("CARRY_FORWARD_CAP_DAYS_SNAPSHOT").HasPrecision(6, 2);
        builder.Property(e => e.YearEndExcessActionSnapshot).HasColumnName("YEAR_END_EXCESS_ACTION_SNAPSHOT").HasMaxLength(20);
    }
}

public sealed class LeaveBalanceMigrationConfiguration : IEntityTypeConfiguration<LeaveBalanceMigration>
{
    public void Configure(EntityTypeBuilder<LeaveBalanceMigration> builder)
    {
        builder.ToTable("HR_LEAVE_BALANCE_MIGRATION", table =>
        {
            table.HasCheckConstraint("CK_HR_BAL_MIGRATION_STATUS", "STATUS IN ('DRAFT','POSTED','REVERSED')");
            table.HasCheckConstraint("CK_HR_BAL_MIGRATION_MONTHS", "SOURCE_YEAR_START_MONTH BETWEEN 1 AND 12 AND TARGET_YEAR_START_MONTH BETWEEN 1 AND 12");
            table.HasCheckConstraint("CK_HR_BAL_MIGRATION_DAYS", "SOURCE_YEAR_START_DAY BETWEEN 1 AND 31 AND TARGET_YEAR_START_DAY BETWEEN 1 AND 31");
        });
        builder.HasKey(e => e.Id).HasName("PK_HR_BAL_MIGRATION");
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(e => e.MigrationKey).HasColumnName("MIGRATION_KEY").HasMaxLength(200).IsRequired();
        builder.HasIndex(e => e.MigrationKey).IsUnique().HasDatabaseName("UX_HR_BAL_MIGRATION_KEY");
        builder.Property(e => e.EffectiveDate).HasColumnName("EFFECTIVE_DATE").IsRequired();
        builder.Property(e => e.SourceYearStartMonth).HasColumnName("SOURCE_YEAR_START_MONTH").HasPrecision(2, 0).IsRequired();
        builder.Property(e => e.SourceYearStartDay).HasColumnName("SOURCE_YEAR_START_DAY").HasPrecision(2, 0).IsRequired();
        builder.Property(e => e.TargetYearStartMonth).HasColumnName("TARGET_YEAR_START_MONTH").HasPrecision(2, 0).IsRequired();
        builder.Property(e => e.TargetYearStartDay).HasColumnName("TARGET_YEAR_START_DAY").HasPrecision(2, 0).IsRequired();
        builder.Property(e => e.PayloadHash).HasColumnName("PAYLOAD_HASH").HasMaxLength(64).IsRequired();
        builder.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.PostedBy).HasColumnName("POSTED_BY").HasMaxLength(200);
        builder.Property(e => e.PostedDate).HasColumnName("POSTED_DATE");
        builder.Property(e => e.ReversedBy).HasColumnName("REVERSED_BY").HasMaxLength(200);
        builder.Property(e => e.ReversedDate).HasColumnName("REVERSED_DATE");
        builder.HasMany(e => e.Lines).WithOne(e => e.Migration).HasForeignKey(e => e.MigrationId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_HR_BAL_MIG_LINE_BATCH");
    }
}

public sealed class LeaveBalanceMigrationLineConfiguration : IEntityTypeConfiguration<LeaveBalanceMigrationLine>
{
    public void Configure(EntityTypeBuilder<LeaveBalanceMigrationLine> builder)
    {
        builder.ToTable("HR_LEAVE_BALANCE_MIGRATION_LINE", table =>
        {
            table.HasCheckConstraint("CK_HR_BAL_MIG_LINE_DATES", "SOURCE_CYCLE_END_DATE>=SOURCE_CYCLE_START_DATE AND TARGET_CYCLE_END_DATE>=TARGET_CYCLE_START_DATE");
            table.HasCheckConstraint("CK_HR_BAL_MIG_LINE_SOURCE_NULLS", "SOURCE_START_WAS_NULL IN (0,1) AND SOURCE_END_WAS_NULL IN (0,1)");
        });
        builder.HasKey(e => e.Id).HasName("PK_HR_BAL_MIGRATION_LINE");
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.HasIndex(e => new { e.MigrationId, e.SourceBalanceId, e.TargetCycleStartDate }).IsUnique().HasDatabaseName("UX_HR_BAL_MIGRATION_LINE");
        builder.Property(e => e.MigrationId).HasColumnName("MIGRATION_ID").IsRequired();
        builder.Property(e => e.SourceBalanceId).HasColumnName("SOURCE_BALANCE_ID").IsRequired();
        builder.Property(e => e.TargetBalanceId).HasColumnName("TARGET_BALANCE_ID");
        builder.Property(e => e.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.SourceCycleStartDate).HasColumnName("SOURCE_CYCLE_START_DATE").IsRequired();
        builder.Property(e => e.SourceCycleEndDate).HasColumnName("SOURCE_CYCLE_END_DATE").IsRequired();
        builder.Property(e => e.SourceYearNo).HasColumnName("SOURCE_YEAR_NO").HasPrecision(4, 0).IsRequired();
        builder.Property(e => e.SourceCycleStartWasNull).HasColumnName("SOURCE_START_WAS_NULL").HasConversion<int>().HasPrecision(1, 0).IsRequired();
        builder.Property(e => e.SourceCycleEndWasNull).HasColumnName("SOURCE_END_WAS_NULL").HasConversion<int>().HasPrecision(1, 0).IsRequired();
        builder.Property(e => e.SourceAccruedDays).HasColumnName("SOURCE_ACCRUED_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.SourceUsedDays).HasColumnName("SOURCE_USED_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.SourceCarriedDays).HasColumnName("SOURCE_CARRIED_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.TargetCycleStartDate).HasColumnName("TARGET_CYCLE_START_DATE").IsRequired();
        builder.Property(e => e.TargetCycleEndDate).HasColumnName("TARGET_CYCLE_END_DATE").IsRequired();
        builder.Property(e => e.AllocatedAccruedDays).HasColumnName("ALLOCATED_ACCRUED_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.AllocatedUsedDays).HasColumnName("ALLOCATED_USED_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.AllocatedCarriedDays).HasColumnName("ALLOCATED_CARRIED_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.TargetAccruedBefore).HasColumnName("TARGET_ACCRUED_BEFORE").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.TargetUsedBefore).HasColumnName("TARGET_USED_BEFORE").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.TargetCarriedBefore).HasColumnName("TARGET_CARRIED_BEFORE").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.SourceMovementId).HasColumnName("SOURCE_MOVEMENT_ID");
        builder.Property(e => e.TargetMovementId).HasColumnName("TARGET_MOVEMENT_ID");
        builder.HasOne<LeaveBalance>().WithMany().HasForeignKey(e => e.SourceBalanceId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_HR_BAL_MIG_LINE_SRC");
        builder.HasOne<LeaveBalance>().WithMany().HasForeignKey(e => e.TargetBalanceId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_HR_BAL_MIG_LINE_TGT");
        builder.HasOne<LeaveBalanceMovement>().WithMany().HasForeignKey(e => e.SourceMovementId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_HR_BAL_MIG_LINE_SRCMOV");
        builder.HasOne<LeaveBalanceMovement>().WithMany().HasForeignKey(e => e.TargetMovementId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_HR_BAL_MIG_LINE_TGTMOV");
    }
}

public sealed class LeaveCashoutSettlementConfiguration : IEntityTypeConfiguration<LeaveCashoutSettlement>
{
    public void Configure(EntityTypeBuilder<LeaveCashoutSettlement> builder)
    {
        builder.ToTable("HR_LEAVE_CASHOUT", table => table.ExcludeFromMigrations());
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(e => e.SettlementKey).HasColumnName("SETTLEMENT_KEY").HasMaxLength(200).IsRequired();
        builder.HasIndex(e => e.SettlementKey).IsUnique().HasDatabaseName("UX_HR_LEAVE_CASHOUT_KEY");
        builder.Property(e => e.PayloadHash).HasColumnName("PAYLOAD_HASH").HasMaxLength(64).IsRequired();
        builder.Property(e => e.LeaveYearCloseId).HasColumnName("LEAVE_YEAR_CLOSE_ID").IsRequired();
        builder.Property(e => e.PayPeriod).HasColumnName("PAY_PERIOD").HasMaxLength(40).IsRequired();
        builder.Property(e => e.WageBasisCode).HasColumnName("WAGE_BASIS_CODE").HasMaxLength(30).IsRequired();
        builder.Property(e => e.BasisComponentCode).HasColumnName("BASIS_COMPONENT_CODE").HasMaxLength(100);
        builder.Property(e => e.DayDivisor).HasColumnName("DAY_DIVISOR").HasPrecision(10, 4).IsRequired();
        builder.Property(e => e.PayComponentCode).HasColumnName("PAY_COMPONENT_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.PostedBy).HasColumnName("POSTED_BY").HasMaxLength(200);
        builder.Property(e => e.PostedDate).HasColumnName("POSTED_DATE");
        builder.Property(e => e.ReversedBy).HasColumnName("REVERSED_BY").HasMaxLength(200);
        builder.Property(e => e.ReversedDate).HasColumnName("REVERSED_DATE");
        builder.Property(e => e.ReversalReason).HasColumnName("REVERSAL_REASON").HasMaxLength(1000);
        builder.HasMany(e => e.Lines).WithOne(e => e.Settlement).HasForeignKey(e => e.SettlementId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<LeaveYearClose>().WithMany().HasForeignKey(e => e.LeaveYearCloseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalaryComponent>().WithMany().HasForeignKey(e => e.PayComponentCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalaryComponent>().WithMany().HasForeignKey(e => e.BasisComponentCode).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LeaveCashoutSettlementLineConfiguration : IEntityTypeConfiguration<LeaveCashoutSettlementLine>
{
    public void Configure(EntityTypeBuilder<LeaveCashoutSettlementLine> builder)
    {
        builder.ToTable("HR_LEAVE_CASHOUT_LINE", table => table.ExcludeFromMigrations());
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.HasIndex(e => new { e.SettlementId, e.LeaveYearCloseLineId }).IsUnique().HasDatabaseName("UX_HR_LEAVE_CASHOUT_LINE");
        builder.Property(e => e.SettlementId).HasColumnName("SETTLEMENT_ID").IsRequired();
        builder.Property(e => e.LeaveYearCloseLineId).HasColumnName("LEAVE_YEAR_CLOSE_LINE_ID").IsRequired();
        builder.Property(e => e.EmployeeCode).HasColumnName("EMPLOYEE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.SalaryStructureId).HasColumnName("SALARY_STRUCTURE_ID").IsRequired();
        builder.Property(e => e.SalaryEffectiveFrom).HasColumnName("SALARY_EFFECTIVE_FROM").IsRequired();
        builder.Property(e => e.CashoutDays).HasColumnName("CASHOUT_DAYS").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.WageBasisAmount).HasColumnName("WAGE_BASIS_AMOUNT").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.DayDivisor).HasColumnName("DAY_DIVISOR").HasPrecision(10, 4).IsRequired();
        builder.Property(e => e.CashoutAmount).HasColumnName("CASHOUT_AMOUNT").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.CurrencyCode).HasColumnName("CURRENCY_CODE").HasMaxLength(20).IsRequired();
        builder.Property(e => e.BasisSnapshotJson).HasColumnName("BASIS_SNAPSHOT_JSON").HasColumnType("NCLOB").IsRequired();
        builder.Property(e => e.PayrollAdjustmentId).HasColumnName("PAYROLL_ADJUSTMENT_ID");
        builder.HasIndex(e => e.PayrollAdjustmentId).IsUnique().HasDatabaseName("UX_HR_LCO_LINE_ADJ");
        builder.HasOne(e => e.PayrollAdjustment).WithMany().HasForeignKey(e => e.PayrollAdjustmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LeaveYearCloseLine>().WithMany().HasForeignKey(e => e.LeaveYearCloseLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalaryStructure>().WithMany().HasForeignKey(e => e.SalaryStructureId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LeavePolicyConfiguration : IEntityTypeConfiguration<LeavePolicy>
{
    public void Configure(EntityTypeBuilder<LeavePolicy> builder)
    {
        builder.ToTable("HR_LEAVE_POLICY", table => table.ExcludeFromMigrations());

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.PolicyName).HasColumnName("POLICY_NAME").HasMaxLength(200).IsRequired();
        builder.Property(e => e.LeaveTypeCode).HasColumnName("LEAVE_TYPE_CODE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(e => e.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.HasIndex(e => new { e.LeaveTypeCode, e.EffectiveFrom }).HasDatabaseName("IX_HR_LEAVE_POLICY_EFFECTIVE");
        builder.Property(e => e.AccrualMethod).HasColumnName("ACCRUAL_METHOD").HasMaxLength(50).IsRequired();
        builder.Property(e => e.AccrualRate).HasColumnName("ACCRUAL_RATE").HasPrecision(8, 4).IsRequired();
        builder.Property(e => e.AnnualEntitlementDays).HasColumnName("ANNUAL_ENTITLEMENT_DAYS").HasPrecision(8, 4);
        builder.Property(e => e.CycleBasis).HasColumnName("CYCLE_BASIS").HasMaxLength(40).IsRequired();
        builder.Property(e => e.ProrateFirstCycle).HasColumnName("PRORATE_FIRST_CYCLE").HasConversion<int>().IsRequired();
        builder.Property(e => e.ApplicableTo).HasColumnName("APPLICABLE_TO").HasMaxLength(50).IsRequired();
        builder.Property(e => e.MinServiceMonths).HasColumnName("MIN_SERVICE_MONTHS").IsRequired();
        builder.Property(e => e.Tier1YearsThreshold).HasColumnName("TIER1_YEARS_THRESHOLD").IsRequired();
        builder.Property(e => e.Tier1Days).HasColumnName("TIER1_DAYS").HasPrecision(6, 2).IsRequired();
        builder.Property(e => e.Tier2Days).HasColumnName("TIER2_DAYS").HasPrecision(6, 2).IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<int>().IsRequired();

        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.LeaveType)
            .WithMany()
            .HasForeignKey(e => e.LeaveTypeCode)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_HR_LEAVE_POLICY_TYPE");
    }
}
