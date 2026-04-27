using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.SavedSearch;
using ThinkOnErp.Application.Features.SavedSearches.Commands.CreateSavedSearch;
using ThinkOnErp.Application.Features.SavedSearches.Queries.GetSavedSearches;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for saved search management operations.
/// Handles CRUD operations for user-defined saved searches.
/// Requirements: 8.6, 8.11, 19.9
/// </summary>
[ApiController]
[Route("api/saved-searches")]
[Authorize]
public class SavedSearchesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SavedSearchesController> _logger;

    public SavedSearchesController(IMediator mediator, ILogger<SavedSearchesController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all saved searches accessible to the current user (private + public).
    /// Requires authentication.
    /// </summary>
    /// <returns>ApiResponse containing list of SavedSearchDto objects</returns>
    /// <response code="200">Returns the list of saved searches</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<SavedSearchDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<SavedSearchDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<SavedSearchDto>>>> GetSavedSearches()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("Retrieving saved searches for user {UserId}", userId);

            var query = new GetSavedSearchesQuery { UserId = userId };
            var result = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} saved searches for user {UserId}", 
                result.Count, userId);

            return Ok(ApiResponse<List<SavedSearchDto>>.CreateSuccess(
                result,
                "Saved searches retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving saved searches");
            throw;
        }
    }

    /// <summary>
    /// Creates a new saved search for the current user.
    /// Requires authentication.
    /// </summary>
    /// <param name="dto">Saved search creation data</param>
    /// <returns>ApiResponse containing the new saved search ID</returns>
    /// <response code="201">Saved search created successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="401">User is not authenticated</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateSavedSearch([FromBody] CreateSavedSearchDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var userName = GetCurrentUserName();

            _logger.LogInformation("Creating saved search '{SearchName}' for user {UserId}", 
                dto.SearchName, userId);

            var command = new CreateSavedSearchCommand
            {
                UserId = userId,
                SearchName = dto.SearchName,
                SearchDescription = dto.SearchDescription,
                SearchCriteria = dto.SearchCriteria,
                IsPublic = dto.IsPublic,
                IsDefault = dto.IsDefault,
                CreationUser = userName
            };

            var newId = await _mediator.Send(command);

            _logger.LogInformation("Successfully created saved search with ID {SavedSearchId}", newId);

            return CreatedAtAction(
                nameof(GetSavedSearches),
                new { id = newId },
                ApiResponse<Int64>.CreateSuccess(
                    newId,
                    "Saved search created successfully",
                    201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating saved search");
            throw;
        }
    }

    /// <summary>
    /// Gets the current user's ID from JWT claims.
    /// </summary>
    private Int64 GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("userId")?.Value  // Match the claim name from JwtTokenService
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Int64.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        return userId;
    }

    /// <summary>
    /// Gets the current user's name from JWT claims.
    /// </summary>
    private string GetCurrentUserName()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value 
            ?? User.FindFirst("UserName")?.Value 
            ?? "Unknown";
    }
}
