using MediatR;
using ThinkOnErp.Application.DTOs.Company;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Queries.GetCompanyById;

public class GetCompanyByIdQueryHandler : IRequestHandler<GetCompanyByIdQuery, CompanyDto?>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILogoStorageService _logoStorageService;

    public GetCompanyByIdQueryHandler(ICompanyRepository companyRepository, ILogoStorageService logoStorageService)
    {
        _companyRepository = companyRepository;
        _logoStorageService = logoStorageService;
    }

    public async Task<CompanyDto?> Handle(GetCompanyByIdQuery request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(request.CompanyId);

        if (company == null)
            return null;

        string? companyLogoBase64 = null;
        if (company.CompanyLogoPath != null)
        {
            var logoBytes = await _logoStorageService.GetLogoAsync(company.CompanyLogoPath);
            if (logoBytes != null)
                companyLogoBase64 = Convert.ToBase64String(logoBytes);
        }

        var dto = new CompanyDto
        {
            CompanyId = company.Id,
            CompanyNameLocal = company.CompanyNameLocal,
            CompanyNameEn = company.CompanyNameEn,
            CountryId = company.CountryId,
            CurrId = company.CurrId,
            LegalNameLocal = company.LegalName,
            LegalNameEn = company.LegalNameE,
            CompanyCode = company.CompanyCode,
            HasLogo = company.CompanyLogoPath != null,
            CompanyLogoBase64 = companyLogoBase64,
            IsActive = company.IsActive,
            CreationUser = company.CreationUser,
            CreationDate = company.CreationDate,
            UpdateUser = company.UpdateUser,
            UpdateDate = company.UpdateDate
        };

        return dto;
    }
}
