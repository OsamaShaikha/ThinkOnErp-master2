using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Commands.UpdateCompany;

public class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand, int>
{
    private readonly ICompanyRepository _companyRepository;

    public UpdateCompanyCommandHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<int> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = new SysCompany
        {
            RowId = request.RowId,
            RowDesc = request.RowDesc,
            RowDescE = request.RowDescE,
            CountryId = request.CountryId,
            CurrId = request.CurrId,
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        return await _companyRepository.UpdateAsync(company);
    }
}
