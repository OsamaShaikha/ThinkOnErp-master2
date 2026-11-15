using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Organization.Queries.GetOrganizationReferenceIntegrity;

public sealed record GetOrganizationReferenceIntegrityQuery : IRequest<ApiResponse<OrganizationReferenceIntegritySummary>>;

public sealed class GetOrganizationReferenceIntegrityQueryHandler : IRequestHandler<GetOrganizationReferenceIntegrityQuery, ApiResponse<OrganizationReferenceIntegritySummary>>
{
    private readonly IOrganizationRepository _organization;

    public GetOrganizationReferenceIntegrityQueryHandler(IOrganizationRepository organization)
    {
        _organization = organization;
    }

    public async Task<ApiResponse<OrganizationReferenceIntegritySummary>> Handle(GetOrganizationReferenceIntegrityQuery request, CancellationToken ct)
    {
        var result = await _organization.VerifyReferenceIntegrityAsync(ct);
        return ApiResponse<OrganizationReferenceIntegritySummary>.CreateSuccess(
            result,
            "Organization reference integrity summary retrieved; no data was changed.");
    }
}
