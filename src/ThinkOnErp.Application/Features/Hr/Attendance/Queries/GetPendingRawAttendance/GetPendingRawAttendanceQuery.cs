using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetPendingRawAttendance;

public sealed record GetPendingRawAttendanceQuery(int Size = 100)
    : IRequest<ApiResponse<IReadOnlyList<RawAttendanceDto>>>;

public sealed class GetPendingRawAttendanceQueryValidator : AbstractValidator<GetPendingRawAttendanceQuery>
{
    public GetPendingRawAttendanceQueryValidator()
    {
        RuleFor(x => x.Size).InclusiveBetween(1, 100);
    }
}

public sealed class GetPendingRawAttendanceQueryHandler
    : IRequestHandler<GetPendingRawAttendanceQuery, ApiResponse<IReadOnlyList<RawAttendanceDto>>>
{
    private readonly IAttendanceCorrectionRepository _repository;

    public GetPendingRawAttendanceQueryHandler(IAttendanceCorrectionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<IReadOnlyList<RawAttendanceDto>>> Handle(
        GetPendingRawAttendanceQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Size < 1 || request.Size > 100)
            throw new ArgumentException("Queue size must be 1..100.");

        var rows = await _repository.GetUnprocessedRawPunchesAsync(request.Size, cancellationToken);
        var mapped = rows.Select(AttendanceCommon.MapRawAttendance).ToArray();
        return ApiResponse<IReadOnlyList<RawAttendanceDto>>.CreateSuccess(mapped);
    }
}
