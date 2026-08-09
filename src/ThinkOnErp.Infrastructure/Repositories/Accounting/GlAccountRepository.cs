using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class GlAccountRepository : IGlAccountRepository
{
    private readonly OracleDbContext _context;

    public GlAccountRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<GlAccount>> GetAllAsync(
        long companyId,
        CancellationToken cancellationToken = default)
    {
        return await AccountReadQuery()
            .Where(account => account.CompanyId == companyId)
            .OrderBy(account => account.AccountCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GlAccount>> GetPostableAsync(
        long companyId,
        long branchId,
        CancellationToken cancellationToken = default)
    {
        return await AccountReadQuery()
            .Where(account =>
                account.CompanyId == companyId &&
                account.IsActive &&
                account.AccountType == "DETAIL" &&
                (!account.IsBranchSpecific || account.BranchLinks.Any(link =>
                    link.BranchId == branchId && link.IsActive)))
            .OrderBy(account => account.AccountCode)
            .ToListAsync(cancellationToken);
    }

    public Task<GlAccount?> GetByIdAsync(
        long companyId,
        long accountId,
        CancellationToken cancellationToken = default)
    {
        return _context.GlAccounts
            .Include(account => account.Category)
            .Include(account => account.BranchLinks)
            .SingleOrDefaultAsync(
                account => account.CompanyId == companyId && account.Id == accountId,
                cancellationToken);
    }

    public Task<bool> AccountCodeExistsAsync(
        long companyId,
        string accountCode,
        CancellationToken cancellationToken = default)
    {
        return _context.GlAccounts
            .AsNoTracking()
            .AnyAsync(
                account => account.CompanyId == companyId && account.AccountCode == accountCode,
                cancellationToken);
    }

    public Task<bool> HasChildrenAsync(
        long companyId,
        long accountId,
        CancellationToken cancellationToken = default)
    {
        return _context.GlAccounts
            .AsNoTracking()
            .AnyAsync(
                account =>
                    account.CompanyId == companyId &&
                    account.ParentAccountId == accountId,
                cancellationToken);
    }

    public Task<bool> HasAccountsAsync(
        long companyId,
        CancellationToken cancellationToken = default)
    {
        return _context.GlAccounts
            .AsNoTracking()
            .AnyAsync(account => account.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountCategory>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.AccountCategories
            .AsNoTracking()
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.CategoryCode)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> BranchBelongsToCompanyAsync(
        long companyId,
        long branchId,
        CancellationToken cancellationToken = default)
    {
        return _context.SysBranches
            .AsNoTracking()
            .AnyAsync(
                branch =>
                    branch.Id == branchId &&
                    branch.CompanyId == companyId &&
                    branch.IsActive,
                cancellationToken);
    }

    public async Task AddAsync(
        GlAccount account,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await _context.GlAccounts.AddAsync(account, cancellationToken);
    }

    public async Task DeleteAsync(
        GlAccount account,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        _context.GlAccountBranches.RemoveRange(account.BranchLinks);
        _context.GlAccounts.Remove(account);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsChildRecordConstraint(exception))
        {
            throw new AccountingConflictException(
                "The account is referenced by accounting data and cannot be deleted.",
                "GL_ACCOUNT_IN_USE",
                exception);
        }
    }

    public async Task ImportAsync(
        IReadOnlyCollection<GlAccount> accounts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        if (accounts.Count == 0)
        {
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await _context.GlAccounts.AddRangeAsync(accounts, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<GlAccount> AccountReadQuery()
    {
        return _context.GlAccounts
            .AsNoTracking()
            .Include(account => account.Category)
            .Include(account => account.BranchLinks);
    }

    private static bool IsChildRecordConstraint(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is OracleException { Number: 2292 })
            {
                return true;
            }

            if (current.InnerException is null)
            {
                break;
            }
        }

        return false;
    }
}
