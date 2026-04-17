using MediatR;
using ThinkOnErp.Application.DTOs.Company;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Queries.GetCompanyById;

public class GetCompanyByIdQueryHandler : IRequestHandler<GetCompanyByIdQuery, CompanyDto?>
{
    private readonly ICompanyRepository _companyRepository;

    public GetCompanyByIdQueryHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<CompanyDto?> Handle(GetCompanyByIdQuery request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(request.CompanyId);

        if (company == null)
            return null;

        return new CompanyDto
        {
            CompanyId = company.RowId,
            CompanyNameAr = company.RowDesc,
            CompanyNameEn = company.RowDescE,
            CountryId = company.CountryId,
            CurrId = company.CurrId,
            LegalNameAr = company.LegalName,
            LegalNameEn = company.LegalNameE,
            CompanyCode = company.CompanyCode,
            DefaultLang = company.DefaultLang,
            TaxNumber = company.TaxNumber,
            FiscalYearId = company.FiscalYearId,
            FiscalYearCode = company.FiscalYear?.FiscalYearCode,
            BaseCurrencyId = company.BaseCurrencyId,
            SystemLanguage = company.SystemLanguage,
            RoundingRules = company.RoundingRules,
            IsActive = company.IsActive,
            CreationUser = company.CreationUser,
            CreationDate = company.CreationDate,
            UpdateUser = company.UpdateUser,
            UpdateDate = company.UpdateDate
        };
    }
}
