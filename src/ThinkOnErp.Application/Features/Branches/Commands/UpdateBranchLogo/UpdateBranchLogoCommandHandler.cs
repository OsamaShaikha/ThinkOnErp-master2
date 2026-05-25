using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Commands.UpdateBranchLogo;

public class UpdateBranchLogoCommandHandler : IRequestHandler<UpdateBranchLogoCommand, Int64>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogoStorageService _logoStorageService;
    private readonly ILogger<UpdateBranchLogoCommandHandler> _logger;

    public UpdateBranchLogoCommandHandler(
        IBranchRepository branchRepository,
        ILogoStorageService logoStorageService,
        ILogger<UpdateBranchLogoCommandHandler> logger)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _logoStorageService = logoStorageService ?? throw new ArgumentNullException(nameof(logoStorageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Int64> Handle(UpdateBranchLogoCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating logo for branch ID: {BranchId}", request.BranchId);

        try
        {
            var currentPath = await _branchRepository.GetLogoPathAsync(request.BranchId);

            string? newPath = null;
            if (request.Logo.Length > 0)
            {
                newPath = await _logoStorageService.SaveLogoAsync(request.Logo, "branches", request.BranchId);
            }

            var rowsAffected = await _branchRepository.UpdateLogoPathAsync(
                request.BranchId,
                newPath,
                request.UpdateUser);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Branch not found for logo update with ID: {BranchId}", request.BranchId);
                throw new InvalidOperationException($"No branch found with ID {request.BranchId}");
            }

            if (currentPath != null)
                await _logoStorageService.DeleteLogoAsync(currentPath);

            var action = request.Logo.Length == 0 ? "deleted" : "updated";
            _logger.LogInformation("Branch logo {Action} successfully for branch ID: {BranchId}", action, request.BranchId);

            return rowsAffected;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            _logger.LogError(ex, "Error updating logo for branch ID: {BranchId}", request.BranchId);
            throw new InvalidOperationException($"Failed to update branch logo: {ex.Message}", ex);
        }
    }
}