using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.ApproveShortLeave;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CancelShortLeave;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.RejectShortLeave;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.SaveShortLeavePolicy;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.SubmitShortLeave;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetShortLeaveById;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetShortLeavePolicy;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetShortLeaveRequests;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/short-leaves")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class ShortLeavesController : HrControllerBase
{
    private readonly IMediator _mediator;

    public ShortLeavesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [HrPermission("hr-leaves", "create")]
    public async Task<IActionResult> SubmitRequest([FromBody] CreateShortLeaveDto dto, CancellationToken ct)
    {
        if (!HttpContext.Items.TryGetValue(TenantRequestContext.HttpContextItemKey, out var value) ||
            value is not TenantRequestContext tenant || tenant.CompanyId <= 0)
            return Forbid();

        // The DTO's zero default means the client omitted CompanyId. Always persist
        // against the tenant resolved for this request, and reject explicit mismatches.
        if (dto.CompanyId != 0 && dto.CompanyId != tenant.CompanyId)
            return Forbid();

        dto = dto with { CompanyId = tenant.CompanyId };
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? CurrentUsername;
        var result = await _mediator.Send(new SubmitShortLeaveCommand(dto, user), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:long}/approve")]
    [HrPermission("hr-leaves", "approve")]
    public async Task<IActionResult> ApproveRequest(long id, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? CurrentUsername;
        var result = await _mediator.Send(new ApproveShortLeaveCommand(id, user), ct);
        return Ok(result);
    }

    [HttpPost("{id:long}/reject")]
    [HrPermission("hr-leaves", "approve")]
    public async Task<IActionResult> RejectRequest(long id, [FromBody] RejectShortLeaveDto dto, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? CurrentUsername;
        var result = await _mediator.Send(new RejectShortLeaveCommand(id, dto?.Reason ?? string.Empty, user), ct);
        return Ok(result);
    }

    [HttpPost("{id:long}/cancel")]
    [HrPermission("hr-leaves", "reverse")]
    public async Task<IActionResult> CancelRequest(long id, [FromBody] CancelShortLeaveDto dto, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? CurrentUsername;
        var result = await _mediator.Send(new CancelShortLeaveCommand(id, dto?.Reason ?? string.Empty, user), ct);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [HrPermission("hr-leaves", "view")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetShortLeaveByIdQuery(id), ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet]
    [HrPermission("hr-leaves", "view")]
    public async Task<IActionResult> GetRequests(
        [FromQuery] long? companyId = null,
        [FromQuery] long? branchId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? date = null,
        CancellationToken ct = default)
    {
        var results = await _mediator.Send(new GetShortLeaveRequestsQuery(ResolveCompanyId(companyId), branchId, status, date), ct);
        return Ok(results);
    }

    [HttpGet("policy")]
    [HrPermission("hr-leaves", "view")]
    public async Task<IActionResult> GetPolicy([FromQuery] long? companyId = null, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetShortLeavePolicyQuery(ResolveCompanyId(companyId)), ct);
        return Ok(result);
    }

    [HttpPut("policy")]
    [HrPermission("hr-leaves", "edit")]
    public async Task<IActionResult> SavePolicy([FromBody] ShortLeavePolicyDto dto, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? CurrentUsername;
        var result = await _mediator.Send(new SaveShortLeavePolicyCommand(dto, user), ct);
        return Ok(result);
    }

    private long ResolveCompanyId(long? companyId)
    {
        if (companyId.HasValue)
            return companyId.Value;

        if (HttpContext.Items.TryGetValue(TenantRequestContext.HttpContextItemKey, out var value) &&
            value is TenantRequestContext tenant)
            return tenant.CompanyId;

        throw new InvalidOperationException("The tenant context is required to resolve the short leave company.");
    }
}
