using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.PaymentMethods;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IPaymentMethodService
{
    Task<IReadOnlyList<PaymentMethodDto>> GetAllAsync(long? branchId, string? methodType, bool? showInPos, bool? showInInvoices, bool? activeOnly, CancellationToken ct = default);
    Task<PaymentMethodDto?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<PaymentMethodDto?> GetByCodeAsync(long branchId, string code, CancellationToken ct = default);
    Task<ApiResponse<PaymentMethodDto>> CreateAsync(CreatePaymentMethodDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PaymentMethodDto>> UpdateAsync(long id, UpdatePaymentMethodDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<int>> SeedDefaultPaymentMethodsAsync(long branchId, string username, CancellationToken ct = default);
}
