using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Queries.GetBranchLogo;

public class GetBranchLogoQueryHandler : IRequestHandler<GetBranchLogoQuery, byte[]?>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogoStorageService _logoStorageService;
    private readonly ILogger<GetBranchLogoQueryHandler> _logger;

    public GetBranchLogoQueryHandler(
        IBranchRepository branchRepository,
        ILogoStorageService logoStorageService,
        ILogger<GetBranchLogoQueryHandler> logger)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _logoStorageService = logoStorageService ?? throw new ArgumentNullException(nameof(logoStorageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]?> Handle(GetBranchLogoQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving logo for branch ID: {BranchId}", request.BranchId);

        try
        {
            var path = await _branchRepository.GetLogoPathAsync(request.BranchId);
            if (path == null)
            {
                _logger.LogInformation("No logo path found for branch ID: {BranchId}", request.BranchId);
                return null;
            }

            var logo = await _logoStorageService.GetLogoAsync(path);
            if (logo == null || logo.Length == 0)
            {
                _logger.LogInformation("No logo file found for branch ID: {BranchId}", request.BranchId);
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