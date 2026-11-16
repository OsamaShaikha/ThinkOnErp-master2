using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Application.Common;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController, Route("api/hr/payroll-posting"), TenantScoped, Authorize(Policy = "AdminOnly")]
public sealed class PayrollPostingConfigurationsController(IHrPayrollPostingService service) : ControllerBase
{
    [HttpGet("{branchId:long}"), HrPermission("hr-payroll-posting", "view")]
    public async Task<IActionResult> Get(long branchId, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(branchId, cancellationToken));

    [HttpPut("{branchId:long}"), HrPermission("hr-payroll-posting", "configure")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Save(long branchId, PayrollPostingConfiguration configuration, CancellationToken cancellationToken)
    {
        return Task.FromResult<IActionResult>(Conflict(ApiResponse<object>.CreateFailure(
            "Payroll accounting configuration is owned by Accounting. The independent handoff workflow must be enabled first.",
            new() { "HR_ACCOUNTING_HANDOFF_REQUIRED" }, StatusCodes.Status409Conflict)));
    }
}
