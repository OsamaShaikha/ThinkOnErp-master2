using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvCountRepository
{
    Task<InvCountSession?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<InvCountSession?> GetBySessionNoAsync(string sessionNo, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<InvCountSession> Sessions, long TotalCount)> GetAllAsync(
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task AddAsync(InvCountSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(InvCountSession session, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
