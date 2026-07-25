using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Commands.SetCompanyStatus;

public class SetCompanyStatusCommandHandler : IRequestHandler<SetCompanyStatusCommand, bool>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILogger<SetCompanyStatusCommandHandler> _logger;

    public SetCompanyStatusCommandHandler(ICompanyRepository companyRepository, ILogger<SetCompanyStatusCommandHandler> logger)
    {
        _companyRepository = companyRepository;
        _logger = logger;
    }

    public async Task<bool> Handle(SetCompanyStatusCommand request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(request.CompanyId);
        if (company == null)
        {
            _logger.LogWarning("Company not found with ID {CompanyId} when updating status", request.CompanyId);
            return false;
        }

        company.IsActive = request.IsActive;
        company.UpdateUser = request.UpdateUser;
        company.UpdateDate = DateTime.Now;

        var rowsAffected = await _companyRepository.UpdateAsync(company);
        _logger.LogInformation("Company {CompanyId} status updated to IsActive={IsActive}", request.CompanyId, request.IsActive);
        return rowsAffected > 0;
    }
}
