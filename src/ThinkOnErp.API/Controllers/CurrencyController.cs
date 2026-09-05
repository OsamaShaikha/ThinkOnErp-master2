using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Currency;
using ThinkOnErp.Application.Features.Currencies.Commands.CreateCurrency;
using ThinkOnErp.Application.Features.Currencies.Commands.DeleteCurrency;
using ThinkOnErp.Application.Features.Currencies.Commands.UpdateCurrency;
using ThinkOnErp.Application.Features.Currencies.Queries.GetAllCurrencies;
using ThinkOnErp.Application.Features.Currencies.Queries.GetCurrencyById;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for currency management operations.
/// Handles CRUD operations for system currencies with appropriate authorization.
/// </summary>
[ApiController]
[Route("api/currencies")]
[TenantScoped]
[Authorize]
public class CurrencyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CurrencyController> _logger;

    public CurrencyController(IMediator mediator, ILogger<CurrencyController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all currencies");
            throw;
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<CurrencyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CurrencyDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<CurrencyDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CurrencyDto>>> GetCurrencyById(Int64 id)
    {
        try
        {
            _logger.LogInformation("Retrieving currency with ID: {CurrencyId}", id);

            var query = new GetCurrencyByIdQuery { CurrencyId = id };
            var currency = await _mediator.Send(query);

            if (currency == null)
            {
                _logger.LogWarning("Currency not found with ID: {CurrencyId}", id);
                return NotFound(ApiResponse<CurrencyDto>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved currency with ID: {CurrencyId}", id);

            return Ok(ApiResponse<CurrencyDto>.CreateSuccess(
                currency,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving currency with ID: {CurrencyId}", id);
            throw;
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateCurrency([FromBody] CreateCurrencyCommand command)
    {
        try
        {
            command.CreationUser = User.Identity?.Name ?? "system";
            _logger.LogInformation("Creating new currency: {CurrencyDesc}", command.CurrencyNameLocal);

            var currencyId = await _mediator.Send(command);

            _logger.LogInformation("Currency created successfully with ID: {CurrencyId}", currencyId);

            return CreatedAtAction(
                nameof(GetCurrencyById),
                new { id = currencyId },
                ApiResponse<Int64>.CreateSuccess(
                    currencyId,
                    ResponseCodes.RecordCreated,
                    201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating currency: {CurrencyDesc}", command.CurrencyNameLocal);
            throw;
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateCurrency(Int64 id, [FromBody] UpdateCurrencyCommand command)
    {
        try
        {
            command.CurrencyId = id;
            command.UpdateUser = User.Identity?.Name ?? "system";

            _logger.LogInformation("Updating currency with ID: {CurrencyId}", id);

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Currency not found for update with ID: {CurrencyId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("Currency updated successfully with ID: {CurrencyId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.RecordUpdated,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating currency with ID: {CurrencyId}", id);
            throw;
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<int>>> DeleteCurrency(Int64 id)
    {
        try
        {
            _logger.LogInformation("Deleting currency with ID: {CurrencyId}", id);

            var command = new DeleteCurrencyCommand { CurrencyId = id };
            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Currency not found for deletion with ID: {CurrencyId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("Currency deleted successfully with ID: {CurrencyId}", id);

            return Ok(ApiResponse<int>.CreateSuccess(
                (int)rowsAffected,
                ResponseCodes.RecordDeleted,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting currency with ID: {CurrencyId}", id);
            throw;
        }
    }
}
