using System;
using System.Linq;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftDefinitions;

public sealed record GetShiftDefinitionsQuery(bool ActiveOnly = false, string? Search = null, int Page = 1, int Size = 50)
    : IRequest<ApiResponse<ShiftDefinitionPageDto>>;

public sealed class GetShiftDefinitionsQueryHandler : IRequestHandler<GetShiftDefinitionsQuery, ApiResponse<ShiftDefinitionPageDto>>
{
    private readonly IShiftDefinitionRepository _repository;

    public GetShiftDefinitionsQueryHandler(IShiftDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<ShiftDefinitionPageDto>> Handle(GetShiftDefinitionsQuery request, CancellationToken ct)
    {
        if (request.Page < 1 || request.Page > 1000000 || request.Size < 1 || request.Size > 100 || request.Search?.Length > 200)
            throw new ArgumentException("Invalid shift page, size or search length.");

        var result = await _repository.ListAsync(request.ActiveOnly, request.Search?.Trim(), request.Page, request.Size, ct);
        var used = await _repository.ReferencedCodesAsync(result.Items.Select(s => s.ShiftCode).ToArray(), ct);
        var pageDto = new ShiftDefinitionPageDto(
            result.Items.Select(s => ShiftDefinitionHelper.Map(s, used.Contains(s.ShiftCode))).ToList(),
            result.Total,
            request.Page,
            request.Size);

        return ApiResponse<ShiftDefinitionPageDto>.CreateSuccess(pageDto);
    }
}
