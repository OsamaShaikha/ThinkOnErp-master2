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
            TaxNumber = request.TaxNumber,
            FiscalYearId = request.FiscalYearId,
            CountryId = request.CountryId,
            CurrId = request.CurrId,
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        var result = await _companyRepository.UpdateAsync(company);

        // If company logo was provided, update it separately
        if (!string.IsNullOrEmpty(request.CompanyLogoBase64))
        {
            try
            {
                var companyLogo = Convert.FromBase64String(request.CompanyLogoBase64);
                await _companyRepository.UpdateLogoAsync(request.CompanyId, companyLogo, request.UpdateUser);
            }
            catch (FormatException)
            {
                throw new ArgumentException("Invalid Base64 format for company logo");
            }
        }

        return result;
    }
}
