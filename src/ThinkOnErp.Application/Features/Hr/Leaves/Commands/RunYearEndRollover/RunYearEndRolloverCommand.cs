using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateYearEndCloseDraft;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.PostYearEndClose;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.RunYearEndRollover;

public sealed record RunYearEndRolloverCommand(YearEndRolloverDto Dto, string User) : IRequest<ApiResponse<LeaveRolloverResultDto>>;

public sealed class RunYearEndRolloverCommandValidator : AbstractValidator<RunYearEndRolloverCommand>
{
    public RunYearEndRolloverCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.FromYear).GreaterThan(0);
        RuleFor(x => x.Dto.ToYear).Equal(x => x.Dto.FromYear + 1);
    }
}

public sealed class RunYearEndRolloverCommandHandler : IRequestHandler<RunYearEndRolloverCommand, ApiResponse<LeaveRolloverResultDto>>
{
    private readonly IMediator _mediator;

    public RunYearEndRolloverCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<ApiResponse<LeaveRolloverResultDto>> Handle(RunYearEndRolloverCommand request, CancellationToken cancellationToken)
    {
        var draftResponse = await _mediator.Send(new CreateYearEndCloseDraftCommand(request.Dto, request.User), cancellationToken);
        var close = draftResponse.Data ?? throw new InvalidOperationException("Failed to prepare year-end close draft.");

        if (!string.Equals(close.Status, "POSTED", StringComparison.OrdinalIgnoreCase))
        {
            var postResponse = await _mediator.Send(new PostYearEndCloseCommand(close.Id, request.User), cancellationToken);
            close = postResponse.Data ?? throw new InvalidOperationException("Failed to post year-end close.");
        }

        var result = new LeaveRolloverResultDto(
            close.Lines.Count,
            close.Lines.Count,
            close.TotalCarriedDays,
            close.TotalExpiredDays,
            close.Lines.Select(l => $"{l.EmployeeCode} [{l.LeaveTypeCode}]: Unused {l.UnusedDays:F4} => carried {l.CarriedDays:F4}, expired {l.ExpiredDays:F4}").ToList());

        return ApiResponse<LeaveRolloverResultDto>.CreateSuccess(result, "Year-end leave rollover completed successfully.");
    }
}
