using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/salary-components")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class SalaryComponentsController : ControllerBase
{
    private readonly IEmployeeService _employeeService;
    private readonly ILogger<SalaryComponentsController> _logger;

    public SalaryComponentsController(
        IEmployeeService employeeService,
        ILogger<SalaryComponentsController> logger)
    {
        _employeeService = employeeService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SalaryComponentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SalaryComponentDto>>>> GetComponents(
        CancellationToken cancellationToken)
    {
        var result = await _employeeService.GetSalaryComponentsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SalaryComponentDto>>.CreateSuccess(result, "Salary components retrieved successfully."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SalaryComponentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SalaryComponentDto>>> CreateComponent(
        [FromBody] CreateSalaryComponentDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _employeeService.CreateSalaryComponentAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<SalaryComponentDto>.CreateSuccess(result, "Salary component created successfully."));
    }
}
