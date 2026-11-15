namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class PayrollPostingConfiguration
{
    public long BranchId { get; set; }
    public string SalaryExpense { get; set; } = string.Empty;
    public string EmployerSscExpense { get; set; } = string.Empty;
    public string SalaryPayable { get; set; } = string.Empty;
    public string SscPayable { get; set; } = string.Empty;
    public string TaxPayable { get; set; } = string.Empty;
    public string DeductionsPayable { get; set; } = string.Empty;
    public string? LoanReceivableAccount { get; set; }
    public string? AdvanceReceivableAccount { get; set; }
    public long CurrencyId { get; set; }
    public int VoucherType { get; set; }
}
