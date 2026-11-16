using System;
using System.Linq;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftAssignments;

public sealed record GetShiftAssignmentsQuery(
    string EmployeeCode,
    DateOnly? From = null,
    DateOnly? To = null,
    bool ActiveOnly = false,
    int Page = 1,
    int Size = 50) : IRequest<ApiResponse<ShiftAssignmentPageDto>>;

public sealed class GetShiftAssignmentsQueryHandler : IRequestHandler<GetShiftAssignmentsQuery, ApiResponse<ShiftAssignmentPageDto>>
{
    private readonly IShiftAssignmentRepository _repository;

    public GetShiftAssignmentsQueryHandler(IShiftAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<ShiftAssignmentPageDto>> Handle(GetShiftAssignmentsQuery request, CancellationToken ct)
    {
        if (request.Page < 1 || request.Page > 1000000 || request.Size < 1 || request.Size > 100 ||
            (request.From.HasValue && request.To.HasValue && request.From > request.To))
            throw new ArgumentException("Invalid assignment date range or page.");

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
            result.Items.Select(a => a.ShiftCode).Distinct().ToArray(),
            ct);

        var pageDto = new ShiftAssignmentPageDto(
            result.Items.Select(a => ShiftAssignmentHelper.Map(a, definitions.GetValueOrDefault(a.ShiftCode))).ToList(),
            result.Total,
            request.Page,
            request.Size);

        return ApiResponse<ShiftAssignmentPageDto>.CreateSuccess(pageDto);
    }
}
