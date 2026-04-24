using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Commands.CreateCompany;

public class CreateCompanyCommandHandler : IRequestHandler<CreateCompanyCommand, Int64>
{
    private readonly ICompanyRepository _companyRepository;

    public CreateCompanyCommandHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<Int64> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = new SysCompany
        {
            RowDesc = request.CompanyNameAr,
            RowDescE = request.CompanyNameEn,
            LegalName = request.LegalNameAr,
            LegalNameE = request.LegalNameEn,
            CompanyCode = request.CompanyCode,
            TaxNumber = request.TaxNumber,
            FiscalYearId = request.FiscalYearId,
            CountryId = request.CountryId,
            CurrId = request.CurrId,
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        return await _companyRepository.CreateAsync(company);
    }
}
