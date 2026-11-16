using System.Collections.Generic;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Organization.Queries.GetDepartmentByCode;

public sealed record GetDepartmentByCodeQuery(string Code) : IRequest<ApiResponse<HrDepartment>>;

public sealed class GetDepartmentByCodeQueryHandler : IRequestHandler<GetDepartmentByCodeQuery, ApiResponse<HrDepartment>>
{
    private readonly IOrganizationRepository _organization;

    public GetDepartmentByCodeQueryHandler(IOrganizationRepository organization)
    {
        _organization = organization;
    }

    public async Task<ApiResponse<HrDepartment>> Handle(GetDepartmentByCodeQuery request, CancellationToken ct)
    {
        var normalizedCode = OrganizationCatalogRules.Code(request.Code);
        var result = await _organization.DepartmentAsync(normalizedCode, ct);
        if (result == null)
            return ApiResponse<HrDepartment>.CreateFailure("Department not found.", statusCode: 404);

        return ApiResponse<HrDepartment>.CreateSuccess(result);
    }
}
