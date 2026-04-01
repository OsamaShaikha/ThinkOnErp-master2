using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Commands.DeleteCompany;

public class DeleteCompanyCommandHandler : IRequestHandler<DeleteCompanyCommand, int>
{
    private readonly ICompanyRepository _companyRepository;

    public DeleteCompanyCommandHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<int> Handle(DeleteCompanyCommand request, CancellationToken cancellationToken)
    {
        return await _companyRepository.DeleteAsync(request.RowId);
    }
}
