namespace ThinkOnErp.Application.DTOs.Accounting.Balances;

public sealed class AccountBalanceFilterDto
{
    public string? AccountCode { get; set; }
    public long? BranchId { get; set; }
    public long FiscalYearId { get; set; }
    public long? FiscalPeriodId { get; set; }
}
