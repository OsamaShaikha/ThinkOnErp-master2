using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.PostBalanceMigration;

public sealed record PostBalanceMigrationCommand(long Id, string User) : IRequest<ApiResponse<LeaveBalanceMigrationDto>>;

public sealed class PostBalanceMigrationCommandValidator : AbstractValidator<PostBalanceMigrationCommand>
{
    public PostBalanceMigrationCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class PostBalanceMigrationCommandHandler : IRequestHandler<PostBalanceMigrationCommand, ApiResponse<LeaveBalanceMigrationDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public PostBalanceMigrationCommandHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveBalanceMigrationDto>> Handle(PostBalanceMigrationCommand request, CancellationToken cancellationToken)
    {
        var migration = await _leaveRepo.ApplyLeaveBalanceMigrationAsync(request.Id, request.User, reverse: false, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-balance migration '{request.Id}' was not found.");

        return ApiResponse<LeaveBalanceMigrationDto>.CreateSuccess(LeaveCommon.MapLeaveBalanceMigration(migration), "Leave-balance migration posted.");
    }
}
