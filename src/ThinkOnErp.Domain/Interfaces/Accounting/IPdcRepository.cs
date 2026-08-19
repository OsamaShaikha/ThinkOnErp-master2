using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface IPdcRepository
{
    Task<GlPdcRegister?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<GlPdcRegister?> GetByChequeNumberAsync(string chequeNumber, string chequeType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlPdcRegister>> GetChequesAsync(
        long? branchId,
        string? chequeType,
        string? status,
        string? partyCode,
        DateTime? fromDueDate,
        DateTime? toDueDate,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlPdcRegister>> GetUpcomingMaturitiesAsync(long? branchId, int daysAhead, CancellationToken cancellationToken = default);
    Task AddAsync(GlPdcRegister pdc, CancellationToken cancellationToken = default);
    void Remove(GlPdcRegister pdc);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
