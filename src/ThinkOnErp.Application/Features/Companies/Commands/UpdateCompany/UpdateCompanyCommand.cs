using MediatR;

namespace ThinkOnErp.Application.Features.Companies.Commands.UpdateCompany;

public class UpdateCompanyCommand : IRequest<Int64>
{
    public Int64 CompanyId { get; set; }
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public string? LegalNameAr { get; set; }
    public string? LegalNameEn { get; set; }
    public string? CompanyCode { get; set; }
    public string? DefaultLang { get; set; }
    public string? TaxNumber { get; set; }
    public Int64? FiscalYearId { get; set; }
    public Int64? BaseCurrencyId { get; set; }
    public string? SystemLanguage { get; set; }
    public string? RoundingRules { get; set; }
    public Int64? CountryId { get; set; }
    public Int64? CurrId { get; set; }
    public string? CompanyLogoBase64 { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
