using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Reports;
using ThinkOnErp.Application.DTOs.Inventory.StockMovements;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Stock Ledger, Balances and Valuation API: Manages real-time ledger postings, available-to-promise (ATP), live stock balances, and GL reconciliations.
/// </summary>
[ApiController]
[Route("api/inventory/stock")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public class InvStockController : ControllerBase
{
    private readonly IInvStockLedgerService _stockLedgerService;
    private readonly IInvStockBalanceRepository _stockBalanceRepository;
    private readonly IInvAtpCalculator _atpCalculator;
    private readonly IInvReconciliationService _reconciliationService;

    public InvStockController(
        IInvStockLedgerService stockLedgerService,
        IInvStockBalanceRepository stockBalanceRepository,
        IInvAtpCalculator atpCalculator,
        IInvReconciliationService reconciliationService)
    {
        _stockLedgerService = stockLedgerService;
        _stockBalanceRepository = stockBalanceRepository;
        _atpCalculator = atpCalculator;
        _reconciliationService = reconciliationService;
    }

    /// <summary>
    /// Posts a stock movement directly to the append-only stock ledger with automatic GL accounting voucher generation.
    /// </summary>
    /// <param name="request">Stock movement request payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created ledger entry with updated running balance and valuation.</returns>
    [HttpPost("movements")]
    [ProducesResponseType(typeof(ApiResponse<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PostMovement([FromBody] StockMovementRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _stockLedgerService.PostMovementAsync(request, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.StockMovementPosted;

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieves historical audit movements for a specific item.
    /// </summary>
    /// <param name="itemId">Item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated ledger movements.</returns>
    [HttpGet("movements/{itemId}")]
    [ProducesResponseType(typeof(ApiResponse<List<StockMovementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItemMovements(long itemId, CancellationToken cancellationToken = default)
    {
        var response = await _stockLedgerService.GetMovementsByItemAsync(itemId, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.StockMovementsRetrieved;

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieves real-time stock balance records across warehouses.
    /// </summary>
    /// <param name="itemId">Optional item filter.</param>
    /// <param name="warehouseId">Optional warehouse filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching stock balances with on-hand, reserved, and available quantities.</returns>
    [HttpGet("balances")]
    [ProducesResponseType(typeof(ApiResponse<List<StockBalanceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBalances([FromQuery] long? itemId, [FromQuery] long? warehouseId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Domain.Entities.Inventory.InvStockBalance> balances;

        if (itemId.HasValue && warehouseId.HasValue)
        {
            var single = await _stockBalanceRepository.GetAsync(itemId.Value, warehouseId.Value, null, cancellationToken);
            balances = single != null ? new[] { single } : Array.Empty<Domain.Entities.Inventory.InvStockBalance>();
        }
        else if (itemId.HasValue)
        {
            balances = await _stockBalanceRepository.GetByItemAsync(itemId.Value, cancellationToken);
        }
        else if (warehouseId.HasValue)
        {
            balances = await _stockBalanceRepository.GetByWarehouseAsync(warehouseId.Value, cancellationToken);
        }
        else
        {
            balances = await _stockBalanceRepository.GetAllAsync(cancellationToken);
        }

        var list = balances.Select(b => new StockBalanceDto
        {
            ItemId = b.ItemId,
            WarehouseId = b.WarehouseId,
            BinId = b.BinId,
            OnHandQty = b.OnHandQty,
            ReservedQty = b.ReservedQty,
            AvailableQty = b.OnHandQty - b.ReservedQty,
            OnOrderQty = b.OnOrderQty,
            AvgCost = b.AvgCost,
            TotalValue = b.OnHandQty * b.AvgCost
        }).ToList();

        return Ok(ApiResponse<List<StockBalanceDto>>.CreateSuccess(list, ResponseCodes.StockBalancesRetrieved));
    }

    /// <summary>
    /// Retrieves total inventory valuation summary report.
    /// </summary>
    [HttpGet("valuation")]
    [ProducesResponseType(typeof(ApiResponse<List<StockBalanceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetValuation([FromQuery] long? warehouseId, CancellationToken cancellationToken)
    {
        var balances = warehouseId.HasValue 
            ? await _stockBalanceRepository.GetByWarehouseAsync(warehouseId.Value, cancellationToken)
            : await _stockBalanceRepository.GetAllAsync(cancellationToken);
            
        var list = balances.Select(b => new StockBalanceDto
        {
            ItemId = b.ItemId,
            WarehouseId = b.WarehouseId,
            BinId = b.BinId,
            OnHandQty = b.OnHandQty,
            ReservedQty = b.ReservedQty,
            AvailableQty = b.OnHandQty - b.ReservedQty,
            OnOrderQty = b.OnOrderQty,
            AvgCost = b.AvgCost,
            TotalValue = b.OnHandQty * b.AvgCost
        }).ToList();

        return Ok(ApiResponse<List<StockBalanceDto>>.CreateSuccess(list, "Inventory valuation retrieved successfully"));
    }

    /// <summary>
    /// Computes the dynamic Available-To-Promise (ATP) quantity for customer commitments.
    /// </summary>
    /// <param name="itemId">Item identifier.</param>
    /// <param name="warehouseId">Optional warehouse scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The calculated ATP breakdown.</returns>
    [HttpGet("atp/{itemId}")]
    [ProducesResponseType(typeof(ApiResponse<AtpResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAtp(long itemId, [FromQuery] long? warehouseId, CancellationToken cancellationToken)
    {
        var response = await _atpCalculator.CalculateAtpAsync(itemId, warehouseId, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.AtpCalculated;

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Executes a reconciliation audit comparing the perpetual inventory physical valuation with GL Control Accounts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Reconciliation report with variance analysis.</returns>
    [HttpGet("reconciliation")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunReconciliation(CancellationToken cancellationToken)
    {
        var response = await _reconciliationService.RunReconciliationCheckAsync(cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.StockReconciliationCompleted;

        return StatusCode(response.StatusCode, response);
    }
}
