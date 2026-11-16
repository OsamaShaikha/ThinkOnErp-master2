using System.Collections.Generic;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Organization.Queries.GetDepartments;

public sealed record GetDepartmentsQuery : IRequest<ApiResponse<IReadOnlyList<HrDepartment>>>;

public sealed class GetDepartmentsQueryHandler : IRequestHandler<GetDepartmentsQuery, ApiResponse<IReadOnlyList<HrDepartment>>>
{
    private readonly IOrganizationRepository _organization;

    public GetDepartmentsQueryHandler(IOrganizationRepository organization)
    {
        _organization = organization;
    }

    public async Task<ApiResponse<IReadOnlyList<HrDepartment>>> Handle(GetDepartmentsQuery request, CancellationToken ct)
    {
        var result = await _organization.DepartmentsAsync(ct);
        return ApiResponse<IReadOnlyList<HrDepartment>>.CreateSuccess(result);
    }
}
