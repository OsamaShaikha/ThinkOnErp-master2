using System.Collections.Generic;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftDefinitionByCode;

public sealed record GetShiftDefinitionByCodeQuery(string Code) : IRequest<ApiResponse<ShiftDefinitionDto>>;

public sealed class GetShiftDefinitionByCodeQueryHandler : IRequestHandler<GetShiftDefinitionByCodeQuery, ApiResponse<ShiftDefinitionDto>>
{
    private readonly IShiftDefinitionRepository _repository;

    public GetShiftDefinitionByCodeQueryHandler(IShiftDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<ShiftDefinitionDto>> Handle(GetShiftDefinitionByCodeQuery request, CancellationToken ct)
    {
        var code = ShiftDefinitionHelper.Code(request.Code);
        var shift = await _repository.GetAsync(code, ct);
        if (shift == null)
            return ApiResponse<ShiftDefinitionDto>.CreateFailure("Shift not found.", statusCode: 404);

        var referenced = await _repository.ReferencedCodesAsync(new[] { code }, ct);
        var dto = ShiftDefinitionHelper.Map(shift, referenced.Contains(code));
        return ApiResponse<ShiftDefinitionDto>.CreateSuccess(dto);
    }
}
