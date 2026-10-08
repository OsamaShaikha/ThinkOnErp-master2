using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController, Route("api/hr/payroll-posting"), TenantScoped, Authorize(Policy = "AdminOnly")]
public sealed class PayrollPostingConfigurationsController(IHrPayrollPostingService service) : ControllerBase
{
    [HttpGet("{branchId:long}"), HrPermission("hr-payroll-posting", "view")]
    public async Task<IActionResult> Get(long branchId, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(branchId, cancellationToken));

    [HttpPut("{branchId:long}"), HrPermission("hr-payroll-posting", "configure")]
    public async Task<IActionResult> Save(long branchId, PayrollPostingConfiguration configuration, CancellationToken cancellationToken)
    {
        if (branchId != configuration.BranchId) return BadRequest("Branch identifiers must match.");
        await service.SaveAsync(configuration, cancellationToken);
        return Ok(configuration);
    }
}
