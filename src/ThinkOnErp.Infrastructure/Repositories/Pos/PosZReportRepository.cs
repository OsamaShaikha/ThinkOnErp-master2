using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Interfaces.Pos;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Pos;

public class PosZReportRepository : IPosZReportRepository
{
    private readonly OracleDbContext _context;

    public PosZReportRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<long> GetNextZSequenceNumberAsync(long branchId, CancellationToken ct = default)
    {
        var maxSeq = await _context.PosZReports
            .Where(z => z.BranchId == branchId)
            .MaxAsync(z => (long?)z.ZSequenceNumber, ct);

        return (maxSeq ?? 0) + 1;
    }

    public async Task<PosZReport?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.PosZReports
            .Include(z => z.Till)
            .Include(z => z.Shift)
            .FirstOrDefaultAsync(z => z.Id == id, ct);
    }

    public async Task<(IReadOnlyList<PosZReport> Items, long TotalCount)> GetZReportsPagedAsync(
        long branchId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.PosZReports
            .AsNoTracking()
            .Include(z => z.Till)
            .Include(z => z.Shift)
            .Where(z => z.BranchId == branchId);

        if (fromDate.HasValue)
            query = query.Where(z => z.ReportDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(z => z.ReportDate <= toDate.Value);

        var totalCount = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(z => z.ReportDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddZReportAsync(PosZReport report, CancellationToken ct = default)
    {
        await _context.PosZReports.AddAsync(report, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
