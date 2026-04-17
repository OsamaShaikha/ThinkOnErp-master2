using MediatR;

namespace ThinkOnErp.Application.Features.FiscalYears.Commands.CloseFiscalYear;

public class CloseFiscalYearCommand : IRequest<Int64>
{
    public Int64 FiscalYearId { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
