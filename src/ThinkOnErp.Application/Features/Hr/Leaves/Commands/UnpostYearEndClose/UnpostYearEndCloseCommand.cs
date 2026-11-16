using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.UnpostYearEndClose;

public sealed record UnpostYearEndCloseCommand(long Id, string User) : IRequest<ApiResponse<LeaveYearCloseDto>>;

public sealed class UnpostYearEndCloseCommandValidator : AbstractValidator<UnpostYearEndCloseCommand>
{
    public UnpostYearEndCloseCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class UnpostYearEndCloseCommandHandler : IRequestHandler<UnpostYearEndCloseCommand, ApiResponse<LeaveYearCloseDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public UnpostYearEndCloseCommandHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveYearCloseDto>> Handle(UnpostYearEndCloseCommand request, CancellationToken cancellationToken)
    {
        var close = await _leaveRepo.UnpostLeaveYearCloseAsync(request.Id, request.User, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-year close '{request.Id}' was not found.");

        return ApiResponse<LeaveYearCloseDto>.CreateSuccess(LeaveCommon.MapLeaveYearClose(close), "Leave-year close unposted.");
    }
}
