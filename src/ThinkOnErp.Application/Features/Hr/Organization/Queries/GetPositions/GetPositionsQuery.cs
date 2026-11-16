using System.Collections.Generic;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Organization.Queries.GetPositions;

public sealed record GetPositionsQuery(string? DepartmentCode = null) : IRequest<ApiResponse<IReadOnlyList<HrPosition>>>;

public sealed class GetPositionsQueryHandler : IRequestHandler<GetPositionsQuery, ApiResponse<IReadOnlyList<HrPosition>>>
{
    private readonly IOrganizationRepository _organization;

    public GetPositionsQueryHandler(IOrganizationRepository organization)
    {
        _organization = organization;
    }

    public async Task<ApiResponse<IReadOnlyList<HrPosition>>> Handle(GetPositionsQuery request, CancellationToken ct)
    {
        var result = await _organization.PositionsAsync(request.DepartmentCode, ct);
        return ApiResponse<IReadOnlyList<HrPosition>>.CreateSuccess(result);
    }
}
