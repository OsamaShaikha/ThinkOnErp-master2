using System.Collections.Generic;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.Application.Features.Pos.Sync.Commands.PushOfflineOrders;

public record PushOfflineOrdersCommand(long BranchId, List<CreatePosOrderDto> Orders, string Username) : IRequest<ApiResponse<PushOfflineOrdersResultDto>>;

public class PushOfflineOrdersCommandValidator : AbstractValidator<PushOfflineOrdersCommand>
{
    public PushOfflineOrdersCommandValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0).WithMessage("BranchId is required");
        RuleFor(x => x.Orders).NotEmpty().WithMessage("Orders list cannot be empty");
    }
}

public class PushOfflineOrdersCommandHandler : IRequestHandler<PushOfflineOrdersCommand, ApiResponse<PushOfflineOrdersResultDto>>
{
    private readonly IPosSyncService _syncService;

    public PushOfflineOrdersCommandHandler(IPosSyncService syncService)
    {
        _syncService = syncService;
    }

    public async Task<ApiResponse<PushOfflineOrdersResultDto>> Handle(PushOfflineOrdersCommand request, CancellationToken cancellationToken)
    {
        return await _syncService.PushOfflineOrdersAsync(request.BranchId, request.Orders, request.Username, cancellationToken);
    }
}
