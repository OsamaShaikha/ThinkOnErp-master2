using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Queries.GetBranchLogo;

/// <summary>
/// Handler for retrieving branch logos.
/// Uses repository pattern to maintain clean architecture separation.
/// </summary>
public class GetBranchLogoQueryHandler : IRequestHandler<GetBranchLogoQuery, byte[]?>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogger<GetBranchLogoQueryHandler> _logger;

    public GetBranchLogoQueryHandler(
        IBranchRepository branchRepository,
        ILogger<GetBranchLogoQueryHandler> logger)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]?> Handle(GetBranchLogoQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving logo for branch ID: {BranchId}", request.BranchId);

        try
        {
            var logo = await _branchRepository.GetLogoAsync(request.BranchId);

            if (logo == null || logo.Length == 0)
            {
                _logger.LogInformation("No logo found for branch ID: {BranchId}", request.BranchId);
                return null;
            }

            _logger.LogInformation("Retrieved logo for branch ID: {BranchId}, Size: {LogoSize} bytes", 
                request.BranchId, logo.Length);

            return logo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logo for branch ID: {BranchId}", request.BranchId);
            throw new InvalidOperationException($"Failed to retrieve branch logo: {ex.Message}", ex);
        }
    }
}