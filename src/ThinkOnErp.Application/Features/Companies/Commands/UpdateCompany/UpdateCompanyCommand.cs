using MediatR;

namespace ThinkOnErp.Application.Features.Companies.Commands.UpdateCompany;

public class UpdateCompanyCommand : IRequest<Int64>
{
    public Int64 CompanyId { get; set; }
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public Int64? CountryId { get; set; }
    public Int64? CurrId { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
