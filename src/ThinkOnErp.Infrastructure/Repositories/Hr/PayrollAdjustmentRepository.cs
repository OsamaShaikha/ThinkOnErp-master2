using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class PayrollAdjustmentRepository : IPayrollAdjustmentRepository
{
    private readonly OracleDbContext _context;

    public PayrollAdjustmentRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PayrollAdjustment>> GetAdjustmentsAsync(long companyId, string? employeeCode, string? payPeriod, string? status, CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollAdjustments
            .Include(a => a.Employee)
            .Include(a => a.Component)
            .Where(a => a.CompanyId == companyId);

        if (!string.IsNullOrEmpty(employeeCode))
            query = query.Where(a => a.EmployeeCode == employeeCode);

        if (!string.IsNullOrEmpty(payPeriod))
            query = query.Where(a => a.PayPeriod == payPeriod);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(a => a.Status == status);

        return await query.OrderByDescending(a => a.CreationDate).ToListAsync(cancellationToken);
    }

    public async Task<PayrollAdjustment?> GetAdjustmentByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollAdjustments
            .Include(a => a.Employee)
            .Include(a => a.Component)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollAdjustment>> GetApprovedAdjustmentsForPeriodAsync(long companyId, string payPeriod, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollAdjustments
            .Include(a => a.Component)
            .Where(a => a.CompanyId == companyId && a.PayPeriod == payPeriod && a.Status == "APPROVED")
            .ToListAsync(cancellationToken);
    }

    public async Task AddAdjustmentAsync(PayrollAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        await _context.PayrollAdjustments.AddAsync(adjustment, cancellationToken);
    }

    public Task UpdateAdjustmentAsync(PayrollAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        _context.PayrollAdjustments.Update(adjustment);
        return Task.CompletedTask;
    }

    public Task DeleteAdjustmentAsync(PayrollAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        _context.PayrollAdjustments.Remove(adjustment);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
