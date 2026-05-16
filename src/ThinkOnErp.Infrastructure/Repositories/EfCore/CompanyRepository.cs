using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysCompany entity using LINQ queries.
/// Implements ICompanyRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking and ID generation.
/// </summary>
public class CompanyRepository : ICompanyRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<CompanyRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the CompanyRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public CompanyRepository(
        ThinkOnErpDbContext context,
        ILogger<CompanyRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active companies from the database using compiled query.
    /// Uses AsNoTracking for read-only query optimization.
    /// Eagerly loads Currency and DefaultBranch navigation properties.
    /// Global query filter automatically filters out soft-deleted records (IsActive = false).
    /// 
    /// Performance Optimization: Uses direct LINQ query.
    /// </summary>
    /// <returns>A list of all active SysCompany entities ordered by RowDesc</returns>
    public async Task<List<SysCompany>> GetAllAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all companies");

                // Direct LINQ query (compiled query temporarily disabled)
                var companies = await _context.Companies
                    .AsNoTracking()
                    .Include(c => c.Currency)
                    .Include(c => c.DefaultBranch)
                    .OrderBy(c => c.RowDesc)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} companies", companies.Count);
                return companies;
            },
            "GetAllCompanies",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific company by its ID using compiled query.
    /// Uses AsNoTracking for read-only query optimization.
    /// Eagerly loads Currency and DefaultBranch navigation properties.
    /// Global query filter automatically filters out soft-deleted records (IsActive = false).
    /// 
    /// Performance Optimization: Uses compiled query for improved performance.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company</param>
    /// <returns>The SysCompany entity if found, null otherwise</returns>
    public async Task<SysCompany?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving company with ID: {RowId} using compiled query", rowId);

                // Use compiled query for better performance
                var company = await CompiledQueries.GetCompanyById(_context, rowId);

                if (company == null)
                {
                    _logger.LogDebug("Company with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved company: {RowDesc}", company.RowDesc);
                }

                return company;
            },
            "GetCompanyById",
            _logger);
    }

    /// <summary>
    /// Creates a new company in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_COMPANY.
    /// </summary>
    /// <param name="company">The company entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_COMPANY sequence</returns>
    public async Task<long> CreateAsync(SysCompany company)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new company: {RowDesc}", company.RowDesc);

                // Set creation date if not already set
                if (!company.CreationDate.HasValue)
                {
                    company.CreationDate = DateTime.Now;
                }

                // Set IsActive to true by default
                company.IsActive = true;

                // Add the entity to the context
                _context.Companies.Add(company);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created company with ID: {RowId}, Name: {RowDesc}, Code: {CompanyCode}", 
                    company.RowId, company.RowDesc, company.CompanyCode);

                return company.RowId;
            },
            "CreateCompany",
            _logger);
    }

    /// <summary>
    /// Updates an existing company in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="company">The company entity with updated values</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> UpdateAsync(SysCompany company)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating company with ID: {RowId}", company.RowId);

                // Set update date
                company.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.Companies.Update(company);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated company with ID: {RowId}, Name: {RowDesc}", 
                        company.RowId, company.RowDesc);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating company with ID: {RowId}", 
                        company.RowId);
                }

                return rowsAffected;
            },
            "UpdateCompany",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a company by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company to delete</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> DeleteAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Soft deleting company with ID: {RowId}", rowId);

                // Find the company to delete - need to ignore query filter to find it even if already deleted
                var company = await _context.Companies
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.RowId == rowId);

                if (company == null)
                {
                    _logger.LogWarning("Company with ID {RowId} not found for deletion", rowId);
                    return 0;
                }

                // Perform soft delete by setting IsActive to false
                company.IsActive = false;
                company.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted company with ID: {RowId}, Name: {RowDesc}", 
                        rowId, company.RowDesc);
                }

                return rowsAffected;
            },
            "DeleteCompany",
            _logger);
    }

    /// <summary>
    /// Updates the company logo by loading the entity, updating the CompanyLogo property, and saving.
    /// Uses LINQ projection to load only the necessary fields for efficient update.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company</param>
    /// <param name="logo">The logo image as byte array</param>
    /// <param name="userName">The username of the user updating the logo</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> UpdateLogoAsync(long rowId, byte[] logo, string userName)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating logo for company with ID: {RowId}", rowId);

                // Load the company entity (need tracking for update)
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.RowId == rowId);

                if (company == null)
                {
                    _logger.LogWarning("Company with ID {RowId} not found for logo update", rowId);
                    return 0;
                }

                // Update the logo and audit fields
                company.CompanyLogo = logo;
                company.UpdateUser = userName;
                company.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated logo for company with ID: {RowId}, Logo size: {Size} bytes", 
                        rowId, logo?.Length ?? 0);
                }

                return rowsAffected;
            },
            "UpdateCompanyLogo",
            _logger);
    }

    /// <summary>
    /// Retrieves the company logo using LINQ projection to select only the logo column.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company</param>
    /// <returns>The logo image as byte array, null if not found or no logo exists</returns>
    public async Task<byte[]?> GetLogoAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving logo for company with ID: {RowId}", rowId);

                // Use projection to select only the logo column for efficiency
                var logo = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.RowId == rowId)
                    .Select(c => c.CompanyLogo)
                    .FirstOrDefaultAsync();

                if (logo == null)
                {
                    _logger.LogDebug("Logo not found for company with ID {RowId}", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved logo for company with ID {RowId}, Size: {Size} bytes", 
                        rowId, logo.Length);
                }

                return logo;
            },
            "GetCompanyLogo",
            _logger);
    }

    /// <summary>
    /// Creates a new company with an automatic default branch and fiscal year in a single transaction.
    /// Uses EF Core transaction to ensure atomicity - all operations succeed or all fail.
    /// </summary>
    /// <param name="companyNameAr">Arabic name of the company</param>
    /// <param name="companyNameEn">English name of the company (required)</param>
    /// <param name="legalNameAr">Legal name in Arabic</param>
    /// <param name="legalNameEn">Legal name in English (required)</param>
    /// <param name="companyCode">Unique company code (required)</param>
    /// <param name="taxNumber">Tax registration number</param>
    /// <param name="countryId">Country ID</param>
    /// <param name="currId">Currency ID (legacy)</param>
    /// <param name="companyLogo">Company logo as byte array</param>
    /// <param name="branchNameAr">Arabic name for the default branch</param>
    /// <param name="branchNameEn">English name for the default branch</param>
    /// <param name="branchPhone">Branch phone number</param>
    /// <param name="branchMobile">Branch mobile number</param>
    /// <param name="branchFax">Branch fax number</param>
    /// <param name="branchEmail">Branch email address</param>
    /// <param name="branchLogo">Branch logo as byte array</param>
    /// <param name="defaultLang">Default language for the branch (ar/en)</param>
    /// <param name="baseCurrencyId">Base currency ID for the branch</param>
    /// <param name="roundingRules">Rounding rules for the branch</param>
    /// <param name="creationUser">Username of the user creating the records</param>
    /// <returns>A tuple containing the new company ID, branch ID, and fiscal year ID</returns>
    public async Task<(long CompanyId, long BranchId, long FiscalYearId)> CreateWithBranchAsync(
        string? companyNameAr,
        string companyNameEn,
        string? legalNameAr,
        string legalNameEn,
        string companyCode,
        string? taxNumber,
        long? countryId,
        long? currId,
        byte[]? companyLogo,
        string? branchNameAr,
        string? branchNameEn,
        string? branchPhone,
        string? branchMobile,
        string? branchFax,
        string? branchEmail,
        byte[]? branchLogo,
        string? defaultLang,
        long? baseCurrencyId,
        int? roundingRules,
        string creationUser)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating company with branch: {CompanyCode}", companyCode);

                // Begin transaction to ensure atomicity
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Step 1: Create the company
                    var company = new SysCompany
                    {
                        RowDesc = companyNameAr ?? companyNameEn,
                        RowDescE = companyNameEn,
                        LegalName = legalNameAr,
                        LegalNameE = legalNameEn,
                        CompanyCode = companyCode,
                        TaxNumber = taxNumber,
                        CountryId = countryId,
                        CurrId = currId,
                        CompanyLogo = companyLogo,
                        IsActive = true,
                        CreationUser = creationUser,
                        CreationDate = DateTime.Now
                    };

                    _context.Companies.Add(company);
                    await _context.SaveChangesAsync();

                    _logger.LogDebug("Created company with ID: {CompanyId}", company.RowId);

                    // Step 2: Create the default branch
                    var branch = new SysBranch
                    {
                        ParRowId = company.RowId,
                        RowDesc = branchNameAr ?? branchNameEn ?? companyNameAr ?? companyNameEn,
                        RowDescE = branchNameEn ?? companyNameEn,
                        Phone = branchPhone,
                        Mobile = branchMobile,
                        Fax = branchFax,
                        Email = branchEmail,
                        BranchLogo = branchLogo,
                        DefaultLang = defaultLang ?? "ar",
                        BaseCurrencyId = baseCurrencyId,
                        RoundingRules = roundingRules ?? 1,
                        IsHeadBranch = true,
                        IsActive = true,
                        CreationUser = creationUser,
                        CreationDate = DateTime.Now
                    };

                    _context.Branches.Add(branch);
                    await _context.SaveChangesAsync();

                    _logger.LogDebug("Created branch with ID: {BranchId}", branch.RowId);

                    // Step 3: Create the fiscal year for the branch
                    var currentYear = DateTime.Now.Year;
                    var fiscalYear = new SysFiscalYear
                    {
                        CompanyId = company.RowId,
                        BranchId = branch.RowId,
                        FiscalYearCode = $"FY{currentYear}",
                        RowDesc = $"{currentYear}",
                        RowDescE = $"{currentYear}",
                        StartDate = new DateTime(currentYear, 1, 1),
                        EndDate = new DateTime(currentYear, 12, 31),
                        IsClosed = false,
                        IsActive = true,
                        CreationUser = creationUser,
                        CreationDate = DateTime.Now
                    };

                    _context.FiscalYears.Add(fiscalYear);
                    await _context.SaveChangesAsync();

                    _logger.LogDebug("Created fiscal year with ID: {FiscalYearId}", fiscalYear.RowId);

                    // Step 4: Update the company to set the default branch
                    company.DefaultBranchId = branch.RowId;
                    company.UpdateUser = creationUser;
                    company.UpdateDate = DateTime.Now;
                    await _context.SaveChangesAsync();

                    _logger.LogDebug("Set default branch {BranchId} for company {CompanyId}", 
                        branch.RowId, company.RowId);

                    // Commit the transaction
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "Successfully created company with branch and fiscal year. " +
                        "CompanyId: {CompanyId}, BranchId: {BranchId}, FiscalYearId: {FiscalYearId}",
                        company.RowId, branch.RowId, fiscalYear.RowId);

                    return (company.RowId, branch.RowId, fiscalYear.RowId);
                }
                catch (Exception ex)
                {
                    // Rollback transaction on error
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Failed to create company with branch. Rolling back transaction.");
                    throw;
                }
            },
            "CreateCompanyWithBranch",
            _logger);
    }

    /// <summary>
    /// Sets the default branch for a company by loading the entity, updating DefaultBranchId, and saving.
    /// </summary>
    /// <param name="companyId">The unique identifier of the company</param>
    /// <param name="branchId">The unique identifier of the branch to set as default</param>
    /// <param name="userName">The username of the user making the change</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> SetDefaultBranchAsync(long companyId, long branchId, string userName)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Setting default branch {BranchId} for company {CompanyId}", 
                    branchId, companyId);

                // Load the company entity (need tracking for update)
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.RowId == companyId);

                if (company == null)
                {
                    _logger.LogWarning("Company with ID {CompanyId} not found", companyId);
                    return 0;
                }

                // Verify the branch exists and belongs to this company
                var branchExists = await _context.Branches
                    .AnyAsync(b => b.RowId == branchId && b.ParRowId == companyId);

                if (!branchExists)
                {
                    _logger.LogWarning("Branch with ID {BranchId} not found or does not belong to company {CompanyId}", 
                        branchId, companyId);
                    throw new InvalidOperationException(
                        $"Branch with ID {branchId} not found or does not belong to company {companyId}");
                }

                // Update the default branch
                company.DefaultBranchId = branchId;
                company.UpdateUser = userName;
                company.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Set default branch {BranchId} for company {CompanyId}", 
                        branchId, companyId);
                }

                return rowsAffected;
            },
            "SetDefaultBranch",
            _logger);
    }
}
