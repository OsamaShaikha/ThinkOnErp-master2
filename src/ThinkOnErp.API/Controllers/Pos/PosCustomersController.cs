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
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Accounting;

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
    private readonly ICustomerRepository _customerRepository;
    private readonly IArSubledgerRepository _arSubledgerRepository;
    private readonly IReceiptPaymentVoucherService _receiptVoucherService;
    private readonly IGlFiscalPeriodRepository _fiscalPeriodRepository;
    private readonly IPosShiftService _shiftService;

    public PosCustomersController(
        ICustomerService customerService,
        ICustomerRepository customerRepository,
        IArSubledgerRepository arSubledgerRepository,
        IReceiptPaymentVoucherService receiptVoucherService,
        IGlFiscalPeriodRepository fiscalPeriodRepository,
        IPosShiftService shiftService)
    {
        _customerService = customerService;
        _customerRepository = customerRepository;
        _arSubledgerRepository = arSubledgerRepository;
        _receiptVoucherService = receiptVoucherService;
        _fiscalPeriodRepository = fiscalPeriodRepository;
        _shiftService = shiftService;
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
        var codes = customers.Select(c => c.CustomerCode).ToList();
        var balances = await _arSubledgerRepository.GetCustomersBalancesAsync(codes, cancellationToken);

        var posCustomers = customers.Select(c =>
        {
            var currentBal = balances.GetValueOrDefault(c.CustomerCode, 0m);
            return new PosCustomerDto
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
                CurrentBalance = currentBal,
                PaymentTermsDays = c.PaymentTermsDays,
                BranchId = c.BranchId,
                IsActive = c.IsActive
            };
        }).ToList();

        return Ok(ApiResponse<IReadOnlyList<PosCustomerDto>>.CreateSuccess(
            posCustomers,
            ResponseCodes.DataRetrieved,
            200));
    }

    /// <summary>
    /// Retrieves a specific active customer by code for POS including current balance and available credit.
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

        var balance = await _arSubledgerRepository.GetCustomerBalanceAsync(customer.CustomerCode, cancellationToken);

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
            CurrentBalance = balance,
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
    /// Retrieves fast customer balance and credit availability for POS cashier registers.
    /// Accepts either customer code (e.g. CUST-001) or customer numeric ID.
    /// </summary>
    [HttpGet("{codeOrId}/balance")]
    [ProducesResponseType(typeof(ApiResponse<PosCustomerBalanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosCustomerBalanceDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosCustomerBalanceDto>>> GetCustomerBalance(
        string codeOrId,
        CancellationToken cancellationToken = default)
    {
        Customer? customer = await _customerRepository.GetByCodeAsync(codeOrId, cancellationToken);
        if (customer == null && long.TryParse(codeOrId, out var id))
        {
            customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        }

        if (customer == null || !customer.IsActive)
        {
            return NotFound(ApiResponse<PosCustomerBalanceDto>.CreateFailure("Customer not found or inactive for POS.", null, 404));
        }

        var currentBalance = await _arSubledgerRepository.GetCustomerBalanceAsync(customer.CustomerCode, cancellationToken);
        var openInvoices = await _arSubledgerRepository.GetOpenInvoicesAsync(customer.CustomerCode, cancellationToken);
        var openInvoicesBalance = openInvoices.Sum(i => i.LocalOpenAmount);
        var overdueCount = openInvoices.Count(i => i.DueDate.HasValue && i.DueDate.Value < DateTime.UtcNow && i.LocalOpenAmount > 0);

        var balanceDto = new PosCustomerBalanceDto
        {
            CustomerId = customer.Id,
            CustomerCode = customer.CustomerCode,
            CustomerName = !string.IsNullOrWhiteSpace(customer.NameLocal) ? customer.NameLocal : customer.NameEn,
            Phone = customer.Phone,
            CurrentBalance = currentBalance,
            OpenInvoicesBalance = openInvoicesBalance,
            CreditLimit = customer.CreditLimit,
            PaymentTermsDays = customer.PaymentTermsDays,
            OverdueInvoicesCount = overdueCount
        };

        return Ok(ApiResponse<PosCustomerBalanceDto>.CreateSuccess(
            balanceDto,
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
            CurrentBalance = 0m,
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

    /// <summary>
    /// Retrieves all unpaid / open invoices for a specific customer in POS.
    /// Used by cashier to choose a specific invoice or view outstanding invoices before settling.
    /// </summary>
    [HttpGet("{codeOrId}/open-invoices")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosCustomerOpenInvoiceDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosCustomerOpenInvoiceDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosCustomerOpenInvoiceDto>>>> GetCustomerOpenInvoices(
        string codeOrId,
        CancellationToken cancellationToken = default)
    {
        Customer? customer = await _customerRepository.GetByCodeAsync(codeOrId, cancellationToken);
        if (customer == null && long.TryParse(codeOrId, out var id))
        {
            customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        }

        if (customer == null || !customer.IsActive)
        {
            return NotFound(ApiResponse<IReadOnlyList<PosCustomerOpenInvoiceDto>>.CreateFailure("Customer not found or inactive for POS.", null, 404));
        }

        var openInvoices = await _arSubledgerRepository.GetOpenInvoicesAsync(customer.CustomerCode, cancellationToken);

        var list = openInvoices.Select(i => new PosCustomerOpenInvoiceDto
        {
            TransactionId = i.Id,
            ReferenceNo = i.ReferenceNo ?? $"INV-{i.Id}",
            Description = i.Description,
            InvoiceDate = i.TransactionDate,
            DueDate = i.DueDate,
            OriginalAmount = i.Amount,
            RemainingAmount = i.OpenAmount
        }).ToList();

        return Ok(ApiResponse<IReadOnlyList<PosCustomerOpenInvoiceDto>>.CreateSuccess(
            list,
            ResponseCodes.DataRetrieved,
            200));
    }

    /// <summary>
    /// Settles customer debt from POS register for:
    /// 1. A single specific invoice (SpecificInvoice)
    /// 2. All open invoices (AllInvoices)
    /// 3. A lump sum on account with auto FIFO distribution (OnAccount)
    /// </summary>
    [HttpPost("{codeOrId}/settle")]
    [ProducesResponseType(typeof(ApiResponse<PosCustomerSettlementResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosCustomerSettlementResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PosCustomerSettlementResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosCustomerSettlementResultDto>>> SettleCustomerDebt(
        string codeOrId,
        [FromBody] PosCustomerSettlementRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var username = User.Identity?.Name ?? "system";

        Customer? customer = await _customerRepository.GetByCodeAsync(codeOrId, cancellationToken);
        if (customer == null && long.TryParse(codeOrId, out var id))
        {
            customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        }

        if (customer == null || !customer.IsActive)
        {
            return NotFound(ApiResponse<PosCustomerSettlementResultDto>.CreateFailure("Customer not found or inactive for POS.", null, 404));
        }

        var openInvoices = await _arSubledgerRepository.GetOpenInvoicesAsync(customer.CustomerCode, cancellationToken);
        var previousBalance = await _arSubledgerRepository.GetCustomerBalanceAsync(customer.CustomerCode, cancellationToken);

        var invoicesToSettle = new List<ReceiptInvoiceSettlementItemDto>();
        decimal totalPaymentAmount = request.Amount;

        switch (request.SettlementType)
        {
            case PosCustomerSettlementType.AllInvoices:
            {
                var totalOpen = openInvoices.Sum(i => i.OpenAmount);
                if (totalOpen <= 0)
                {
                    return BadRequest(ApiResponse<PosCustomerSettlementResultDto>.CreateFailure("لا توجد أي فواتير مفتوحة غير مسددة للعميل.", null, 400));
                }

                if (totalPaymentAmount <= 0)
                {
                    totalPaymentAmount = totalOpen;
                }

                var remainingToAllocate = totalPaymentAmount;
                foreach (var inv in openInvoices.OrderBy(i => i.TransactionDate))
                {
                    if (remainingToAllocate <= 0) break;
                    var alloc = Math.Min(remainingToAllocate, inv.OpenAmount);
                    invoicesToSettle.Add(new ReceiptInvoiceSettlementItemDto
                    {
                        InvoiceTransactionId = inv.Id,
                        AmountToApply = alloc,
                        Notes = $"سداد كامل/جزئي لجميع الفواتير - فاتورة {inv.ReferenceNo ?? inv.Id.ToString()}"
                    });
                    remainingToAllocate -= alloc;
                }
                break;
            }

            case PosCustomerSettlementType.SpecificInvoice:
            {
                var targetId = request.SpecificInvoiceTransactionId;
                if (!targetId.HasValue && request.Invoices != null && request.Invoices.Any())
                {
                    targetId = request.Invoices.First().InvoiceTransactionId;
                }

                if (!targetId.HasValue)
                {
                    return BadRequest(ApiResponse<PosCustomerSettlementResultDto>.CreateFailure("يجب تحديد رقم الفاتورة المراد سدادها (SpecificInvoiceTransactionId).", null, 400));
                }

                var targetInvoice = openInvoices.FirstOrDefault(i => i.Id == targetId.Value);
                if (targetInvoice == null)
                {
                    return BadRequest(ApiResponse<PosCustomerSettlementResultDto>.CreateFailure($"الفاتورة رقم #{targetId.Value} غير موجودة ضمن فواتير العميل المفتوحة أو تم سدادها بالكامل مسبقاً.", null, 400));
                }

                if (totalPaymentAmount <= 0)
                {
                    totalPaymentAmount = targetInvoice.OpenAmount;
                }
                else if (totalPaymentAmount > targetInvoice.OpenAmount)
                {
                    return BadRequest(ApiResponse<PosCustomerSettlementResultDto>.CreateFailure($"مبلغ السداد ({totalPaymentAmount:N2}) يتجاوز المبلغ المتبقي للفاتورة ({targetInvoice.OpenAmount:N2}).", null, 400));
                }

                invoicesToSettle.Add(new ReceiptInvoiceSettlementItemDto
                {
                    InvoiceTransactionId = targetInvoice.Id,
                    AmountToApply = totalPaymentAmount,
                    Notes = $"سداد الفاتورة رقم {targetInvoice.ReferenceNo ?? targetInvoice.Id.ToString()}"
                });
                break;
            }

            case PosCustomerSettlementType.OnAccount:
            default:
            {
                if (totalPaymentAmount <= 0)
                {
                    return BadRequest(ApiResponse<PosCustomerSettlementResultDto>.CreateFailure("يجب تحديد مبلغ السداد (Amount) أكبر من الصفر للدفع على الحساب.", null, 400));
                }

                var remainingToAllocate = totalPaymentAmount;
                foreach (var inv in openInvoices.OrderBy(i => i.TransactionDate))
                {
                    if (remainingToAllocate <= 0) break;
                    var alloc = Math.Min(remainingToAllocate, inv.OpenAmount);
                    invoicesToSettle.Add(new ReceiptInvoiceSettlementItemDto
                    {
                        InvoiceTransactionId = inv.Id,
                        AmountToApply = alloc,
                        Notes = $"توزيع تلقائي (FIFO) على فاتورة {inv.ReferenceNo ?? inv.Id.ToString()}"
                    });
                    remainingToAllocate -= alloc;
                }
                break;
            }
        }

        if (totalPaymentAmount <= 0)
        {
            return BadRequest(ApiResponse<PosCustomerSettlementResultDto>.CreateFailure("مبلغ السداد يجب أن يكون أكبر من الصفر.", null, 400));
        }

        var currentPeriod = await _fiscalPeriodRepository.GetPeriodByDateOnlyAsync(DateTime.UtcNow, cancellationToken);
        var fiscalYearId = currentPeriod?.FiscalYearId ?? 1;

        var isCard = request.PaymentMethod == PosPaymentMethod.Card;
        var paymentMethodStr = isCard ? "CARD" : "CASH";
        var cashAccount = isCard ? "111201" : "111101";

        var receiptDto = new CreateReceiptVoucherDto
        {
            BranchId = customer.BranchId ?? 1,
            FiscalYearId = fiscalYearId,
            VoucherDate = DateTime.UtcNow,
            Description = request.Notes ?? $"سداد ذمة من نقطة البيع - عميل {customer.CustomerCode} ({request.SettlementType})",
            PaymentMethod = paymentMethodStr,
            CashOrBankAccountCode = cashAccount,
            TotalAmount = totalPaymentAmount,
            CustomerCode = customer.CustomerCode,
            TransferReference = request.ReferenceNo,
            AutoPost = true,
            InvoicesToSettle = invoicesToSettle
        };

        var receiptResult = await _receiptVoucherService.CreateReceiptVoucherAsync(receiptDto, username, cancellationToken);

        if (request.ShiftId.HasValue && request.ShiftId.Value > 0 && request.PaymentMethod == PosPaymentMethod.Cash)
        {
            var cashMovementDto = new CashMovementDto
            {
                ShiftId = request.ShiftId.Value,
                MovementType = PosCashMovementType.FloatIn,
                Amount = totalPaymentAmount,
                Reason = $"قبض سداد ذمة عميل ({customer.CustomerCode}) - سند #{receiptResult.VoucherNo}",
                ApprovedBy = username
            };
            await _shiftService.RecordCashMovementAsync(cashMovementDto, username, cancellationToken);
        }

        var remainingBalance = await _arSubledgerRepository.GetCustomerBalanceAsync(customer.CustomerCode, cancellationToken);
        var appliedSum = invoicesToSettle.Sum(i => i.AmountToApply);
        var unappliedCredit = totalPaymentAmount > appliedSum ? totalPaymentAmount - appliedSum : 0m;

        var settledDetails = invoicesToSettle.Select(applied =>
        {
            var originalInv = openInvoices.FirstOrDefault(o => o.Id == applied.InvoiceTransactionId);
            var prevOpen = originalInv?.OpenAmount ?? applied.AmountToApply;
            var remainingInv = Math.Max(0m, prevOpen - applied.AmountToApply);
            return new PosSettledInvoiceDetailDto
            {
                InvoiceTransactionId = applied.InvoiceTransactionId,
                ReferenceNo = originalInv?.ReferenceNo ?? applied.InvoiceTransactionId.ToString(),
                AppliedAmount = applied.AmountToApply,
                RemainingInvoiceAmount = remainingInv,
                IsFullyPaid = remainingInv == 0
            };
        }).ToList();

        var resultDto = new PosCustomerSettlementResultDto
        {
            ReceiptNumber = receiptResult.VoucherNo.ToString(),
            VoucherId = receiptResult.VoucherId,
            SettlementDate = DateTime.UtcNow,
            CustomerId = customer.Id,
            CustomerCode = customer.CustomerCode,
            CustomerName = !string.IsNullOrWhiteSpace(customer.NameLocal) ? customer.NameLocal : customer.NameEn,
            SettlementType = request.SettlementType,
            PaymentMethod = request.PaymentMethod,
            AmountPaid = totalPaymentAmount,
            PreviousBalance = previousBalance,
            RemainingBalance = remainingBalance,
            AppliedToInvoicesAmount = appliedSum,
            UnappliedCreditAmount = unappliedCredit,
            InvoicesSettledCount = settledDetails.Count,
            SettledInvoices = settledDetails
        };

        return Ok(ApiResponse<PosCustomerSettlementResultDto>.CreateSuccess(
            resultDto,
            ResponseCodes.RecordCreated,
            200));
    }
}
