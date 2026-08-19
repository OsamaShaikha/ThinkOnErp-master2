using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface IVendorRepository
{
    Task<Vendor?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Vendor?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vendor>> GetAllAsync(string? searchTerm = null, long? branchId = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddAsync(Vendor vendor, CancellationToken cancellationToken = default);
    void Update(Vendor vendor);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
