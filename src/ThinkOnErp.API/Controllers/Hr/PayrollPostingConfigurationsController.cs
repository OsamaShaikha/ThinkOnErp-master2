using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollPostingConfig;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController, Route("api/hr/payroll-posting"), TenantScoped, Authorize(Policy = "AdminOnly")]
public sealed class PayrollPostingConfigurationsController : HrControllerBase
{
    private readonly IMediator? _mediator;
    private readonly IHrPayrollPostingService? _service;

    [ActivatorUtilitiesConstructor]
    public PayrollPostingConfigurationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Obsolete("Use IMediator constructor. This constructor remains for backwards-compatible test instantiation.")]
    public PayrollPostingConfigurationsController(IHrPayrollPostingService service)
    {
        _service = service;
    }

    [HttpGet("{branchId:long}"), HrPermission("hr-payroll-posting", "view")]
    public async Task<IActionResult> Get(long branchId, CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new GetPayrollPostingConfigQuery(branchId), cancellationToken);
            return Ok(response.Data);
        }

        return Ok(await _service!.GetAsync(branchId, cancellationToken));
    }

    [HttpPut("{branchId:long}"), HrPermission("hr-payroll-posting", "configure")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Save(long branchId, PayrollPostingConfiguration configuration, CancellationToken cancellationToken)
    {
        return Task.FromResult<IActionResult>(Conflict(ApiResponse<object>.CreateFailure(
            "Payroll accounting configuration is owned by Accounting. The independent handoff workflow must be enabled first.",
            new() { "HR_ACCOUNTING_HANDOFF_REQUIRED" }, StatusCodes.Status409Conflict)));
    }
}
