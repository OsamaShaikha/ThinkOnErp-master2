using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/short-leaves")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class ShortLeavesController : ControllerBase
{
    private readonly IShortLeaveService _service;

    public ShortLeavesController(IShortLeaveService service)
    {
        _service = service;
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
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "ANONYMOUS";
        var result = await _service.SubmitRequestAsync(dto, user, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:long}/approve")]
    [HrPermission("hr-leaves", "approve")]
    public async Task<IActionResult> ApproveRequest(long id, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "ANONYMOUS";
        var result = await _service.ApproveRequestAsync(id, user, ct);
        return Ok(result);
    }

    [HttpPost("{id:long}/reject")]
    [HrPermission("hr-leaves", "approve")]
    public async Task<IActionResult> RejectRequest(long id, [FromBody] RejectShortLeaveDto dto, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "ANONYMOUS";
        var result = await _service.RejectRequestAsync(id, dto?.Reason ?? string.Empty, user, ct);
        return Ok(result);
    }

    [HttpPost("{id:long}/cancel")]
    [HrPermission("hr-leaves", "reverse")]
    public async Task<IActionResult> CancelRequest(long id, [FromBody] CancelShortLeaveDto dto, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "ANONYMOUS";
        var result = await _service.CancelRequestAsync(id, dto?.Reason ?? string.Empty, user, ct);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [HrPermission("hr-leaves", "view")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
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
        var results = await _service.GetRequestsAsync(ResolveCompanyId(companyId), branchId, status, date, ct);
        return Ok(results);
    }

    [HttpGet("policy")]
    [HrPermission("hr-leaves", "view")]
    public async Task<IActionResult> GetPolicy([FromQuery] long? companyId = null, CancellationToken ct = default)
    {
        var result = await _service.GetPolicyAsync(ResolveCompanyId(companyId), ct);
        return Ok(result);
    }

    [HttpPut("policy")]
    [HrPermission("hr-leaves", "edit")]
    public async Task<IActionResult> SavePolicy([FromBody] ShortLeavePolicyDto dto, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "ANONYMOUS";
        var result = await _service.SavePolicyAsync(dto, user, ct);
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

