using System;
using System.Collections.Generic;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftDayById;

public sealed record GetShiftDayByIdQuery(string EmployeeCode, long Id)
    : IRequest<ApiResponse<ShiftDayDto>>;

public sealed class GetShiftDayByIdQueryHandler : IRequestHandler<GetShiftDayByIdQuery, ApiResponse<ShiftDayDto>>
{
    private readonly IShiftDayRepository _repository;

    public GetShiftDayByIdQueryHandler(IShiftDayRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<ShiftDayDto>> Handle(GetShiftDayByIdQuery request, CancellationToken ct)
    {
        await _repository.EnsureEmployeeAsync(request.EmployeeCode, ct);
        var day = await _repository.GetAsync(request.EmployeeCode, request.Id, ct);
        if (day == null)
            return ApiResponse<ShiftDayDto>.CreateFailure("Daily roster entry not found.", statusCode: 404);

        var definitions = await _repository.DefinitionsAsync(day.ShiftCode == null ? Array.Empty<string>() : new[] { day.ShiftCode }, ct);
        var dto = ShiftDayHelper.Map(day, day.ShiftCode == null ? null : definitions.GetValueOrDefault(day.ShiftCode));
        return ApiResponse<ShiftDayDto>.CreateSuccess(dto);
    }
}
