using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.ReverseLeaveCashout;

public sealed record ReverseLeaveCashoutCommand(
    long Id,
    ReverseLeaveCashoutSettlementDto Dto,
    string User) : IRequest<ApiResponse<LeaveCashoutSettlementDto>>;

public sealed class ReverseLeaveCashoutCommandValidator : AbstractValidator<ReverseLeaveCashoutCommand>
{
    public ReverseLeaveCashoutCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class ReverseLeaveCashoutCommandHandler : IRequestHandler<ReverseLeaveCashoutCommand, ApiResponse<LeaveCashoutSettlementDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public ReverseLeaveCashoutCommandHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveCashoutSettlementDto>> Handle(ReverseLeaveCashoutCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        var reason = dto.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            throw new ArgumentException("A reversal reason of 1 to 1000 characters is required.", nameof(dto.Reason));

        var settlement = await _leaveRepo.ReverseLeaveCashoutSettlementAsync(request.Id, reason, request.User, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave cashout settlement '{request.Id}' was not found in the current HR scope.");

        return ApiResponse<LeaveCashoutSettlementDto>.CreateSuccess(LeaveCommon.MapLeaveCashoutSettlement(settlement), "Leave cashout settlement reversed.");
    }
}
