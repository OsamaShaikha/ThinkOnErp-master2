using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysCompany entity data access operations.
/// Defines the contract for company management in the Domain layer with zero external dependencies.
/// </summary>
public interface ICompanyRepository
{
    /// <summary>
    /// Retrieves all active companies from the database.
    /// Calls SP_SYS_COMPANY_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysCompany entities</returns>
    Task<List<SysCompany>> GetAllAsync();

    /// <summary>
    /// Retrieves a specific company by its ID.
    /// Calls SP_SYS_COMPANY_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company</param>
    /// <returns>The SysCompany entity if found, null otherwise</returns>
    Task<SysCompany?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Creates a new company in the database.
    /// Calls SP_SYS_COMPANY_INSERT stored procedure.
    /// </summary>
    /// <param name="company">The company entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_COMPANY sequence</returns>
    Task<Int64> CreateAsync(SysCompany company);

    /// <summary>
    /// Updates an existing company in the database.
    /// Calls SP_SYS_COMPANY_UPDATE stored procedure.
    /// </summary>
    /// <param name="company">The company entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateAsync(SysCompany company);

    /// <summary>
    /// Performs a soft delete on a company by setting IS_ACTIVE to false.
    /// Calls SP_SYS_COMPANY_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company to delete</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> DeleteAsync(Int64 rowId);

    /// <summary>
    /// Updates the company logo.
    /// Calls SP_SYS_COMPANY_UPDATE_LOGO stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company</param>
    /// <param name="logo">The logo image as byte array</param>
    /// <param name="userName">The username of the user updating the logo</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateLogoAsync(Int64 rowId, byte[] logo, string userName);

    /// <summary>
    /// Retrieves the company logo.
    /// Calls SP_SYS_COMPANY_GET_LOGO stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company</param>
    /// <returns>The logo image as byte array, null if not found</returns>
    Task<byte[]?> GetLogoAsync(Int64 rowId);

    /// <summary>
    /// Creates a new company with an automatic default branch in a single transaction.
    /// Calls SP_SYS_COMPANY_INSERT_WITH_BRANCH stored procedure.
    /// </summary>
    /// <param name="companyNameAr">Arabic name of the company</param>
    /// <param name="companyNameEn">English name of the company (required)</param>
    /// <param name="legalNameAr">Legal name in Arabic</param>
    /// <param name="legalNameEn">Legal name in English (required)</param>
    /// <param name="companyCode">Unique company code (required)</param>
    /// <param name="defaultLang">Default language (ar/en)</param>
    /// <param name="taxNumber">Tax registration number</param>
    /// <param name="fiscalYearId">Current fiscal year ID</param>
    /// <param name="baseCurrencyId">Base currency ID</param>
    /// <param name="systemLanguage">System language (ar/en)</param>
    /// <param name="roundingRules">Rounding rules</param>
    /// <param name="countryId">Country ID</param>
    /// <param name="currId">Currency ID (legacy)</param>
    /// <param name="branchNameAr">Arabic name for the default branch</param>
    /// <param name="branchNameEn">English name for the default branch</param>
    /// <param name="branchPhone">Branch phone number</param>
    /// <param name="branchMobile">Branch mobile number</param>
    /// <param name="branchFax">Branch fax number</param>
    /// <param name="branchEmail">Branch email address</param>
    /// <param name="branchLogo">Branch logo as byte array</param>
    /// <param name="creationUser">Username of the user creating the records</param>
    /// <returns>A tuple containing the new company ID and branch ID</returns>
    Task<(Int64 CompanyId, Int64 BranchId)> CreateWithBranchAsync(
        string? companyNameAr,
        string companyNameEn,
        string? legalNameAr,
        string legalNameEn,
        string companyCode,
        string? defaultLang,
        string? taxNumber,
        Int64? fiscalYearId,
        Int64? baseCurrencyId,
        string? systemLanguage,
        string? roundingRules,
        Int64? countryId,
        Int64? currId,
        string? branchNameAr,
        string? branchNameEn,
        string? branchPhone,
        string? branchMobile,
        string? branchFax,
        string? branchEmail,
        byte[]? branchLogo,
        string creationUser);

    /// <summary>
    /// Sets the default branch for a company.
    /// Calls SP_SYS_COMPANY_SET_DEFAULT_BRANCH stored procedure.
    /// </summary>
    /// <param name="companyId">The unique identifier of the company</param>
    /// <param name="branchId">The unique identifier of the branch to set as default</param>
    /// <param name="userName">The username of the user making the change</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> SetDefaultBranchAsync(Int64 companyId, Int64 branchId, string userName);
}
