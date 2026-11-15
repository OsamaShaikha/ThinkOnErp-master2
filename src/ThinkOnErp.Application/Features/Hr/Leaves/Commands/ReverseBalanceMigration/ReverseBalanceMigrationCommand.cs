using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.ReverseBalanceMigration;

public sealed record ReverseBalanceMigrationCommand(long Id, string User) : IRequest<ApiResponse<LeaveBalanceMigrationDto>>;

public sealed class ReverseBalanceMigrationCommandValidator : AbstractValidator<ReverseBalanceMigrationCommand>
{
    public ReverseBalanceMigrationCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class ReverseBalanceMigrationCommandHandler : IRequestHandler<ReverseBalanceMigrationCommand, ApiResponse<LeaveBalanceMigrationDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public ReverseBalanceMigrationCommandHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveBalanceMigrationDto>> Handle(ReverseBalanceMigrationCommand request, CancellationToken cancellationToken)
    {
        var migration = await _leaveRepo.ApplyLeaveBalanceMigrationAsync(request.Id, request.User, reverse: true, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-balance migration '{request.Id}' was not found.");

        return ApiResponse<LeaveBalanceMigrationDto>.CreateSuccess(LeaveCommon.MapLeaveBalanceMigration(migration), "Leave-balance migration reversed.");
    }
}
