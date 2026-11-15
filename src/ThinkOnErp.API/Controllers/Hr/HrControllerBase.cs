using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;

namespace ThinkOnErp.API.Controllers.Hr;

/// <summary>
/// Shared plumbing for HR controllers that dispatch through MediatR:
/// current-user helpers and a single ApiResponse → HTTP status mapping.
/// </summary>
public abstract class HrControllerBase : ControllerBase
{
    protected const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Username recorded in audit columns.</summary>
    protected string CurrentUsername => User.Identity?.Name ?? "SYSTEM";

    /// <summary>Reads the numeric <c>userId</c> claim; false when missing or not positive.</summary>
    protected bool TryGetCurrentUserId(out long userId)
        => long.TryParse(User.FindFirst("userId")?.Value, out userId) && userId > 0;

    /// <summary>Maps a handler's <see cref="ApiResponse{T}"/> to the matching HTTP result.</summary>
    protected ActionResult<ApiResponse<T>> FromResponse<T>(ApiResponse<T> response)
    {
        if (response.Success) return Ok(response);

        return response.StatusCode switch
        {
            StatusCodes.Status404NotFound => NotFound(response),
            StatusCodes.Status400BadRequest => BadRequest(response),
            _ => StatusCode(response.StatusCode, response)
        };
    }
}
