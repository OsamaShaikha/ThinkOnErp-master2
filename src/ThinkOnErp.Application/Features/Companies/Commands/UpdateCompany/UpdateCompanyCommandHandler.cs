using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Commands.UpdateCompany;

public class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand, Int64>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILogoStorageService _logoStorageService;

    public UpdateCompanyCommandHandler(ICompanyRepository companyRepository, ILogoStorageService logoStorageService)
    {
        _companyRepository = companyRepository;
        _logoStorageService = logoStorageService;
    }

    public async Task<Int64> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var existing = await _companyRepository.GetByIdAsync(request.CompanyId);
        if (existing == null) return 0;

        existing.CompanyNameAr = request.CompanyNameAr;
        existing.CompanyNameEn = request.CompanyNameEn;
        existing.LegalName = request.LegalNameAr;
        existing.LegalNameE = request.LegalNameEn;
        existing.CompanyCode = request.CompanyCode;
        existing.CountryId = request.CountryId;
        existing.CurrId = request.CurrId;
        existing.DefaultBranchId = request.DefaultBranchId;
        existing.UpdateUser = request.UpdateUser;
        existing.UpdateDate = DateTime.UtcNow;

        if (request.CompanyLogo != null)
        {
            if (existing.CompanyLogoPath != null)
                await _logoStorageService.DeleteLogoAsync(existing.CompanyLogoPath);

            existing.CompanyLogoPath = await _logoStorageService.SaveLogoAsync(request.CompanyLogo, "companies", request.CompanyId);
        }

        return await _companyRepository.UpdateAsync(existing);
    }
}
