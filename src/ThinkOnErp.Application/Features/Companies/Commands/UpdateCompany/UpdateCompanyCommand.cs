using MediatR;

namespace ThinkOnErp.Application.Features.Companies.Commands.UpdateCompany;

public class UpdateCompanyCommand : IRequest<int>
{
    public decimal RowId { get; set; }
    public string RowDesc { get; set; } = string.Empty;
    public string RowDescE { get; set; } = string.Empty;
    public decimal? CountryId { get; set; }
    public decimal? CurrId { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
