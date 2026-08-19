using ThinkOnErp.Application.DTOs.Accounting.Vouchers;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IReceiptPaymentVoucherService
{
    Task<ReceiptVoucherDto> CreateReceiptVoucherAsync(CreateReceiptVoucherDto dto, string username, CancellationToken cancellationToken = default);
    Task<ReceiptVoucherDto> UpdateReceiptVoucherAsync(long voucherId, UpdateReceiptVoucherDto dto, string username, CancellationToken cancellationToken = default);
    Task<bool> DeleteReceiptVoucherAsync(long voucherId, string username, CancellationToken cancellationToken = default);
    Task<ReceiptVoucherDto> GetReceiptVoucherByIdAsync(long voucherId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReceiptVoucherDto>> GetReceiptVouchersAsync(long? branchId, DateTime? fromDate, DateTime? toDate, string? customerCode, CancellationToken cancellationToken = default);

    Task<PaymentVoucherDto> CreatePaymentVoucherAsync(CreatePaymentVoucherDto dto, string username, CancellationToken cancellationToken = default);
    Task<PaymentVoucherDto> UpdatePaymentVoucherAsync(long voucherId, UpdatePaymentVoucherDto dto, string username, CancellationToken cancellationToken = default);
    Task<bool> DeletePaymentVoucherAsync(long voucherId, string username, CancellationToken cancellationToken = default);
    Task<PaymentVoucherDto> GetPaymentVoucherByIdAsync(long voucherId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentVoucherDto>> GetPaymentVouchersAsync(long? branchId, DateTime? fromDate, DateTime? toDate, string? vendorCode, CancellationToken cancellationToken = default);
}
