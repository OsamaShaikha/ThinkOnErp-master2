using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface IGlVoucherRepository
{
    Task<IReadOnlyList<GlVoucherType>> GetVoucherTypesAsync(CancellationToken cancellationToken = default);
    Task<GlVoucherType?> GetVoucherTypeByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<GlVoucherType?> GetVoucherTypeByCodeAsync(int typeCode, CancellationToken cancellationToken = default);
    Task<GlVoucherType?> GetVoucherTypeByKeyAsync(string typeKey, CancellationToken cancellationToken = default);
    Task<GlVoucherType> CreateVoucherTypeAsync(GlVoucherType voucherType, CancellationToken cancellationToken = default);
    Task<GlVoucherType> UpdateVoucherTypeAsync(GlVoucherType voucherType, CancellationToken cancellationToken = default);
    Task DeleteVoucherTypeAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> VoucherTypeExistsAsync(int typeCode, string typeKey, long? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> HasAssociatedVouchersAsync(int typeCode, CancellationToken cancellationToken = default);

    Task<GlVoucherHeader?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<GlVoucherHeader?> GetByNumberAsync(
        long branchId,
        int year,
        int month,
        int typeCode,
        long voucherNo,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<GlVoucherHeader> Items, long TotalCount)> GetPagedVouchersAsync(
        long? branchId,
        int? year,
        int? month,
        int? typeCode,
        int? status,
        DateTime? fromDate,
        DateTime? toDate,
        string? searchKeyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<long> GenerateNextSerialNoAsync(
        long branchId,
        int year,
        int month,
        int typeCode,
        string resetPolicy,
        CancellationToken cancellationToken = default);

    Task AddVoucherAsync(GlVoucherHeader voucher, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
