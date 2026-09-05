using MediatR;
using ThinkOnErp.Application.DTOs.Company;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Queries.GetAllCompanies;

public class GetAllCompaniesQueryHandler : IRequestHandler<GetAllCompaniesQuery, List<CompanyDto>>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILogoStorageService _logoStorageService;

    public GetAllCompaniesQueryHandler(ICompanyRepository companyRepository, ILogoStorageService logoStorageService)
    {
        _companyRepository = companyRepository;
        _logoStorageService = logoStorageService;
    }

    public async Task<List<CompanyDto>> Handle(GetAllCompaniesQuery request, CancellationToken cancellationToken)
    {
        var companies = await _companyRepository.GetAllAsync();

        var result = new List<CompanyDto>();

        foreach (var c in companies)
        {
            string? companyLogoBase64 = null;
            if (c.CompanyLogoPath != null)
            {
                var logoBytes = await _logoStorageService.GetLogoAsync(c.CompanyLogoPath);
                if (logoBytes != null)
                    companyLogoBase64 = Convert.ToBase64String(logoBytes);
            }

            var dto = new CompanyDto
            {
                CompanyId = c.Id,
                CompanyNameLocal = c.CompanyNameLocal,
                CompanyNameEn = c.CompanyNameEn,
                CountryId = c.CountryId,
                CurrId = c.CurrId,
                LegalNameLocal = c.LegalName,
                LegalNameEn = c.LegalNameE,
                CompanyCode = c.CompanyCode,
                HasLogo = c.CompanyLogoPath != null,
                CompanyLogoBase64 = companyLogoBase64,
                IsActive = c.IsActive,
                CreationUser = c.CreationUser,
                CreationDate = c.CreationDate,
                UpdateUser = c.UpdateUser,
                UpdateDate = c.UpdateDate
            };

            result.Add(dto);
        }

        return result;
    }
}
