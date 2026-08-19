using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly OracleDbContext _context;

    public CustomerRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<Customer?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .Include(c => c.ArControlAccount)
            .Include(c => c.DefaultCurrency)
            .Include(c => c.Branch)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Customer?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .Include(c => c.ArControlAccount)
            .Include(c => c.DefaultCurrency)
            .Include(c => c.Branch)
            .FirstOrDefaultAsync(c => c.CustomerCode == code, cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(
        string? searchTerm = null,
        long? branchId = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Customers
            .Include(c => c.ArControlAccount)
            .Include(c => c.DefaultCurrency)
            .Include(c => c.Branch)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToUpper();
            query = query.Where(c =>
                c.CustomerCode.ToUpper().Contains(term) ||
                c.NameAr.ToUpper().Contains(term) ||
                c.NameEn.ToUpper().Contains(term) ||
                (c.Phone != null && c.Phone.Contains(term)) ||
                (c.TaxNumber != null && c.TaxNumber.Contains(term)));
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(c => c.BranchId == branchId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        return await query.OrderBy(c => c.CustomerCode).ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Customers.AnyAsync(c => c.CustomerCode == code, cancellationToken);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await _context.Customers.AddAsync(customer, cancellationToken);
    }

    public void Update(Customer customer)
    {
        _context.Customers.Update(customer);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
