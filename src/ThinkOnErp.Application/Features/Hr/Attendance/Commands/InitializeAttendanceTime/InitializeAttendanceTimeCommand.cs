using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Commands.InitializeAttendanceTime;

public sealed record InitializeAttendanceTimeCommand(InitializeAttendanceTimeDto Dto, string CurrentUser)
    : IRequest<ApiResponse<AttendanceTimeSettings>>;

public sealed class InitializeAttendanceTimeCommandValidator : AbstractValidator<InitializeAttendanceTimeCommand>
{
    public InitializeAttendanceTimeCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.TimeZoneId).NotEmpty().MaximumLength(100);
    }
}

public sealed class InitializeAttendanceTimeCommandHandler
    : IRequestHandler<InitializeAttendanceTimeCommand, ApiResponse<AttendanceTimeSettings>>
{
    private readonly IAttendanceTimeSettingsRepository _settings;

    public InitializeAttendanceTimeCommandHandler(IAttendanceTimeSettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<ApiResponse<AttendanceTimeSettings>> Handle(InitializeAttendanceTimeCommand request, CancellationToken cancellationToken)
    {
        var result = await _settings.InitializeAsync(request.Dto.TimeZoneId, request.CurrentUser, cancellationToken);
        return ApiResponse<AttendanceTimeSettings>.CreateSuccess(result);
    }
}
