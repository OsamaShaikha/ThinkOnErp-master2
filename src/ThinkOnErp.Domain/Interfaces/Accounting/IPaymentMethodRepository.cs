using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface IPaymentMethodRepository
{
    Task<PaymentMethod?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<PaymentMethod?> GetByCodeAsync(long branchId, string code, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentMethod>> GetAllAsync(long? branchId, string? methodType, bool? showInPos, bool? showInInvoices, bool? activeOnly, CancellationToken ct = default);
    Task AddAsync(PaymentMethod method, CancellationToken ct = default);
    Task UpdateAsync(PaymentMethod method, CancellationToken ct = default);
    Task DeleteAsync(PaymentMethod method, CancellationToken ct = default);
}
