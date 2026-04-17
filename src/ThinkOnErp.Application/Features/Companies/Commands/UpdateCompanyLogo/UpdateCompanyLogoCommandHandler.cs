using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Commands.UpdateCompanyLogo;

public class UpdateCompanyLogoCommandHandler : IRequestHandler<UpdateCompanyLogoCommand, Int64>
{
    private readonly ICompanyRepository _companyRepository;

    public UpdateCompanyLogoCommandHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<Int64> Handle(UpdateCompanyLogoCommand request, CancellationToken cancellationToken)
    {
        return await _companyRepository.UpdateLogoAsync(request.CompanyId, request.Logo, request.UpdateUser);
    }
}