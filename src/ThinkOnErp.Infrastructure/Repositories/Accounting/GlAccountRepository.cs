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
                account.IsActive &&
                account.AccountType == "DETAIL" &&
                (!account.IsBranchSpecific || account.BranchLinks.Any(link =>
                    link.BranchId == branchId && link.IsActive)))
            .OrderBy(account => account.AccountCode)
            .ToListAsync(cancellationToken);
    }

    public Task<GlAccount?> GetByCodeAsync(
        long companyId,
        string accountCode,
        CancellationToken cancellationToken = default)
    {
        return _context.GlAccounts
            .Include(account => account.BranchLinks)
            .SingleOrDefaultAsync(
                account => account.AccountCode == accountCode,
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
                account => account.AccountCode == accountCode,
                cancellationToken);
    }

    public Task<bool> HasChildrenAsync(
        long companyId,
        string accountCode,
        CancellationToken cancellationToken = default)
    {
        return _context.GlAccounts
            .AsNoTracking()
            .AnyAsync(
                account => account.ParentAccountCode == accountCode,
                cancellationToken);
    }

    public Task<bool> HasAccountsAsync(
        long companyId,
        CancellationToken cancellationToken = default)
    {
        return _context.GlAccounts
            .AsNoTracking()
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GlAccountStructureConfig>> GetStructureConfigsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.GlAccountStructureConfigs
            .AsNoTracking()
            .OrderBy(c => c.LevelNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveStructureConfigsAsync(IEnumerable<GlAccountStructureConfig> configs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configs);

        foreach (var config in configs)
        {
            var existing = await _context.GlAccountStructureConfigs
                .FirstOrDefaultAsync(c => c.LevelNumber == config.LevelNumber, cancellationToken);

            if (existing != null)
            {
                existing.DigitLength = config.DigitLength;
                if (!string.IsNullOrWhiteSpace(config.LevelNameAr)) existing.LevelNameAr = config.LevelNameAr;
                if (!string.IsNullOrWhiteSpace(config.LevelNameEn)) existing.LevelNameEn = config.LevelNameEn;
                if (config.Description != null) existing.Description = config.Description;
                existing.IsActive = config.IsActive;
            }
            else
            {
                await _context.GlAccountStructureConfigs.AddAsync(config, cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GetNextChildCodeAsync(
        long companyId,
        string parentAccountCode,
        CancellationToken cancellationToken = default)
    {
        var existingCodes = await _context.GlAccounts
            .AsNoTracking()
            .Where(a => a.ParentAccountCode == parentAccountCode)
            .Select(a => a.AccountCode)
            .ToListAsync(cancellationToken);

        // Fetch Level Digit Configurations from database
        var configs = await GetStructureConfigsAsync(cancellationToken);

        int parentLevel = 1;
        int currentCumulative = 0;
        int suffixLength = 1;

        if (configs.Count > 0)
        {
            // Determine parent level by matching cumulative digit lengths
            for (int i = 0; i < configs.Count; i++)
            {
                currentCumulative += configs[i].DigitLength;
                if (parentAccountCode.Length <= currentCumulative)
                {
                    parentLevel = configs[i].LevelNumber;
                    break;
                }
            }

            int childLevel = parentLevel + 1;
            var childConfig = configs.FirstOrDefault(c => c.LevelNumber == childLevel);
            if (childConfig != null)
            {
                suffixLength = childConfig.DigitLength;
            }
            else
            {
                suffixLength = parentAccountCode.Length >= 4 ? 2 : 1;
            }
        }
        else
        {
            // Fallback default rules if configuration table is unseeded
            int parentLength = parentAccountCode.Length;
            int targetLengthFallback = parentLength switch
            {
                1 => 2,
                2 => 3,
                3 => 4,
                _ => parentLength + 2
            };
            suffixLength = targetLengthFallback - parentLength;
        }

        int targetLength = parentAccountCode.Length + suffixLength;

        if (existingCodes.Count == 0)
        {
            if (suffixLength == 1) return parentAccountCode + "1";
            return parentAccountCode + "1".PadLeft(suffixLength, '0');
        }

        long maxSuffix = 0;
        foreach (var code in existingCodes)
        {
            if (code.Length == targetLength && code.StartsWith(parentAccountCode))
            {
                var suffixStr = code.Substring(parentAccountCode.Length);
                if (long.TryParse(suffixStr, out var suffixVal) && suffixVal > maxSuffix)
                {
                    maxSuffix = suffixVal;
                }
            }
        }

        long nextSuffix = maxSuffix + 1;
        return parentAccountCode + nextSuffix.ToString().PadLeft(suffixLength, '0');
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

