using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Commands.UpdateBranchLogo;

/// <summary>
/// Handler for updating branch logos.
/// Uses repository pattern to maintain clean architecture separation.
/// </summary>
public class UpdateBranchLogoCommandHandler : IRequestHandler<UpdateBranchLogoCommand, Int64>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogger<UpdateBranchLogoCommandHandler> _logger;

    public UpdateBranchLogoCommandHandler(
        IBranchRepository branchRepository,
        ILogger<UpdateBranchLogoCommandHandler> logger)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Int64> Handle(UpdateBranchLogoCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating logo for branch ID: {BranchId}", request.BranchId);

        try
        {
            var rowsAffected = await _branchRepository.UpdateLogoAsync(
                request.BranchId,
                request.Logo,
                request.UpdateUser);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Branch not found for logo update with ID: {BranchId}", request.BranchId);
                throw new InvalidOperationException($"No branch found with ID {request.BranchId}");
            }

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