using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Commands.CreateCompany;

public class CreateCompanyCommandHandler : IRequestHandler<CreateCompanyCommand, decimal>
{
    private readonly ICompanyRepository _companyRepository;

    public CreateCompanyCommandHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<decimal> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = new SysCompany
        {
            RowDesc = request.RowDesc,
            RowDescE = request.RowDescE,
            CountryId = request.CountryId,
            CurrId = request.CurrId,
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        return await _companyRepository.CreateAsync(company);
    }
}
