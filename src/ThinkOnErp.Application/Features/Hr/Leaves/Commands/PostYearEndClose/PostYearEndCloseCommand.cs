using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.PostYearEndClose;

public sealed record PostYearEndCloseCommand(long Id, string User) : IRequest<ApiResponse<LeaveYearCloseDto>>;

public sealed class PostYearEndCloseCommandValidator : AbstractValidator<PostYearEndCloseCommand>
{
    public PostYearEndCloseCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class PostYearEndCloseCommandHandler : IRequestHandler<PostYearEndCloseCommand, ApiResponse<LeaveYearCloseDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public PostYearEndCloseCommandHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveYearCloseDto>> Handle(PostYearEndCloseCommand request, CancellationToken cancellationToken)
    {
        var close = await _leaveRepo.PostLeaveYearCloseAsync(request.Id, request.User, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-year close '{request.Id}' was not found.");

        return ApiResponse<LeaveYearCloseDto>.CreateSuccess(LeaveCommon.MapLeaveYearClose(close), "Leave-year close posted.");
    }
}
