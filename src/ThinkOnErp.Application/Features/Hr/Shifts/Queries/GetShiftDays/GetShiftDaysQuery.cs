using System;
using System.Linq;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftDays;

public sealed record GetShiftDaysQuery(
    string EmployeeCode,
    DateOnly? From = null,
    DateOnly? To = null,
    bool ActiveOnly = false,
    int Page = 1,
    int Size = 50) : IRequest<ApiResponse<ShiftDayPageDto>>;

public sealed class GetShiftDaysQueryHandler : IRequestHandler<GetShiftDaysQuery, ApiResponse<ShiftDayPageDto>>
{
    private readonly IShiftDayRepository _repository;

    public GetShiftDaysQueryHandler(IShiftDayRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<ShiftDayPageDto>> Handle(GetShiftDaysQuery request, CancellationToken ct)
    {
        if (request.Page < 1 || request.Page > 1000000 || request.Size < 1 || request.Size > 100 ||
            (request.From.HasValue && request.To.HasValue && request.From > request.To))
            throw new ArgumentException("Invalid daily roster date range or page.");

        await _repository.EnsureEmployeeAsync(request.EmployeeCode, ct);
        var result = await _repository.ListAsync(
            request.EmployeeCode,
            request.From?.ToDateTime(TimeOnly.MinValue),
            request.To?.ToDateTime(TimeOnly.MinValue),
            request.ActiveOnly,
            request.Page,
            request.Size,
            ct);

        var definitions = await _repository.DefinitionsAsync(
            result.Items.Where(d => d.ShiftCode != null).Select(d => d.ShiftCode!).Distinct().ToArray(),
            ct);

        var pageDto = new ShiftDayPageDto(
            result.Items.Select(d => ShiftDayHelper.Map(d, d.ShiftCode == null ? null : definitions.GetValueOrDefault(d.ShiftCode))).ToArray(),
            result.Total,
            request.Page,
            request.Size);

        return ApiResponse<ShiftDayPageDto>.CreateSuccess(pageDto);
    }
}
