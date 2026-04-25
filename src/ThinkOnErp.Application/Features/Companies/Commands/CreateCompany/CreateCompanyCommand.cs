using MediatR;

namespace ThinkOnErp.Application.Features.Companies.Commands.CreateCompany;

public class CreateCompanyCommand : IRequest<Int64>
{
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public string? LegalNameAr { get; set; }
    public string? LegalNameEn { get; set; }
    public string? CompanyCode { get; set; }
    public string? DefaultLang { get; set; }
    public string? TaxNumber { get; set; }
    public Int64? CountryId { get; set; }
    public Int64? CurrId { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
