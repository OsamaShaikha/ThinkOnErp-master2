using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class VendorRepository : IVendorRepository
{
    private readonly OracleDbContext _context;

    public VendorRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<Vendor?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.Vendors
            .Include(v => v.ApControlAccount)
            .Include(v => v.DefaultCurrency)
            .Include(v => v.Branch)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<Vendor?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Vendors
            .Include(v => v.ApControlAccount)
            .Include(v => v.DefaultCurrency)
            .Include(v => v.Branch)
            .FirstOrDefaultAsync(v => v.VendorCode == code, cancellationToken);
    }

    public async Task<IReadOnlyList<Vendor>> GetAllAsync(
        string? searchTerm = null,
        long? branchId = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Vendors
            .Include(v => v.ApControlAccount)
            .Include(v => v.DefaultCurrency)
            .Include(v => v.Branch)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToUpper();
            query = query.Where(v =>
                v.VendorCode.ToUpper().Contains(term) ||
                v.NameAr.ToUpper().Contains(term) ||
                v.NameEn.ToUpper().Contains(term) ||
                (v.Phone != null && v.Phone.Contains(term)) ||
                (v.TaxNumber != null && v.TaxNumber.Contains(term)));
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(v => v.BranchId == branchId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(v => v.IsActive == isActive.Value);
        }

        return await query.OrderBy(v => v.VendorCode).ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Vendors.AnyAsync(v => v.VendorCode == code, cancellationToken);
    }

    public async Task AddAsync(Vendor vendor, CancellationToken cancellationToken = default)
    {
        await _context.Vendors.AddAsync(vendor, cancellationToken);
    }

    public void Update(Vendor vendor)
    {
        _context.Vendors.Update(vendor);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
