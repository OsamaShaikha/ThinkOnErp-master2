using MediatR;

namespace ThinkOnErp.Application.Features.Companies.Commands.CreateCompany;

public class CreateCompanyCommand : IRequest<decimal>
{
    public string RowDesc { get; set; } = string.Empty;
    public string RowDescE { get; set; } = string.Empty;
    public decimal? CountryId { get; set; }
    public decimal? CurrId { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
