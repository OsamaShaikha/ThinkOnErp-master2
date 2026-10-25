using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Controllers;

[ApiController, Route("api/ticket-statuses"), Authorize, TenantScoped]
public sealed class TicketStatusesController(ITicketStatusRepository statuses) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var data = (await statuses.GetAllAsync()).Select(s => new {
            TicketStatusId = s.Id, s.StatusCode, s.StatusNameEn, s.StatusNameLocal, s.IsFinalStatus, s.DisplayOrder
        }).ToList();
        return Ok(ApiResponse<object>.CreateSuccess(data, "DATA_RETRIEVED", 200));
    }
}
