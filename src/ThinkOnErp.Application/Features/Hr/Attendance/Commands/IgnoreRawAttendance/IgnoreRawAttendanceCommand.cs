using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Commands.IgnoreRawAttendance;

public sealed record IgnoreRawAttendanceCommand(long Id, IgnoreRawAttendanceDto Dto, string CurrentUser)
    : IRequest<ApiResponse<RawAttendanceDto>>;

public sealed class IgnoreRawAttendanceCommandValidator : AbstractValidator<IgnoreRawAttendanceCommand>
{
    public IgnoreRawAttendanceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class IgnoreRawAttendanceCommandHandler
    : IRequestHandler<IgnoreRawAttendanceCommand, ApiResponse<RawAttendanceDto>>
{
    private readonly IAttendanceCorrectionRepository _repository;

    public IgnoreRawAttendanceCommandHandler(IAttendanceCorrectionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<RawAttendanceDto>> Handle(
        IgnoreRawAttendanceCommand request,
        CancellationToken cancellationToken)
    {
        var raw = await _repository.IgnoreRawEventAsync(
            request.Id, request.Dto.Reason, request.CurrentUser, cancellationToken);

        return ApiResponse<RawAttendanceDto>.CreateSuccess(AttendanceCommon.MapRawAttendance(raw));
    }
}
