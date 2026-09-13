using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Services.Pos;

/// <summary>
/// Implementation of POS audit service that translates critical POS operational and financial actions
/// into structured DataChangeAuditEvent objects and dispatches them to the centralized THINKON_AUDIT database.
/// </summary>
public class PosAuditService : IPosAuditService
{
    private readonly IAuditLogger _auditLogger;
    private readonly IAuditContextProvider _contextProvider;
    private readonly ILogger<PosAuditService> _logger;

    public PosAuditService(
        IAuditLogger auditLogger,
        IAuditContextProvider contextProvider,
        ILogger<PosAuditService> logger)
    {
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LogVoidLineAsync(long branchId, long orderId, long lineId, string itemCode, decimal quantity, decimal amount, string reason, string? approvedBy, string username, CancellationToken ct = default)
    {
        try
        {
            var auditEvent = BuildBaseAuditEvent("VOID", "POS_ORDER_LINE", lineId, branchId);
            auditEvent.OldValue = JsonSerializer.Serialize(new { OrderId = orderId, LineId = lineId, ItemCode = itemCode, Quantity = quantity, Amount = amount });
            auditEvent.NewValue = JsonSerializer.Serialize(new { Status = "VOIDED", VoidedBy = username, ApprovedBy = approvedBy, Reason = reason });
            auditEvent.Metadata = JsonSerializer.Serialize(new
            {
                EventType = "PosLineVoid",
                OrderId = orderId,
                LineId = lineId,
                ItemCode = itemCode,
                Quantity = quantity,
                Amount = amount,
                Reason = reason,
                ApprovedBy = approvedBy,
                Operator = username
            });

            await _auditLogger.LogDataChangeAsync(auditEvent, ct);
            _logger.LogInformation("POS Audit: Line {LineId} on Order {OrderId} voided by {User}. Reason: {Reason}", lineId, orderId, username, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log POS void line audit for Order {OrderId}, Line {LineId}", orderId, lineId);
        }
    }

    public async Task LogRefundOrderAsync(long branchId, long originalOrderId, long refundOrderId, decimal refundAmount, string reason, string? approvedBy, string username, CancellationToken ct = default)
    {
        try
        {
            var auditEvent = BuildBaseAuditEvent("REFUND", "POS_ORDER_HEADER", refundOrderId, branchId);
            auditEvent.OldValue = JsonSerializer.Serialize(new { OriginalOrderId = originalOrderId });
            auditEvent.NewValue = JsonSerializer.Serialize(new { RefundOrderId = refundOrderId, RefundAmount = refundAmount, Status = "REFUNDED" });
            auditEvent.Metadata = JsonSerializer.Serialize(new
            {
                EventType = "PosOrderRefund",
                OriginalOrderId = originalOrderId,
                RefundOrderId = refundOrderId,
                RefundAmount = refundAmount,
                Reason = reason,
                ApprovedBy = approvedBy,
                Operator = username
            });

            await _auditLogger.LogDataChangeAsync(auditEvent, ct);
            _logger.LogInformation("POS Audit: Order {OriginalOrderId} refunded via Refund Order {RefundOrderId} for {Amount}. Reason: {Reason}", originalOrderId, refundOrderId, refundAmount, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log POS refund audit for Original Order {OriginalOrderId}", originalOrderId);
        }
    }

    public async Task LogPriceOverrideAsync(long branchId, long orderId, long lineId, string itemCode, decimal originalPrice, decimal newPrice, string reason, string? approvedBy, string username, CancellationToken ct = default)
    {
        try
        {
            var auditEvent = BuildBaseAuditEvent("PRICE_OVERRIDE", "POS_ORDER_LINE", lineId, branchId);
            auditEvent.OldValue = JsonSerializer.Serialize(new { UnitPrice = originalPrice });
            auditEvent.NewValue = JsonSerializer.Serialize(new { UnitPrice = newPrice });
            auditEvent.Metadata = JsonSerializer.Serialize(new
            {
                EventType = "PosPriceOverride",
                OrderId = orderId,
                LineId = lineId,
                ItemCode = itemCode,
                OriginalPrice = originalPrice,
                NewPrice = newPrice,
                PriceDifference = newPrice - originalPrice,
                Reason = reason,
                ApprovedBy = approvedBy,
                Operator = username
            });

            await _auditLogger.LogDataChangeAsync(auditEvent, ct);
            _logger.LogInformation("POS Audit: Price overridden on Line {LineId} from {OriginalPrice} to {NewPrice} by {User}", lineId, originalPrice, newPrice, username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log POS price override audit for Line {LineId}", lineId);
        }
    }

    public async Task LogDiscountAppliedAsync(long branchId, long orderId, decimal discountAmount, decimal discountPercent, string discountType, string? approvedBy, string username, CancellationToken ct = default)
    {
        try
        {
            var auditEvent = BuildBaseAuditEvent("DISCOUNT", "POS_ORDER_HEADER", orderId, branchId);
            auditEvent.NewValue = JsonSerializer.Serialize(new { DiscountAmount = discountAmount, DiscountPercent = discountPercent, DiscountType = discountType });
            auditEvent.Metadata = JsonSerializer.Serialize(new
            {
                EventType = "PosDiscountApplied",
                OrderId = orderId,
                DiscountAmount = discountAmount,
                DiscountPercent = discountPercent,
                DiscountType = discountType,
                ApprovedBy = approvedBy,
                Operator = username
            });

            await _auditLogger.LogDataChangeAsync(auditEvent, ct);
            _logger.LogInformation("POS Audit: Discount {Amount} ({Percent}%) applied to Order {OrderId} by {User}", discountAmount, discountPercent, orderId, username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log POS discount audit for Order {OrderId}", orderId);
        }
    }

    public async Task LogCashMovementAsync(long branchId, long shiftId, long? tillId, PosCashMovementType movementType, decimal amount, string reason, string? approvedBy, string username, CancellationToken ct = default)
    {
        try
        {
            var auditEvent = BuildBaseAuditEvent("CASH_MOVEMENT", "POS_SHIFT_CASH_MOVEMENT", shiftId, branchId);
            auditEvent.NewValue = JsonSerializer.Serialize(new { ShiftId = shiftId, TillId = tillId, MovementType = movementType.ToString(), Amount = amount, Reason = reason });
            auditEvent.Metadata = JsonSerializer.Serialize(new
            {
                EventType = "PosCashMovement",
                ShiftId = shiftId,
                TillId = tillId,
                MovementType = movementType.ToString(),
                Amount = amount,
                Reason = reason,
                ApprovedBy = approvedBy,
                Operator = username
            });

            await _auditLogger.LogDataChangeAsync(auditEvent, ct);
            _logger.LogInformation("POS Audit: Cash movement {Type} of {Amount} recorded on Shift {ShiftId} by {User}", movementType, amount, shiftId, username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log POS cash movement audit for Shift {ShiftId}", shiftId);
        }
    }

    public async Task LogBlindCloseShiftAsync(long branchId, long shiftId, long? tillId, decimal expectedCash, decimal countedCash, decimal? variance, string username, CancellationToken ct = default)
    {
        try
        {
            var auditEvent = BuildBaseAuditEvent("BLIND_CLOSE", "POS_SHIFT", shiftId, branchId);
            auditEvent.OldValue = JsonSerializer.Serialize(new { Status = "OPEN", ExpectedCash = expectedCash });
            auditEvent.NewValue = JsonSerializer.Serialize(new { Status = "BLIND_CLOSED", CountedCash = countedCash, Variance = variance });
            auditEvent.Metadata = JsonSerializer.Serialize(new
            {
                EventType = "PosBlindClose",
                ShiftId = shiftId,
                TillId = tillId,
                ExpectedCash = expectedCash,
                CountedCash = countedCash,
                Variance = variance,
                Operator = username,
                HasDiscrepancy = variance != 0
            });

            await _auditLogger.LogDataChangeAsync(auditEvent, ct);
            _logger.LogInformation("POS Audit: Shift {ShiftId} blind-closed by {User}. Expected: {Expected}, Counted: {Counted}, Variance: {Variance}", shiftId, username, expectedCash, countedCash, variance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log POS blind close audit for Shift {ShiftId}", shiftId);
        }
    }

    public async Task LogShiftAuditAsync(long branchId, long shiftId, string auditedBy, string? auditNotes, decimal? finalVariance, string username, CancellationToken ct = default)
    {
        try
        {
            var auditEvent = BuildBaseAuditEvent("SHIFT_AUDIT", "POS_SHIFT", shiftId, branchId);
            auditEvent.NewValue = JsonSerializer.Serialize(new { Status = "AUDITED_AND_CLOSED", AuditedBy = auditedBy, FinalVariance = finalVariance, Notes = auditNotes });
            auditEvent.Metadata = JsonSerializer.Serialize(new
            {
                EventType = "PosShiftSupervisorAudit",
                ShiftId = shiftId,
                AuditedBy = auditedBy,
                FinalVariance = finalVariance,
                AuditNotes = auditNotes,
                FinalizedBy = username
            });

            await _auditLogger.LogDataChangeAsync(auditEvent, ct);
            _logger.LogInformation("POS Audit: Shift {ShiftId} audited and closed by {AuditedBy}. Final Variance: {Variance}", shiftId, auditedBy, finalVariance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log POS shift audit for Shift {ShiftId}", shiftId);
        }
    }

    public async Task LogZReportGeneratedAsync(long branchId, long reportId, long shiftId, string reportNumber, decimal totalSales, decimal totalTax, decimal? variance, string username, CancellationToken ct = default)
    {
        try
        {
            var auditEvent = BuildBaseAuditEvent("Z_REPORT", "POS_Z_REPORT", reportId, branchId);
            auditEvent.NewValue = JsonSerializer.Serialize(new { ReportNumber = reportNumber, ShiftId = shiftId, TotalSales = totalSales, TotalTax = totalTax, Variance = variance });
            auditEvent.Metadata = JsonSerializer.Serialize(new
            {
                EventType = "PosZReportGenerated",
                ReportId = reportId,
                ReportNumber = reportNumber,
                ShiftId = shiftId,
                TotalSales = totalSales,
                TotalTax = totalTax,
                Variance = variance,
                GeneratedBy = username
            });

            await _auditLogger.LogDataChangeAsync(auditEvent, ct);
            _logger.LogInformation("POS Audit: Z-Report {ReportNumber} generated for Shift {ShiftId} by {User}", reportNumber, shiftId, username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log POS Z-Report audit for Report {ReportId}", reportId);
        }
    }

    public async Task LogGiftCardTransactionAsync(long branchId, long cardId, string cardNumberMasked, string transactionType, decimal amount, decimal balanceAfter, string username, CancellationToken ct = default)
    {
        try
        {
            var auditEvent = BuildBaseAuditEvent("GIFT_CARD_TRX", "POS_GIFT_CARD", cardId, branchId);
            auditEvent.NewValue = JsonSerializer.Serialize(new { CardNumber = cardNumberMasked, Type = transactionType, Amount = amount, BalanceAfter = balanceAfter });
            auditEvent.Metadata = JsonSerializer.Serialize(new
            {
                EventType = "PosGiftCardTransaction",
                CardId = cardId,
                CardNumberMasked = cardNumberMasked,
                TransactionType = transactionType,
                Amount = amount,
                BalanceAfter = balanceAfter,
                Operator = username
            });

            await _auditLogger.LogDataChangeAsync(auditEvent, ct);
            _logger.LogInformation("POS Audit: Gift card {Card} transaction {Type} of {Amount}. New balance: {Balance} by {User}", cardNumberMasked, transactionType, amount, balanceAfter, username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log POS gift card audit for Card {CardId}", cardId);
        }
    }

    private DataChangeAuditEvent BuildBaseAuditEvent(string action, string entityType, long entityId, long branchId)
    {
        return new DataChangeAuditEvent
        {
            CorrelationId = _contextProvider.GetCorrelationId(),
            ActorType = _contextProvider.GetActorType(),
            ActorId = _contextProvider.GetActorId(),
            CompanyId = _contextProvider.GetCompanyId(),
            BranchId = branchId > 0 ? branchId : _contextProvider.GetBranchId(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            IpAddress = _contextProvider.GetIpAddress(),
            UserAgent = _contextProvider.GetUserAgent(),
            EventCategory = "POS_FINANCIAL_AUDIT",
            Timestamp = DateTime.UtcNow
        };
    }
}
