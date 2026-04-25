using MediatR;

namespace ThinkOnErp.Application.Features.FiscalYears.Commands.UpdateFiscalYear;

public class UpdateFiscalYearCommand : IRequest<Int64>
{
    public Int64 FiscalYearId { get; set; }
    public Int64 CompanyId { get; set; }
    public Int64 BranchId { get; set; }
    public string FiscalYearCode { get; set; } = string.Empty;
    public string? FiscalYearNameAr { get; set; }
    public string? FiscalYearNameEn { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
