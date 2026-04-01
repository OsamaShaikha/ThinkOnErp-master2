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
        var company = await _companyRepository.GetByIdAsync(request.RowId);

        if (company == null)
            return null;

        return new CompanyDto
        {
            RowId = company.RowId,
            RowDesc = company.RowDesc,
            RowDescE = company.RowDescE,
            CountryId = company.CountryId,
            CurrId = company.CurrId,
            IsActive = company.IsActive,
            CreationUser = company.CreationUser,
            CreationDate = company.CreationDate,
            UpdateUser = company.UpdateUser,
            UpdateDate = company.UpdateDate
        };
    }
}
