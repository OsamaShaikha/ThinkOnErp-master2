using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Queries.GetCompanyLogo;

public class GetCompanyLogoQueryHandler : IRequestHandler<GetCompanyLogoQuery, byte[]?>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILogoStorageService _logoStorageService;

    public GetCompanyLogoQueryHandler(ICompanyRepository companyRepository, ILogoStorageService logoStorageService)
    {
        _companyRepository = companyRepository;
        _logoStorageService = logoStorageService;
    }

    public async Task<byte[]?> Handle(GetCompanyLogoQuery request, CancellationToken cancellationToken)
    {
        var path = await _companyRepository.GetLogoPathAsync(request.CompanyId);
        if (path == null) return null;

        return await _logoStorageService.GetLogoAsync(path);
    }
}