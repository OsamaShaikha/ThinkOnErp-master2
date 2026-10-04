using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Parties;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Customers API: Fast customer lookup and customer selection for cashier registers.
/// </summary>
[ApiController]
[Route("api/pos/customers")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosCustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public PosCustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>
    /// Retrieves active customers formatted for POS cash register with fast search by name, phone, code, or tax number.
    /// </summary>
    /// <param name="search">Keyword search matching customer code, name, phone, or tax number.</param>
    /// <param name="branchId">Optional filter by branch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active POS customers.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosCustomerDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosCustomerDto>>>> GetPosCustomers(
        [FromQuery] string? search = null,
        [FromQuery] long? branchId = null,
        CancellationToken cancellationToken = default)
    {
        // Enforce isActive = true for POS customers
        var filter = new PartyFilterDto
        {
            SearchTerm = search,
            BranchId = branchId,
            IsActive = true
        };

        var customers = await _customerService.GetAllAsync(filter, cancellationToken);

        var posCustomers = customers.Select(c => new PosCustomerDto
        {
            Id = c.Id,
            CustomerCode = c.CustomerCode,
            NameLocal = c.NameLocal,
            NameEn = c.NameEn,
            Phone = c.Phone,
            Email = c.Email,
            TaxNumber = c.TaxNumber,
            Address = c.Address,
            CreditLimit = c.CreditLimit,
            PaymentTermsDays = c.PaymentTermsDays,
            BranchId = c.BranchId,
            IsActive = c.IsActive
        }).ToList();

        return Ok(ApiResponse<IReadOnlyList<PosCustomerDto>>.CreateSuccess(
            posCustomers,
            ResponseCodes.DataRetrieved,
            200));
    }

    /// <summary>
    /// Retrieves a specific active customer by code for POS.
    /// </summary>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(ApiResponse<PosCustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosCustomerDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosCustomerDto>>> GetByCode(
        string code,
        CancellationToken cancellationToken = default)
    {
        var customer = await _customerService.GetByCodeAsync(code, cancellationToken);
        if (customer == null || !customer.IsActive)
        {
            return NotFound(ApiResponse<PosCustomerDto>.CreateFailure("Customer not found or inactive for POS.", null, 404));
        }

        var posCustomer = new PosCustomerDto
        {
            Id = customer.Id,
            CustomerCode = customer.CustomerCode,
            NameLocal = customer.NameLocal,
            NameEn = customer.NameEn,
            Phone = customer.Phone,
            Email = customer.Email,
            TaxNumber = customer.TaxNumber,
            Address = customer.Address,
            CreditLimit = customer.CreditLimit,
            PaymentTermsDays = customer.PaymentTermsDays,
            BranchId = customer.BranchId,
            IsActive = customer.IsActive
        };

        return Ok(ApiResponse<PosCustomerDto>.CreateSuccess(
            posCustomer,
            ResponseCodes.DataRetrieved,
            200));
    }

    /// <summary>
    /// Quick creation of a customer directly from POS cashier terminal.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PosCustomerDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<PosCustomerDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosCustomerDto>>> CreateCustomer(
        [FromBody] CreateCustomerDto dto,
        CancellationToken cancellationToken = default)
    {
        var username = User.Identity?.Name ?? "system";
        var created = await _customerService.CreateAsync(dto, username, cancellationToken);

        var posCustomer = new PosCustomerDto
        {
            Id = created.Id,
            CustomerCode = created.CustomerCode,
            NameLocal = created.NameLocal,
            NameEn = created.NameEn,
            Phone = created.Phone,
            Email = created.Email,
            TaxNumber = created.TaxNumber,
            Address = created.Address,
            CreditLimit = created.CreditLimit,
            PaymentTermsDays = created.PaymentTermsDays,
            BranchId = created.BranchId,
            IsActive = created.IsActive
        };

        return CreatedAtAction(
            nameof(GetByCode),
            new { code = posCustomer.CustomerCode },
            ApiResponse<PosCustomerDto>.CreateSuccess(
                posCustomer,
                ResponseCodes.RecordCreated,
                201));
    }
}
