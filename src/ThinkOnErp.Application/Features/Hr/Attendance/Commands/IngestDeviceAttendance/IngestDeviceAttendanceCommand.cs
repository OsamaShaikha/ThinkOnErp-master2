using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using System.Net;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Commands.IngestDeviceAttendance;

public sealed record IngestDeviceAttendanceCommand(DeviceAttendanceDto Dto, string CurrentUser)
    : IRequest<ApiResponse<RawAttendanceDto>>;

public sealed class IngestDeviceAttendanceCommandValidator : AbstractValidator<IngestDeviceAttendanceCommand>
{
    public IngestDeviceAttendanceCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.EmployeeCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dto.OccurredAt).NotEmpty();
    }
}

public sealed class IngestDeviceAttendanceCommandHandler
    : IRequestHandler<IngestDeviceAttendanceCommand, ApiResponse<RawAttendanceDto>>
{
    private readonly IAttendanceCalculationEngine _engine;

    public IngestDeviceAttendanceCommandHandler(IAttendanceCalculationEngine engine)
    {
        _engine = engine;
    }

    public async Task<ApiResponse<RawAttendanceDto>> Handle(
        IngestDeviceAttendanceCommand request,
        CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        try
        {
            var raw = await _engine.IngestPunchAsync(
                dto.EmployeeCode,
                ShiftTimeRules.ParseInstant(dto.OccurredAt).UtcDateTime,
                dto.PunchType?.Trim().ToUpperInvariant() ?? string.Empty,
                dto.DeviceId,
                dto.Source,
                dto.EventId,
                request.CurrentUser,
                cancellationToken);

            return ApiResponse<RawAttendanceDto>.CreateSuccess(AttendanceCommon.MapRawAttendance(raw));
        }
        catch (AttendanceEventConflictException ex)
        {
            return ApiResponse<RawAttendanceDto>.CreateFailure(ex.Message, statusCode: (int)HttpStatusCode.Conflict);
        }
    }
}
