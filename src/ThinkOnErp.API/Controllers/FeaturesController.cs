using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Feature;
using ThinkOnErp.Application.Features.Features.Commands.CreateFeature;
using ThinkOnErp.Application.Features.Features.Commands.UpdateFeature;
using ThinkOnErp.Application.Features.Features.Commands.DeleteFeature;
using ThinkOnErp.Application.Features.Features.Queries.GetAllFeatures;
using ThinkOnErp.Application.Features.Features.Queries.GetFeatureById;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/features")]
[Authorize(Policy = "SuperAdminOnly")]
public class FeaturesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<FeaturesController> _logger;

    public FeaturesController(IMediator mediator, ILogger<FeaturesController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<FeatureDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<FeatureDto>>>> GetAllFeatures()
    {
        try
        {
            _logger.LogInformation("Retrieving all features");
            var features = await _mediator.Send(new GetAllFeaturesQuery());
            return Ok(ApiResponse<List<FeatureDto>>.CreateSuccess(features, "Features retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all features");
            throw;
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<FeatureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FeatureDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FeatureDto>>> GetFeatureById(long id)
    {
        try
        {
            _logger.LogInformation("Retrieving feature with ID: {FeatureId}", id);
            var feature = await _mediator.Send(new GetFeatureByIdQuery { FeatureId = id });
            if (feature == null)
                return NotFound(ApiResponse<FeatureDto>.CreateFailure("No feature found with the specified identifier", statusCode: 404));
            return Ok(ApiResponse<FeatureDto>.CreateSuccess(feature, "Feature retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving feature with ID: {FeatureId}", id);
            throw;
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<long>>> CreateFeature(
        [FromForm] CreateFeatureFormDto dto,
        IFormFile? icon)
    {
        try
        {
            var command = new CreateFeatureCommand
            {
                FeatureCode = dto.FeatureCode,
                FeatureName = dto.FeatureName,
                FeatureNameE = dto.FeatureNameE,
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

            _logger.LogInformation("Creating new feature: {FeatureCode}", command.FeatureCode);
            var featureId = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetFeatureById), new { id = featureId },
                ApiResponse<long>.CreateSuccess(featureId, "Feature created successfully", 201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating feature: {FeatureCode}", dto.FeatureCode);
            throw;
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<long>>> UpdateFeature(
        long id,
        [FromForm] UpdateFeatureFormDto dto,
        IFormFile? icon)
    {
        try
        {
            var command = new UpdateFeatureCommand
            {
                FeatureId = id,
                FeatureCode = dto.FeatureCode,
                FeatureName = dto.FeatureName,
                FeatureNameE = dto.FeatureNameE,
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

            _logger.LogInformation("Updating feature with ID: {FeatureId}", id);
            var rowsAffected = await _mediator.Send(command);
            if (rowsAffected == 0)
                return NotFound(ApiResponse<long>.CreateFailure("No feature found with the specified identifier", statusCode: 404));
            return Ok(ApiResponse<long>.CreateSuccess(rowsAffected, "Feature updated successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating feature with ID: {FeatureId}", id);
            throw;
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<long>>> DeleteFeature(long id)
    {
        try
        {
            var command = new DeleteFeatureCommand
            {
                FeatureId = id,
                UpdateUser = User.Identity?.Name ?? "system"
            };
            _logger.LogInformation("Deleting feature with ID: {FeatureId}", id);
            var rowsAffected = await _mediator.Send(command);
            if (rowsAffected == 0)
                return NotFound(ApiResponse<long>.CreateFailure("No feature found with the specified identifier", statusCode: 404));
            return Ok(ApiResponse<long>.CreateSuccess(rowsAffected, "Feature deleted successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting feature with ID: {FeatureId}", id);
            throw;
        }
    }
}
