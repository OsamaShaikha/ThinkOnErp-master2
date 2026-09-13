using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.Services.Pos;

/// <summary>
/// Domain-specific audit service for the Point of Sale (POS) module.
/// Persists immutable, tamper-evident audit trails for all sensitive financial and operational
/// POS actions directly into the centralized "THINKON_AUDIT" schema via IAuditLogger.
/// Complies with BRD Section 30: Comprehensive POS Audit Log requirements.
/// </summary>
public interface IPosAuditService
{
    Task LogVoidLineAsync(long branchId, long orderId, long lineId, string itemCode, decimal quantity, decimal amount, string reason, string? approvedBy, string username, CancellationToken ct = default);

    Task LogRefundOrderAsync(long branchId, long originalOrderId, long refundOrderId, decimal refundAmount, string reason, string? approvedBy, string username, CancellationToken ct = default);

    Task LogPriceOverrideAsync(long branchId, long orderId, long lineId, string itemCode, decimal originalPrice, decimal newPrice, string reason, string? approvedBy, string username, CancellationToken ct = default);

    Task LogDiscountAppliedAsync(long branchId, long orderId, decimal discountAmount, decimal discountPercent, string discountType, string? approvedBy, string username, CancellationToken ct = default);

    Task LogCashMovementAsync(long branchId, long shiftId, long? tillId, PosCashMovementType movementType, decimal amount, string reason, string? approvedBy, string username, CancellationToken ct = default);

    Task LogBlindCloseShiftAsync(long branchId, long shiftId, long? tillId, decimal expectedCash, decimal countedCash, decimal? variance, string username, CancellationToken ct = default);

    Task LogShiftAuditAsync(long branchId, long shiftId, string auditedBy, string? auditNotes, decimal? finalVariance, string username, CancellationToken ct = default);

    Task LogZReportGeneratedAsync(long branchId, long reportId, long shiftId, string reportNumber, decimal totalSales, decimal totalTax, decimal? variance, string username, CancellationToken ct = default);

    Task LogGiftCardTransactionAsync(long branchId, long cardId, string cardNumberMasked, string transactionType, decimal amount, decimal balanceAfter, string username, CancellationToken ct = default);
}
