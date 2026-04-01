using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Currency;
using ThinkOnErp.Application.Features.Currencies.Commands.CreateCurrency;
using ThinkOnErp.Application.Features.Currencies.Commands.UpdateCurrency;
using ThinkOnErp.Application.Features.Currencies.Commands.DeleteCurrency;
using ThinkOnErp.Application.Features.Currencies.Queries.GetAllCurrencies;
using ThinkOnErp.Application.Features.Currencies.Queries.GetCurrencyById;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for currency management operations.
/// Handles CRUD operations for system currencies with appropriate authorization.
/// </summary>
[ApiController]
[Route("api/currencies")]
[Authorize]
public class CurrencyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CurrencyController> _logger;

    /// <summary>
    /// Initializes a new instance of the CurrencyController class.
    /// </summary>
    /// <param name="mediator">MediatR instance for sending commands and queries</param>
    /// <param name="logger">Logger for controller operations</param>
    public CurrencyController(IMediator mediator, ILogger<CurrencyController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active currencies from the system.
    /// Requires authentication.
    /// </summary>
    /// <returns>ApiResponse containing list of CurrencyDto objects</returns>
    /// <response code="200">Returns the list of all active currencies</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CurrencyDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<CurrencyDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<CurrencyDto>>>> GetAllCurrencies()
    {
        try
        {
            _logger.LogInformation("Retrieving all currencies");

            var query = new GetAllCurrenciesQuery();
            var currencies = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} currencies", currencies.Count);

            return Ok(ApiResponse<List<CurrencyDto>>.CreateSuccess(
                currencies,
                "Currencies retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all currencies");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific currency by its ID.
    /// Requires authentication.
    /// </summary>
    /// <param name="id">Unique identifier of the currency</param>
    /// <returns>ApiResponse containing CurrencyDto object</returns>
    /// <response code="200">Returns the requested currency</response>
    /// <response code="404">Currency not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<CurrencyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CurrencyDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<CurrencyDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CurrencyDto>>> GetCurrencyById(decimal id)
    {
        try
        {
            _logger.LogInformation("Retrieving currency with ID: {CurrencyId}", id);

            var query = new GetCurrencyByIdQuery { RowId = id };
            var currency = await _mediator.Send(query);

            if (currency == null)
            {
                _logger.LogWarning("Currency not found with ID: {CurrencyId}", id);
                return NotFound(ApiResponse<CurrencyDto>.CreateFailure(
                    "No currency found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved currency with ID: {CurrencyId}", id);

            return Ok(ApiResponse<CurrencyDto>.CreateSuccess(
                currency,
                "Currency retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving currency with ID: {CurrencyId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new currency in the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="command">Command containing currency creation data</param>
    /// <returns>ApiResponse containing the newly created currency's ID</returns>
    /// <response code="201">Currency created successfully</response>
    /// <response code="400">Validation errors in the request</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<decimal>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<decimal>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<decimal>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<decimal>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<decimal>>> CreateCurrency([FromBody] CreateCurrencyCommand command)
    {
        try
        {
            _logger.LogInformation("Creating new currency: {CurrencyDesc}", command.RowDesc);

            var currencyId = await _mediator.Send(command);

            _logger.LogInformation("Currency created successfully with ID: {CurrencyId}", currencyId);

            return CreatedAtAction(
                nameof(GetCurrencyById),
                new { id = currencyId },
                ApiResponse<decimal>.CreateSuccess(
                    currencyId,
                    "Currency created successfully",
                    201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating currency: {CurrencyDesc}", command.RowDesc);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing currency in the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the currency to update</param>
    /// <param name="command">Command containing updated currency data</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Currency updated successfully</response>
    /// <response code="400">Validation errors or ID mismatch</response>
    /// <response code="404">Currency not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<int>>> UpdateCurrency(decimal id, [FromBody] UpdateCurrencyCommand command)
    {
        try
        {
            if (id != command.RowId)
            {
                _logger.LogWarning("Currency ID mismatch: URL ID {UrlId} vs Command ID {CommandId}", id, command.RowId);
                return BadRequest(ApiResponse<int>.CreateFailure(
                    "Currency ID in URL does not match the ID in the request body",
                    statusCode: 400));
            }

            _logger.LogInformation("Updating currency with ID: {CurrencyId}", id);

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Currency not found for update with ID: {CurrencyId}", id);
                return NotFound(ApiResponse<int>.CreateFailure(
                    "No currency found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Currency updated successfully with ID: {CurrencyId}", id);

            return Ok(ApiResponse<int>.CreateSuccess(
                rowsAffected,
                "Currency updated successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating currency with ID: {CurrencyId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes (soft delete) a currency from the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the currency to delete</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Currency deleted successfully</response>
    /// <response code="404">Currency not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<int>>> DeleteCurrency(decimal id)
    {
        try
        {
            _logger.LogInformation("Deleting currency with ID: {CurrencyId}", id);

            var command = new DeleteCurrencyCommand { RowId = id };
            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Currency not found for deletion with ID: {CurrencyId}", id);
                return NotFound(ApiResponse<int>.CreateFailure(
                    "No currency found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Currency deleted successfully with ID: {CurrencyId}", id);

            return Ok(ApiResponse<int>.CreateSuccess(
                rowsAffected,
                "Currency deleted successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting currency with ID: {CurrencyId}", id);
            throw;
        }
    }
}
