using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Commands.UpdateCompanyLogo;

public class UpdateCompanyLogoCommandHandler : IRequestHandler<UpdateCompanyLogoCommand, Int64>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILogoStorageService _logoStorageService;

    public UpdateCompanyLogoCommandHandler(ICompanyRepository companyRepository, ILogoStorageService logoStorageService)
    {
        _companyRepository = companyRepository;
        _logoStorageService = logoStorageService;
    }

    public async Task<Int64> Handle(UpdateCompanyLogoCommand request, CancellationToken cancellationToken)
    {
        var currentPath = await _companyRepository.GetLogoPathAsync(request.CompanyId);

        string? newPath = null;
        if (request.Logo.Length > 0)
        {
            newPath = await _logoStorageService.SaveLogoAsync(request.Logo, "companies", request.CompanyId);
        }

        var rowsAffected = await _companyRepository.UpdateLogoPathAsync(request.CompanyId, newPath, request.UpdateUser);

        if (currentPath != null)
            await _logoStorageService.DeleteLogoAsync(currentPath);

        return rowsAffected;
    }
}