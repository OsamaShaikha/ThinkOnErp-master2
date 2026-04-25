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

        var result = new List<CompanyDto>();

        foreach (var c in companies)
        {
            var dto = new CompanyDto
            {
                CompanyId = c.RowId,
                CompanyNameAr = c.RowDesc,
                CompanyNameEn = c.RowDescE,
                CountryId = c.CountryId,
                CurrId = c.CurrId,
                LegalNameAr = c.LegalName,
                LegalNameEn = c.LegalNameE,
                CompanyCode = c.CompanyCode,
                TaxNumber = c.TaxNumber,
                DefaultBranchId = c.DefaultBranchId,
                DefaultBranchName = c.DefaultBranch?.RowDescE,
                HasLogo = c.HasLogo,
                IsActive = c.IsActive,
                CreationUser = c.CreationUser,
                CreationDate = c.CreationDate,
                UpdateUser = c.UpdateUser,
                UpdateDate = c.UpdateDate
            };

            // Load company logo if it exists
            if (c.HasLogo)
            {
                var companyLogo = await _companyRepository.GetLogoAsync(c.RowId);
                if (companyLogo != null)
                {
                    dto.CompanyLogoBase64 = Convert.ToBase64String(companyLogo);
                }
            }

            // Load default branch logo if it exists
            if (c.DefaultBranchId.HasValue && c.DefaultBranch?.HasLogo == true)
            {
                // Note: We would need IBranchRepository here to get branch logo
                // For now, we'll leave this as null and implement it when needed
                dto.DefaultBranchLogoBase64 = null;
            }

            result.Add(dto);
        }

        return result;
    }
}
