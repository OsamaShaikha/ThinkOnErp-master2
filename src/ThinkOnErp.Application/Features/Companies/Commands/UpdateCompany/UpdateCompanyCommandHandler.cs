using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Commands.UpdateCompany;

public class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand, Int64>
{
    private readonly ICompanyRepository _companyRepository;

    public UpdateCompanyCommandHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<Int64> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = new SysCompany
        {
            RowId = request.CompanyId,
            RowDesc = request.CompanyNameAr,
            RowDescE = request.CompanyNameEn,
            LegalName = request.LegalNameAr,
            LegalNameE = request.LegalNameEn,
            CompanyCode = request.CompanyCode,
            DefaultLang = request.DefaultLang,
            TaxNumber = request.TaxNumber,
            FiscalYearId = request.FiscalYearId,
            BaseCurrencyId = request.BaseCurrencyId,
            SystemLanguage = request.SystemLanguage,
            RoundingRules = request.RoundingRules,
            CountryId = request.CountryId,
            CurrId = request.CurrId,
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        return await _companyRepository.UpdateAsync(company);
    }
}
