using System.Collections.Generic;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Organization.Queries.GetPositionByCode;

public sealed record GetPositionByCodeQuery(string Code) : IRequest<ApiResponse<HrPosition>>;

public sealed class GetPositionByCodeQueryHandler : IRequestHandler<GetPositionByCodeQuery, ApiResponse<HrPosition>>
{
    private readonly IOrganizationRepository _organization;

    public GetPositionByCodeQueryHandler(IOrganizationRepository organization)
    {
        _organization = organization;
    }

    public async Task<ApiResponse<HrPosition>> Handle(GetPositionByCodeQuery request, CancellationToken ct)
    {
        var normalizedCode = OrganizationCatalogRules.Code(request.Code);
        var result = await _organization.PositionAsync(normalizedCode, ct);
        if (result == null)
            return ApiResponse<HrPosition>.CreateFailure("Position not found.", statusCode: 404);

        return ApiResponse<HrPosition>.CreateSuccess(result);
    }
}
