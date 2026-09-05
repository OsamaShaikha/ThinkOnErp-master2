using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class EmployeeDocumentRepository : IEmployeeDocumentRepository
{
    private readonly OracleDbContext _context;

    public EmployeeDocumentRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<EmployeeDocument>> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeDocuments
            .AsNoTracking()
            .Where(d => d.EmployeeCode == employeeCode && d.IsActive)
            .OrderBy(d => d.DocumentType)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeDocument?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeDocuments.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeDocument>> GetExpiringDocumentsAsync(DateTime thresholdDate, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeDocuments
            .Include(d => d.Employee)
                .ThenInclude(e => e!.Department)
            .AsNoTracking()
            .Where(d => d.IsActive && d.ExpiryDate != null && d.ExpiryDate <= thresholdDate)
            .OrderBy(d => d.ExpiryDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(EmployeeDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        await _context.EmployeeDocuments.AddAsync(document, cancellationToken);
    }

    public void Update(EmployeeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _context.EmployeeDocuments.Update(document);
    }

    public void Remove(EmployeeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _context.EmployeeDocuments.Remove(document);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
