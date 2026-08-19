using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class BankingRepository : IBankingRepository
{
    private readonly OracleDbContext _context;

    public BankingRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<BankAccount?> GetBankAccountByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<BankAccount>()
            .Include(b => b.Branch)
            .Include(b => b.Currency)
            .Include(b => b.GlAccount)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<BankAccount?> GetBankAccountByNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Set<BankAccount>()
            .Include(b => b.Branch)
            .Include(b => b.Currency)
            .Include(b => b.GlAccount)
            .FirstOrDefaultAsync(b => b.AccountNumber == accountNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<BankAccount>> GetBankAccountsAsync(long? branchId, bool? activeOnly, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<BankAccount>()
            .Include(b => b.Branch)
            .Include(b => b.Currency)
            .Include(b => b.GlAccount)
            .AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(b => b.BranchId == branchId.Value);
        }

        if (activeOnly.HasValue)
        {
            query = query.Where(b => b.IsActive == activeOnly.Value);
        }

        return await query.OrderBy(b => b.BankName).ThenBy(b => b.AccountNumber).ToListAsync(cancellationToken);
    }

    public async Task AddBankAccountAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        await _context.Set<BankAccount>().AddAsync(account, cancellationToken);
    }

    public async Task<CashRegister?> GetCashRegisterByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<CashRegister>()
            .Include(c => c.Branch)
            .Include(c => c.Currency)
            .Include(c => c.GlAccount)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<CashRegister?> GetCashRegisterByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Set<CashRegister>()
            .Include(c => c.Branch)
            .Include(c => c.Currency)
            .Include(c => c.GlAccount)
            .FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
    }

    public async Task<IReadOnlyList<CashRegister>> GetCashRegistersAsync(long? branchId, bool? activeOnly, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<CashRegister>()
            .Include(c => c.Branch)
            .Include(c => c.Currency)
            .Include(c => c.GlAccount)
            .AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(c => c.BranchId == branchId.Value);
        }

        if (activeOnly.HasValue)
        {
            query = query.Where(c => c.IsActive == activeOnly.Value);
        }

        return await query.OrderBy(c => c.Code).ToListAsync(cancellationToken);
    }

    public async Task AddCashRegisterAsync(CashRegister register, CancellationToken cancellationToken = default)
    {
        await _context.Set<CashRegister>().AddAsync(register, cancellationToken);
    }

    public async Task<BankReconciliation?> GetReconciliationByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<BankReconciliation>()
            .Include(r => r.BankAccount)
            .Include(r => r.FiscalYear)
            .Include(r => r.FiscalPeriod)
            .Include(r => r.StatementLines)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<BankReconciliation>> GetReconciliationsAsync(long? bankAccountId, long? fiscalYearId, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<BankReconciliation>()
            .Include(r => r.BankAccount)
            .Include(r => r.FiscalYear)
            .Include(r => r.FiscalPeriod)
            .AsQueryable();

        if (bankAccountId.HasValue && bankAccountId.Value > 0)
        {
            query = query.Where(r => r.BankAccountId == bankAccountId.Value);
        }

        if (fiscalYearId.HasValue && fiscalYearId.Value > 0)
        {
            query = query.Where(r => r.FiscalYearId == fiscalYearId.Value);
        }

        return await query.OrderByDescending(r => r.StatementDate).ToListAsync(cancellationToken);
    }

    public async Task AddReconciliationAsync(BankReconciliation reconciliation, CancellationToken cancellationToken = default)
    {
        await _context.Set<BankReconciliation>().AddAsync(reconciliation, cancellationToken);
    }

    public async Task AddStatementLinesAsync(IEnumerable<BankStatementLine> lines, CancellationToken cancellationToken = default)
    {
        await _context.Set<BankStatementLine>().AddRangeAsync(lines, cancellationToken);
    }

    public async Task RemoveStatementLinesAsync(long reconciliationId, CancellationToken cancellationToken = default)
    {
        var lines = await _context.Set<BankStatementLine>().Where(l => l.ReconciliationId == reconciliationId).ToListAsync(cancellationToken);
        _context.Set<BankStatementLine>().RemoveRange(lines);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
