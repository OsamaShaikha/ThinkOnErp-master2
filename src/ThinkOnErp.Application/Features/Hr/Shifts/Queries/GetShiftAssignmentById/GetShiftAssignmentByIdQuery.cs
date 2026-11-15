using System.Collections.Generic;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftAssignmentById;

public sealed record GetShiftAssignmentByIdQuery(string EmployeeCode, long Id)
    : IRequest<ApiResponse<ShiftAssignmentDto>>;

public sealed class GetShiftAssignmentByIdQueryHandler : IRequestHandler<GetShiftAssignmentByIdQuery, ApiResponse<ShiftAssignmentDto>>
{
    private readonly IShiftAssignmentRepository _repository;

    public GetShiftAssignmentByIdQueryHandler(IShiftAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<ShiftAssignmentDto>> Handle(GetShiftAssignmentByIdQuery request, CancellationToken ct)
    {
        await _repository.EnsureEmployeeAsync(request.EmployeeCode, ct);
        var assignment = await _repository.GetAsync(request.EmployeeCode, request.Id, ct);
        if (assignment == null)
            return ApiResponse<ShiftAssignmentDto>.CreateFailure("Shift assignment not found.", statusCode: 404);

        var definitions = await _repository.DefinitionsAsync(new[] { assignment.ShiftCode }, ct);
        var dto = ShiftAssignmentHelper.Map(assignment, definitions.GetValueOrDefault(assignment.ShiftCode));
        return ApiResponse<ShiftAssignmentDto>.CreateSuccess(dto);
    }
}
