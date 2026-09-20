using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Interfaces.Pos;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Pos;

public class PosPrintTemplateRepository : IPosPrintTemplateRepository
{
    private readonly OracleDbContext _context;

    public PosPrintTemplateRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PosPrintTemplate>> GetTemplatesByBranchAsync(long branchId, string? templateType = null, CancellationToken ct = default)
    {
        var query = _context.PosPrintTemplates
            .AsNoTracking()
            .Where(t => t.BranchId == branchId);

        if (!string.IsNullOrWhiteSpace(templateType))
        {
            query = query.Where(t => t.TemplateType == templateType);
        }

        return await query.OrderBy(t => t.TemplateCode).ToListAsync(ct);
    }

    public async Task<PosPrintTemplate?> GetTemplateByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.PosPrintTemplates
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<PosPrintTemplate?> GetDefaultTemplateAsync(long branchId, string templateType, CancellationToken ct = default)
    {
        return await _context.PosPrintTemplates
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.TemplateType == templateType && t.IsDefault && t.IsActive, ct);
    }

    public async Task AddTemplateAsync(PosPrintTemplate template, CancellationToken ct = default)
    {
        await _context.PosPrintTemplates.AddAsync(template, ct);
    }

    public Task UpdateTemplateAsync(PosPrintTemplate template, CancellationToken ct = default)
    {
        _context.PosPrintTemplates.Update(template);
        return Task.CompletedTask;
    }

    public Task DeleteTemplateAsync(PosPrintTemplate template, CancellationToken ct = default)
    {
        _context.PosPrintTemplates.Remove(template);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<PosPrinterRouting>> GetRoutingsByBranchAsync(long branchId, CancellationToken ct = default)
    {
        return await _context.PosPrinterRoutings
            .AsNoTracking()
            .Include(r => r.ItemCategory)
            .Where(r => r.BranchId == branchId)
            .OrderBy(r => r.StationName)
            .ToListAsync(ct);
    }

    public async Task<PosPrinterRouting?> GetRoutingByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.PosPrinterRoutings
            .Include(r => r.ItemCategory)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task AddRoutingAsync(PosPrinterRouting routing, CancellationToken ct = default)
    {
        await _context.PosPrinterRoutings.AddAsync(routing, ct);
    }

    public Task UpdateRoutingAsync(PosPrinterRouting routing, CancellationToken ct = default)
    {
        _context.PosPrinterRoutings.Update(routing);
        return Task.CompletedTask;
    }

    public Task DeleteRoutingAsync(PosPrinterRouting routing, CancellationToken ct = default)
    {
        _context.PosPrinterRoutings.Remove(routing);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
