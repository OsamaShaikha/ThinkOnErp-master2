using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.EndOfService.Commands.CreateEndOfServicePolicy;
using ThinkOnErp.Application.Features.Hr.EndOfService.Commands.DeactivateEndOfServicePolicy;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetEndOfServicePolicies;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetEndOfServicePolicyById;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/end-of-service/policies")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class EndOfServicePoliciesController : HrControllerBase
{
    private readonly IMediator _mediator;

    public EndOfServicePoliciesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HrPermission("hr-end-of-service", "configure")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EndOfServicePolicy>>>> GetPolicies(CancellationToken ct)
        => FromResponse(await _mediator.Send(new GetEndOfServicePoliciesQuery(GetTenant().CompanyId), ct));

    [HttpGet("{id:long}")]
    [HrPermission("hr-end-of-service", "configure")]
    public async Task<ActionResult<ApiResponse<EndOfServicePolicy>>> GetPolicy(long id, CancellationToken ct)
        => FromResponse(await _mediator.Send(new GetEndOfServicePolicyByIdQuery(id, GetTenant().CompanyId), ct));

    [HttpPost]
    [HrPermission("hr-end-of-service", "configure")]
    public async Task<ActionResult<ApiResponse<EndOfServicePolicy>>> CreatePolicy(
        [FromBody] CreateEndOfServicePolicyDto request, CancellationToken ct)
        => FromResponse(await _mediator.Send(new CreateEndOfServicePolicyCommand(request, GetTenant().CompanyId, CurrentUsername), ct));

    [HttpPost("{id:long}/deactivate")]
    [HrPermission("hr-end-of-service", "configure")]
    public async Task<ActionResult<ApiResponse<EndOfServicePolicy>>> Deactivate(long id, CancellationToken ct)
        => FromResponse(await _mediator.Send(new DeactivateEndOfServicePolicyCommand(id, GetTenant().CompanyId, CurrentUsername), ct));

    private TenantRequestContext GetTenant()
        => HttpContext.Items.TryGetValue(TenantRequestContext.HttpContextItemKey, out var value) && value is TenantRequestContext tenant && tenant.CompanyId > 0
            ? tenant
            : throw new InvalidOperationException("Trusted tenant context is required.");
}
