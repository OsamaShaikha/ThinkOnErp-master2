using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Queries.GetCompanyLogo;

public class GetCompanyLogoQueryHandler : IRequestHandler<GetCompanyLogoQuery, byte[]?>
{
    private readonly ICompanyRepository _companyRepository;

    public GetCompanyLogoQueryHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<byte[]?> Handle(GetCompanyLogoQuery request, CancellationToken cancellationToken)
    {
        return await _companyRepository.GetLogoAsync(request.CompanyId);
    }
}