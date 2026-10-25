using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.API.Controllers;

[ApiController, Route("api/ticket-delivery"), Authorize(Policy = "SuperAdminOnly")]
public sealed class TicketDeliveryController(TicketEmailQueue queue) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status()
    {
        var status = queue.GetStatus();
        return Ok(new { status.Pending, status.Failed, status.Sent, status.Configured });
    }
}
