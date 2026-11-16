using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.PostLeaveCashout;

public sealed record PostLeaveCashoutCommand(long Id, string User) : IRequest<ApiResponse<LeaveCashoutSettlementDto>>;

public sealed class PostLeaveCashoutCommandValidator : AbstractValidator<PostLeaveCashoutCommand>
{
    public PostLeaveCashoutCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class PostLeaveCashoutCommandHandler : IRequestHandler<PostLeaveCashoutCommand, ApiResponse<LeaveCashoutSettlementDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public PostLeaveCashoutCommandHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveCashoutSettlementDto>> Handle(PostLeaveCashoutCommand request, CancellationToken cancellationToken)
    {
        var settlement = await _leaveRepo.PostLeaveCashoutSettlementAsync(request.Id, request.User, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave cashout settlement '{request.Id}' was not found in the current HR scope.");

        return ApiResponse<LeaveCashoutSettlementDto>.CreateSuccess(LeaveCommon.MapLeaveCashoutSettlement(settlement), "Leave cashout payroll adjustments queued for review.");
    }
}
