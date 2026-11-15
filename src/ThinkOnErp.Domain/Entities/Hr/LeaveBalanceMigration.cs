namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class LeaveBalanceMigration
{
    public long Id { get; set; }
    public string MigrationKey { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public int SourceYearStartMonth { get; set; }
    public int SourceYearStartDay { get; set; }
    public int TargetYearStartMonth { get; set; }
    public int TargetYearStartDay { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT"; // DRAFT, POSTED, REVERSED
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? PostedBy { get; set; }
    public DateTime? PostedDate { get; set; }
    public string? ReversedBy { get; set; }
    public DateTime? ReversedDate { get; set; }
    public List<LeaveBalanceMigrationLine> Lines { get; set; } = new();
}

public sealed class LeaveBalanceMigrationLine
{
    public long Id { get; set; }
    public long MigrationId { get; set; }
    public LeaveBalanceMigration? Migration { get; set; }
    public long SourceBalanceId { get; set; }
    public long? TargetBalanceId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public DateTime SourceCycleStartDate { get; set; }
    public DateTime SourceCycleEndDate { get; set; }
    public int SourceYearNo { get; set; }
    public bool SourceCycleStartWasNull { get; set; }
    public bool SourceCycleEndWasNull { get; set; }
    public decimal SourceAccruedDays { get; set; }
    public decimal SourceUsedDays { get; set; }
    public decimal SourceCarriedDays { get; set; }
    public DateTime TargetCycleStartDate { get; set; }
    public DateTime TargetCycleEndDate { get; set; }
    public decimal AllocatedAccruedDays { get; set; }
    public decimal AllocatedUsedDays { get; set; }
    public decimal AllocatedCarriedDays { get; set; }
    public decimal TargetAccruedBefore { get; set; }
    public decimal TargetUsedBefore { get; set; }
    public decimal TargetCarriedBefore { get; set; }
    public long? SourceMovementId { get; set; }
    public long? TargetMovementId { get; set; }
}
