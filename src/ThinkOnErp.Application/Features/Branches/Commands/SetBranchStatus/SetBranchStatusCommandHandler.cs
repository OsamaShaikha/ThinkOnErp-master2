using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Commands.SetBranchStatus;

public class SetBranchStatusCommandHandler : IRequestHandler<SetBranchStatusCommand, bool>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogger<SetBranchStatusCommandHandler> _logger;

    public SetBranchStatusCommandHandler(IBranchRepository branchRepository, ILogger<SetBranchStatusCommandHandler> logger)
    {
        _branchRepository = branchRepository;
        _logger = logger;
    }

    public async Task<bool> Handle(SetBranchStatusCommand request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.BranchId);
        if (branch == null)
        {
            _logger.LogWarning("Branch not found with ID {BranchId} when updating status", request.BranchId);
            return false;
        }

        branch.IsActive = request.IsActive;
        branch.UpdateUser = request.UpdateUser;
        branch.UpdateDate = DateTime.Now;

        var rowsAffected = await _branchRepository.UpdateAsync(branch);
        _logger.LogInformation("Branch {BranchId} status updated to IsActive={IsActive}", request.BranchId, request.IsActive);
        return rowsAffected > 0;
    }
}
