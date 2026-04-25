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

        var dto = new CompanyDto
        {
            CompanyId = company.RowId,
            CompanyNameAr = company.RowDesc,
            CompanyNameEn = company.RowDescE,
            CountryId = company.CountryId,
            CurrId = company.CurrId,
            LegalNameAr = company.LegalName,
            LegalNameEn = company.LegalNameE,
            CompanyCode = company.CompanyCode,
            TaxNumber = company.TaxNumber,
            DefaultBranchId = company.DefaultBranchId,
            DefaultBranchName = company.DefaultBranch?.RowDescE,
            HasLogo = company.HasLogo,
            IsActive = company.IsActive,
            CreationUser = company.CreationUser,
            CreationDate = company.CreationDate,
            UpdateUser = company.UpdateUser,
            UpdateDate = company.UpdateDate
        };

        // Load company logo if it exists
        if (company.HasLogo)
        {
            var companyLogo = await _companyRepository.GetLogoAsync(company.RowId);
            if (companyLogo != null)
            {
                dto.CompanyLogoBase64 = Convert.ToBase64String(companyLogo);
            }
        }

        // Load default branch logo if it exists
        if (company.DefaultBranchId.HasValue && company.DefaultBranch?.HasLogo == true)
        {
            // Note: We would need IBranchRepository here to get branch logo
            // For now, we'll leave this as null and implement it when needed
            dto.DefaultBranchLogoBase64 = null;
        }

        return dto;
    }
}
