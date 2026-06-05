using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly OracleDbContext _context;

    public CompanyRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<List<SysCompany>> GetAllAsync()
    {
        return await _context.SysCompanies
            .Where(c => c.IsActive)
            .ToListAsync();
    }

    public async Task<SysCompany?> GetByIdAsync(long rowId)
    {
        return await _context.SysCompanies.FindAsync(rowId);
    }

    public async Task<long> CreateAsync(SysCompany company)
    {
        _context.SysCompanies.Add(company);
        await _context.SaveChangesAsync();
        return company.Id;
    }

    public async Task<long> UpdateAsync(SysCompany company)
    {
        company.UpdateDate = DateTime.Now;
        _context.SysCompanies.Update(company);
        return await _context.SaveChangesAsync();
    }

    public async Task<long> DeleteAsync(long rowId)
    {
        var company = await _context.SysCompanies.FindAsync(rowId);
        if (company == null) return 0;
        
        company.IsActive = false;
        company.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<long> UpdateLogoPathAsync(long rowId, string? logoPath, string userName)
    {
        var company = await _context.SysCompanies.FindAsync(rowId);
        if (company == null) return 0;

        company.CompanyLogoPath = logoPath;
        company.UpdateUser = userName;
        company.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<string?> GetLogoPathAsync(long rowId)
    {
        return await _context.SysCompanies
            .Where(c => c.Id == rowId)
            .Select(c => c.CompanyLogoPath)
            .FirstOrDefaultAsync();
    }

    public async Task<(long CompanyId, long BranchId, long FiscalYearId)> CreateWithBranchAsync(
        string? companyNameAr, string companyNameEn,
        string? legalNameAr, string legalNameEn,
        string companyCode, string? taxNumber,
        long? countryId, long? currId,
        string? companyLogoPath, string? branchNameAr, string? branchNameEn,
        string? branchPhone, string? branchMobile,
        string? branchFax, string? branchEmail,
        string? branchLogoPath, int? defaultLang,
        long? baseCurrencyId, int? roundingRules,
        string? companySchema,
        string creationUser)
    {
        // Use a strategy pattern - create company, branch, and fiscal year in a transaction
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Create the company
            var company = new SysCompany
            {
                CompanyNameAr = companyNameAr ?? string.Empty,
                CompanyNameEn = companyNameEn,
                LegalName = legalNameAr,
                LegalNameE = legalNameEn,
                CompanyCode = companyCode,
                CompanySchema = companySchema,
                CountryId = countryId,
                CurrId = currId,
                CompanyLogoPath = companyLogoPath,
                IsActive = true,
                CreationUser = creationUser,
                CreationDate = DateTime.Now
            };
            _context.SysCompanies.Add(company);
            await _context.SaveChangesAsync();

            // 2. Create the branch
            var branch = new SysBranch
            {
                CompanyId = company.Id,
                BranchNameAr = branchNameAr ?? companyNameAr ?? "Default Branch",
                BranchNameEn = branchNameEn ?? companyNameEn ?? "Default Branch",
                Phone = branchPhone,
                Mobile = branchMobile,
                Fax = branchFax,
                Email = branchEmail,
                IsHeadBranch = true,
                TaxNumber = taxNumber,
                DefaultLang = defaultLang ?? 1,
                BaseCurrencyId = baseCurrencyId,
                RoundingRules = roundingRules ?? 1,
                BranchLogoPath = branchLogoPath,
                IsActive = true,
                CreationUser = creationUser,
                CreationDate = DateTime.Now
            };
            _context.SysBranches.Add(branch);
            await _context.SaveChangesAsync();

            // 3. Set default branch on company
            company.DefaultBranchId = branch.Id;
            company.UpdateUser = creationUser;
            company.UpdateDate = DateTime.Now;
            await _context.SaveChangesAsync();

            // 4. Create default fiscal year
            var fiscalYear = new SysFiscalYear
            {
                CompanyId = company.Id,
                BranchId = branch.Id,
                FiscalYearCode = $"FY{DateTime.Now.Year}",
                FiscalYearNameAr = $"Ø§Ù„Ø³Ù†Ø© Ø§Ù„Ù…Ø§Ù„ÙŠØ© {DateTime.Now.Year}",
                FiscalYearNameEn = $"Fiscal Year {DateTime.Now.Year}",
                StartDate = new DateTime(DateTime.Now.Year, 1, 1),
                EndDate = new DateTime(DateTime.Now.Year, 12, 31),
                IsClosed = false,
                IsActive = true,
                CreationUser = creationUser,
                CreationDate = DateTime.Now
            };
            _context.SysFiscalYears.Add(fiscalYear);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return (company.Id, branch.Id, fiscalYear.Id);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<long> SetDefaultBranchAsync(long companyId, long branchId, string userName)
    {
        var company = await _context.SysCompanies.FindAsync(companyId);
        if (company == null) return 0;
        
        company.DefaultBranchId = branchId;
        company.UpdateUser = userName;
        company.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }
}