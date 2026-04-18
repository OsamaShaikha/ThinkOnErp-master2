using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Commands.SetDefaultBranch;

/// <summary>
/// Handler for SetDefaultBranchCommand.
/// Sets the default branch for a company using the repository.
/// </summary>
public class SetDefaultBranchCommandHandler : IRequestHandler<SetDefaultBranchCommand, Int64>
{
    private readonly ICompanyRepository _companyRepository;

    /// <summary>
    /// Initializes a new instance of the SetDefaultBranchCommandHandler class.
    /// </summary>
    /// <param name="companyRepository">The company repository for data access</param>
    public SetDefaultBranchCommandHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository ?? throw new ArgumentNullException(nameof(companyRepository));
    }

    /// <summary>
    /// Handles the SetDefaultBranchCommand by calling the repository method.
    /// </summary>
    /// <param name="request">The command containing company ID, branch ID, and user information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of rows affected</returns>
    public async Task<Int64> Handle(SetDefaultBranchCommand request, CancellationToken cancellationToken)
    {
        return await _companyRepository.SetDefaultBranchAsync(
            request.CompanyId, 
            request.BranchId, 
            request.UpdateUser);
    }
}