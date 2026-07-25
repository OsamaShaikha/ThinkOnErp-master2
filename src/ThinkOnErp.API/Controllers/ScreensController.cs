using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Feature;
using ThinkOnErp.Application.DTOs.Screen;
using ThinkOnErp.Application.DTOs.ScreenFeature;
using ThinkOnErp.Application.Features.ScreenFeatures.Commands.AssignFeaturesToScreen;
using ThinkOnErp.Application.Features.ScreenFeatures.Commands.RemoveFeatureFromScreen;
using ThinkOnErp.Application.Features.ScreenFeatures.Queries.GetFeaturesByScreenId;
using ThinkOnErp.Application.Features.Screens.Commands.CreateScreen;
using ThinkOnErp.Application.Features.Screens.Commands.UpdateScreen;
using ThinkOnErp.Application.Features.Screens.Commands.DeleteScreen;
using ThinkOnErp.Application.Features.Screens.Queries.GetAllScreens;
using ThinkOnErp.Application.Features.Screens.Queries.GetScreenById;
using ThinkOnErp.Application.Features.Screens.Queries.GetScreensBySystemId;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/screens")]
[Authorize(Policy = "AdminOnly")]
public class ScreensController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ScreensController> _logger;

    public ScreensController(IMediator mediator, ILogger<ScreensController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ScreenDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ScreenDto>>>> GetAllScreens()
    {
        try
        {
            _logger.LogInformation("Retrieving all screens");
            var screens = await _mediator.Send(new GetAllScreensQuery());
            return Ok(ApiResponse<List<ScreenDto>>.CreateSuccess(screens, "Screens retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all screens");
            throw;
        }
    }

    [HttpGet("bysystem/{systemId}")]
    [ProducesResponseType(typeof(ApiResponse<List<ScreenDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ScreenDto>>>> GetScreensBySystemId(long systemId)
    {
        try
        {
            _logger.LogInformation("Retrieving screens for system ID: {SystemId}", systemId);
            var screens = await _mediator.Send(new GetScreensBySystemIdQuery { SystemId = systemId });
            return Ok(ApiResponse<List<ScreenDto>>.CreateSuccess(screens, "Screens retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving screens for system ID: {SystemId}", systemId);
            throw;
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ScreenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ScreenDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ScreenDto>>> GetScreenById(long id)
    {
        try
        {
            _logger.LogInformation("Retrieving screen with ID: {ScreenId}", id);
            var screen = await _mediator.Send(new GetScreenByIdQuery { ScreenId = id });
            if (screen == null)
                return NotFound(ApiResponse<ScreenDto>.CreateFailure("No screen found with the specified identifier", statusCode: 404));
            return Ok(ApiResponse<ScreenDto>.CreateSuccess(screen, "Screen retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving screen with ID: {ScreenId}", id);
            throw;
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<long>>> CreateScreen(
        [FromForm] CreateScreenFormDto dto,
        IFormFile? icon)
    {
        try
        {
            var command = new CreateScreenCommand
            {
                SystemId = dto.SystemId,
                ParentScreenId = dto.ParentScreenId,
                ScreenCode = dto.ScreenCode,
                ScreenName = dto.ScreenName,
                ScreenNameE = dto.ScreenNameE,
                Description = dto.Description,
                DescriptionE = dto.DescriptionE,
                DisplayOrder = dto.DisplayOrder,
                CreationUser = User.Identity?.Name ?? "system"
            };

            if (icon != null)
            {
                using var ms = new MemoryStream();
                await icon.CopyToAsync(ms);
                command.IconFile = ms.ToArray();
                command.IconFileName = icon.FileName;
            }

            _logger.LogInformation("Creating new screen: {ScreenCode}", command.ScreenCode);
            var screenId = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetScreenById), new { id = screenId },
                ApiResponse<long>.CreateSuccess(screenId, "Screen created successfully", 201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating screen: {ScreenCode}", dto.ScreenCode);
            throw;
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<long>>> UpdateScreen(
        long id,
        [FromForm] UpdateScreenFormDto dto,
        IFormFile? icon)
    {
        try
        {
            var command = new UpdateScreenCommand
            {
                ScreenId = id,
                SystemId = dto.SystemId,
                ParentScreenId = dto.ParentScreenId,
                ScreenCode = dto.ScreenCode,
                ScreenName = dto.ScreenName,
                ScreenNameE = dto.ScreenNameE,
                Description = dto.Description,
                DescriptionE = dto.DescriptionE,
                DisplayOrder = dto.DisplayOrder,
                UpdateUser = User.Identity?.Name ?? "system"
            };

            if (icon != null)
            {
                using var ms = new MemoryStream();
                await icon.CopyToAsync(ms);
                command.IconFile = ms.ToArray();
                command.IconFileName = icon.FileName;
            }

            _logger.LogInformation("Updating screen with ID: {ScreenId}", id);
            var rowsAffected = await _mediator.Send(command);
            if (rowsAffected == 0)
                return NotFound(ApiResponse<long>.CreateFailure("No screen found with the specified identifier", statusCode: 404));
            return Ok(ApiResponse<long>.CreateSuccess(rowsAffected, "Screen updated successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating screen with ID: {ScreenId}", id);
            throw;
        }
    }

    [HttpGet("{screenId}/features")]
    [ProducesResponseType(typeof(ApiResponse<List<FeatureDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<FeatureDto>>>> GetFeaturesByScreenId(long screenId)
    {
        try
        {
            _logger.LogInformation("Retrieving features for screen ID: {ScreenId}", screenId);
            var features = await _mediator.Send(new GetFeaturesByScreenIdQuery { ScreenId = screenId });
            return Ok(ApiResponse<List<FeatureDto>>.CreateSuccess(features, "Features retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving features for screen ID: {ScreenId}", screenId);
            throw;
        }
    }

    [HttpPost("{screenId}/features")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<bool>>> AssignFeaturesToScreen(
        long screenId,
        [FromBody] AssignFeaturesToScreenDto dto)
    {
        try
        {
            var command = new AssignFeaturesToScreenCommand
            {
                ScreenId = screenId,
                FeatureIds = dto.FeatureIds
            };
            _logger.LogInformation("Assigning features to screen ID: {ScreenId}", screenId);
            await _mediator.Send(command);
            return Ok(ApiResponse<bool>.CreateSuccess(true, "Features assigned successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning features to screen ID: {ScreenId}", screenId);
            throw;
        }
    }

    [HttpDelete("{screenId}/features/{featureId}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveFeatureFromScreen(long screenId, long featureId)
    {
        try
        {
            var command = new RemoveFeatureFromScreenCommand
            {
                ScreenId = screenId,
                FeatureId = featureId
            };
            _logger.LogInformation("Removing feature {FeatureId} from screen ID: {ScreenId}", featureId, screenId);
            await _mediator.Send(command);
            return Ok(ApiResponse<bool>.CreateSuccess(true, "Feature removed from screen successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing feature {FeatureId} from screen ID: {ScreenId}", featureId, screenId);
            throw;
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<long>>> DeleteScreen(long id)
    {
        try
        {
            var command = new DeleteScreenCommand
            {
                ScreenId = id,
                UpdateUser = User.Identity?.Name ?? "system"
            };
            _logger.LogInformation("Deleting screen with ID: {ScreenId}", id);
            var rowsAffected = await _mediator.Send(command);
            if (rowsAffected == 0)
                return NotFound(ApiResponse<long>.CreateFailure("No screen found with the specified identifier", statusCode: 404));
            return Ok(ApiResponse<long>.CreateSuccess(rowsAffected, "Screen deleted successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting screen with ID: {ScreenId}", id);
            throw;
        }
    }
}
