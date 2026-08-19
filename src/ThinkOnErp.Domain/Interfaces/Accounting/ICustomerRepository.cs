using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Customer?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Customer>> GetAllAsync(string? searchTerm = null, long? branchId = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
    void Update(Customer customer);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
