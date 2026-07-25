using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly OracleDbContext _context;
    private readonly IOracleSchemaService _oracleSchemaService;

    public CompanyRepository(OracleDbContext context, IOracleSchemaService oracleSchemaService)
    {
        _context = context;
        _oracleSchemaService = oracleSchemaService;
    }

    public async Task<SysCompany?> GetBySchemaAsync(string companySchema)
    {
        return await _context.SysCompanies
            .FirstOrDefaultAsync(c => c.CompanySchema == companySchema);
    }

    public async Task<SysCompany?> GetByCodeAsync(string companyCode)
    {
        return await _context.SysCompanies
            .OrderBy(c => c.CompanySchema == null ? 1 : 0)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(c => c.CompanyCode == companyCode && c.IsActive);
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
        long? createdBySuperAdminId,
        string creationUser)
    {
        if (string.IsNullOrEmpty(companySchema))
        {
            throw new ArgumentException("Company schema name is required.", nameof(companySchema));
        }

        // Verify foreign key currency exists in master schema
        if (currId.HasValue && !await _context.SysCurrencies.AnyAsync(c => c.Id == currId.Value))
        {
            currId = null;
        }

        // 1. Create the company in the master schema
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
            CreatedBySuperAdminId = createdBySuperAdminId,
            CreationUser = creationUser,
            CreationDate = DateTime.Now
        };
        _context.SysCompanies.Add(company);
        await _context.SaveChangesAsync();

        // 2. Create the Oracle schema and clone objects from DEV_TEMPLATE
        await _oracleSchemaService.CreateCompanySchemaAsync(companySchema, companySchema);

        // 3. Provision the default branch and default fiscal year inside the tenant schema
        var (branchId, fiscalYearId) = await _oracleSchemaService.ProvisionTenantBranchAndFiscalYearAsync(
            schemaName: companySchema,
            schemaPassword: companySchema,
            companyId: company.Id,
            branchNameAr: branchNameAr,
            branchNameEn: branchNameEn,
            branchPhone: branchPhone,
            branchMobile: branchMobile,
            branchFax: branchFax,
            branchEmail: branchEmail,
            taxNumber: taxNumber,
            defaultLang: defaultLang ?? 1,
            baseCurrencyId: baseCurrencyId,
            roundingRules: roundingRules ?? 1,
            branchLogoPath: branchLogoPath,
            creationUser: creationUser);

        // 4. Update default branch ID on the company in the master schema
        company.DefaultBranchId = branchId;
        company.UpdateUser = creationUser;
        company.UpdateDate = DateTime.Now;
        await _context.SaveChangesAsync();

        return (company.Id, branchId, fiscalYearId);
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