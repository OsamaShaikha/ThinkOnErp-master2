using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Commands.CreateSalaryComponent;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetSalaryComponents;

namespace ThinkOnErp.API.Controllers.Hr;

/// <summary>Catalog of allowances and deductions used in employee salary structures.</summary>
[ApiController]
[Route("api/hr/salary-components")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class SalaryComponentsController : HrControllerBase
{
    private readonly IMediator _mediator;

    public SalaryComponentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HrPermission("hr-salary-components", "view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SalaryComponentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SalaryComponentDto>>>> GetComponents(CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetSalaryComponentsQuery(), cancellationToken));

    [HttpPost]
    [HrPermission("hr-salary-components", "create")]
    [ProducesResponseType(typeof(ApiResponse<SalaryComponentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SalaryComponentDto>>> CreateComponent(
        [FromBody] CreateSalaryComponentDto dto, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new CreateSalaryComponentCommand(dto, CurrentUsername), cancellationToken));
}
