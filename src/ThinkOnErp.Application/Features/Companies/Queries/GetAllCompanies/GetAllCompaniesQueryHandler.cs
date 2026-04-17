using MediatR;
using ThinkOnErp.Application.DTOs.Company;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Queries.GetAllCompanies;

public class GetAllCompaniesQueryHandler : IRequestHandler<GetAllCompaniesQuery, List<CompanyDto>>
{
    private readonly ICompanyRepository _companyRepository;

    public GetAllCompaniesQueryHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<List<CompanyDto>> Handle(GetAllCompaniesQuery request, CancellationToken cancellationToken)
    {
        var companies = await _companyRepository.GetAllAsync();

        return companies.Select(c => new CompanyDto
        {
            CompanyId = c.RowId,
            CompanyNameAr = c.RowDesc,
            CompanyNameEn = c.RowDescE,
            CountryId = c.CountryId,
            CurrId = c.CurrId,
            LegalNameAr = c.LegalName,
            LegalNameEn = c.LegalNameE,
            CompanyCode = c.CompanyCode,
            DefaultLang = c.DefaultLang,
            TaxNumber = c.TaxNumber,
            FiscalYearId = c.FiscalYearId,
            FiscalYearCode = c.FiscalYear?.FiscalYearCode,
            BaseCurrencyId = c.BaseCurrencyId,
            SystemLanguage = c.SystemLanguage,
            RoundingRules = c.RoundingRules,
            IsActive = c.IsActive,
            CreationUser = c.CreationUser,
            CreationDate = c.CreationDate,
            UpdateUser = c.UpdateUser,
            UpdateDate = c.UpdateDate
        }).ToList();
    }
}
