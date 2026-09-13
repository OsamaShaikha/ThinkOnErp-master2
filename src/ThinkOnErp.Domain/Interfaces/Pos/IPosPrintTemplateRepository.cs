using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Domain.Interfaces.Pos;

public interface IPosPrintTemplateRepository
{
    // Print Templates
    Task<IReadOnlyList<PosPrintTemplate>> GetTemplatesByBranchAsync(long branchId, string? templateType = null, CancellationToken ct = default);
    Task<PosPrintTemplate?> GetTemplateByIdAsync(long id, CancellationToken ct = default);
    Task<PosPrintTemplate?> GetDefaultTemplateAsync(long branchId, string templateType, CancellationToken ct = default);
    Task AddTemplateAsync(PosPrintTemplate template, CancellationToken ct = default);
    Task UpdateTemplateAsync(PosPrintTemplate template, CancellationToken ct = default);
    Task DeleteTemplateAsync(PosPrintTemplate template, CancellationToken ct = default);

    // Printer Routing
    Task<IReadOnlyList<PosPrinterRouting>> GetRoutingsByBranchAsync(long branchId, CancellationToken ct = default);
    Task<PosPrinterRouting?> GetRoutingByIdAsync(long id, CancellationToken ct = default);
    Task AddRoutingAsync(PosPrinterRouting routing, CancellationToken ct = default);
    Task UpdateRoutingAsync(PosPrinterRouting routing, CancellationToken ct = default);
    Task DeleteRoutingAsync(PosPrinterRouting routing, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
