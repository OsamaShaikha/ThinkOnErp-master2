using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

/// <summary>
/// Compiled queries for frequently executed database operations.
/// Compiled queries are pre-compiled and cached, improving performance for repeated executions.
/// These queries are used by repositories to optimize common read operations.
/// 
/// Performance Optimization: Compiled queries reduce query compilation overhead
/// by caching the query execution plan. This is especially beneficial for:
/// - Queries executed frequently (e.g., GetById, GetAll)
/// - Queries with complex LINQ expressions
/// - Queries in hot paths (e.g., authentication, dashboard)
/// 
/// NOTE: Some compiled queries are temporarily disabled due to type compatibility issues.
/// They will be re-enabled after proper testing and type alignment.
/// </summary>
public static class CompiledQueries
{
    #region Company Queries

    /// <summary>
    /// Compiled query to retrieve a company by ID with related entities.
    /// Eagerly loads Currency and DefaultBranch navigation properties.
    /// </summary>
    public static readonly Func<ThinkOnErpDbContext, long, Task<SysCompany?>> GetCompanyById =
        EF.CompileAsyncQuery((ThinkOnErpDbContext context, long rowId) =>
            context.Companies
                .AsNoTracking()
                .Include(c => c.Currency)
                .Include(c => c.DefaultBranch)
                .FirstOrDefault(c => c.RowId == rowId));

    // Temporarily disabled - type mismatch issue
    // public static readonly Func<ThinkOnErpDbContext, IAsyncEnumerable<SysCompany>> GetAllCompanies =
    //     EF.CompileAsyncQuery((ThinkOnErpDbContext context) =>
    //         context.Companies
    //             .AsNoTracking()
    //             .Include(c => c.Currency)
    //             .Include(c => c.DefaultBranch)
    //             .OrderBy(c => c.RowDesc));

    #endregion

    #region Branch Queries

    /// <summary>
    /// Compiled query to retrieve a branch by ID with related entities.
    /// Eagerly loads Company and BaseCurrency navigation properties.
    /// </summary>
    public static readonly Func<ThinkOnErpDbContext, long, Task<SysBranch?>> GetBranchById =
        EF.CompileAsyncQuery((ThinkOnErpDbContext context, long rowId) =>
            context.Branches
                .AsNoTracking()
                .Include(b => b.Company)
                .Include(b => b.BaseCurrency)
                .FirstOrDefault(b => b.RowId == rowId));

    /// <summary>
    /// Compiled query to retrieve branches by company ID.
    /// </summary>
    // Temporarily disabled - type mismatch issue
    // public static readonly Func<ThinkOnErpDbContext, long, IAsyncEnumerable<SysBranch>> GetBranchesByCompanyId =
    //     EF.CompileAsyncQuery((ThinkOnErpDbContext context, long companyId) =>
    //         context.Branches
    //             .AsNoTracking()
    //             .Include(b => b.Company)
    //             .Include(b => b.BaseCurrency)
    //             .Where(b => b.ParRowId == companyId)
    //             .OrderBy(b => b.RowDesc));

    #endregion

    #region User Queries

    /// <summary>
    /// Compiled query to retrieve a user by ID with related entities.
    /// Eagerly loads Company and Branch navigation properties.
    /// </summary>
    public static readonly Func<ThinkOnErpDbContext, long, Task<SysUser?>> GetUserById =
        EF.CompileAsyncQuery((ThinkOnErpDbContext context, long rowId) =>
            context.Users
                .AsNoTracking()
                .Include(u => u.Company)
                .Include(u => u.Branch)
                .FirstOrDefault(u => u.RowId == rowId));

    /// <summary>
    /// Compiled query to retrieve a user by username for authentication.
    /// Eagerly loads Company and Branch navigation properties.
    /// </summary>
    public static readonly Func<ThinkOnErpDbContext, string, Task<SysUser?>> GetUserByUsername =
        EF.CompileAsyncQuery((ThinkOnErpDbContext context, string username) =>
            context.Users
                .AsNoTracking()
                .Include(u => u.Company)
                .Include(u => u.Branch)
                .FirstOrDefault(u => u.UserName == username));

    /// <summary>
    /// Compiled query to retrieve a user by refresh token for token refresh operations.
    /// Eagerly loads Company and Branch navigation properties.
    /// </summary>
    public static readonly Func<ThinkOnErpDbContext, string, Task<SysUser?>> GetUserByRefreshToken =
        EF.CompileAsyncQuery((ThinkOnErpDbContext context, string refreshToken) =>
            context.Users
                .AsNoTracking()
                .Include(u => u.Company)
                .Include(u => u.Branch)
                .FirstOrDefault(u => u.RefreshToken == refreshToken));

    #endregion

    #region Currency Queries

    /// <summary>
    /// Compiled query to retrieve a currency by ID.
    /// </summary>
    public static readonly Func<ThinkOnErpDbContext, long, Task<SysCurrency?>> GetCurrencyById =
        EF.CompileAsyncQuery((ThinkOnErpDbContext context, long rowId) =>
            context.Currencies
                .AsNoTracking()
                .FirstOrDefault(c => c.RowId == rowId));

    /// <summary>
    /// Compiled query to retrieve all active currencies.
    /// </summary>
    // Temporarily disabled - type mismatch issue
    // public static readonly Func<ThinkOnErpDbContext, IAsyncEnumerable<SysCurrency>> GetAllCurrencies =
    //     EF.CompileAsyncQuery((ThinkOnErpDbContext context) =>
    //         context.Currencies
    //             .AsNoTracking()
    //             .OrderBy(c => c.RowDesc));

    #endregion

    #region Fiscal Year Queries

    /// <summary>
    /// Compiled query to retrieve a fiscal year by ID with related entities.
    /// Eagerly loads Company and Branch navigation properties.
    /// </summary>
    public static readonly Func<ThinkOnErpDbContext, long, Task<SysFiscalYear?>> GetFiscalYearById =
        EF.CompileAsyncQuery((ThinkOnErpDbContext context, long rowId) =>
            context.FiscalYears
                .AsNoTracking()
                .Include(f => f.Company)
                .Include(f => f.Branch)
                .FirstOrDefault(f => f.RowId == rowId));

    /// <summary>
    /// Compiled query to retrieve fiscal years by company ID.
    /// </summary>
    // Temporarily disabled - type mismatch issue
    // public static readonly Func<ThinkOnErpDbContext, long, IAsyncEnumerable<SysFiscalYear>> GetFiscalYearsByCompanyId =
    //     EF.CompileAsyncQuery((ThinkOnErpDbContext context, long companyId) =>
    //         context.FiscalYears
    //             .AsNoTracking()
    //             .Include(f => f.Company)
    //             .Include(f => f.Branch)
    //             .Where(f => f.CompanyId == companyId)
    //             .OrderByDescending(f => f.StartDate));

    /// <summary>
    /// Compiled query to retrieve fiscal years by branch ID.
    /// </summary>
    // Temporarily disabled - type mismatch issue
    // public static readonly Func<ThinkOnErpDbContext, long, IAsyncEnumerable<SysFiscalYear>> GetFiscalYearsByBranchId =
    //     EF.CompileAsyncQuery((ThinkOnErpDbContext context, long branchId) =>
    //         context.FiscalYears
    //             .AsNoTracking()
    //             .Include(f => f.Company)
    //             .Include(f => f.Branch)
    //             .Where(f => f.BranchId == branchId)
    //             .OrderByDescending(f => f.StartDate));

    #endregion

    #region Role Queries

    /// <summary>
    /// Compiled query to retrieve a role by ID.
    /// </summary>
    public static readonly Func<ThinkOnErpDbContext, long, Task<SysRole?>> GetRoleById =
        EF.CompileAsyncQuery((ThinkOnErpDbContext context, long rowId) =>
            context.Roles
                .AsNoTracking()
                .FirstOrDefault(r => r.RowId == rowId));

    /// <summary>
    /// Compiled query to retrieve all active roles.
    /// </summary>
    // Temporarily disabled - type mismatch issue
    // public static readonly Func<ThinkOnErpDbContext, IAsyncEnumerable<SysRole>> GetAllRoles =
    //     EF.CompileAsyncQuery((ThinkOnErpDbContext context) =>
    //         context.Roles
    //             .AsNoTracking()
    //             .OrderBy(r => r.RowDesc));

    #endregion

    #region Ticket Queries

    /// <summary>
    /// Compiled query to retrieve a ticket by ID with related entities.
    /// Uses split query to avoid cartesian explosion.
    /// </summary>
    public static readonly Func<ThinkOnErpDbContext, long, Task<SysRequestTicket?>> GetTicketById =
        EF.CompileAsyncQuery((ThinkOnErpDbContext context, long rowId) =>
            context.Tickets
                .AsNoTracking()
                .AsSplitQuery()
                .Include(t => t.Company)
                .Include(t => t.Branch)
                .Include(t => t.TicketType)
                .Include(t => t.TicketStatus)
                .Include(t => t.TicketPriority)
                .Include(t => t.TicketCategory)
                .Include(t => t.CreatedByUser)
                .Include(t => t.Assignee)
                .FirstOrDefault(t => t.RowId == rowId));

    /// <summary>
    /// Compiled query to retrieve tickets by company ID.
    /// </summary>
    // Temporarily disabled - type mismatch issue
    // public static readonly Func<ThinkOnErpDbContext, long, IAsyncEnumerable<SysRequestTicket>> GetTicketsByCompanyId =
    //     EF.CompileAsyncQuery((ThinkOnErpDbContext context, long companyId) =>
    //         context.Tickets
    //             .AsNoTracking()
    //             .AsSplitQuery()
    //             .Include(t => t.Company)
    //             .Include(t => t.Branch)
    //             .Include(t => t.TicketType)
    //             .Include(t => t.TicketStatus)
    //             .Include(t => t.TicketPriority)
    //             .Where(t => t.CompanyId == companyId)
    //             .OrderByDescending(t => t.CreationDate));

    #endregion

    #region Screen and Permission Queries

    /// <summary>
    /// Compiled query to retrieve user screen permissions by user ID.
    /// </summary>
    // Temporarily disabled - type mismatch issue
    // public static readonly Func<ThinkOnErpDbContext, long, IAsyncEnumerable<SysUserScreenPermission>> GetUserScreenPermissionsByUserId =
    //     EF.CompileAsyncQuery((ThinkOnErpDbContext context, long userId) =>
    //         context.UserScreenPermissions
    //             .AsNoTracking()
    //             .Include(p => p.User)
    //             .Include(p => p.Screen)
    //             .Where(p => p.UserId == userId)
    //             .OrderBy(p => p.Screen!.DisplayOrder));

    /// <summary>
    /// Compiled query to retrieve role screen permissions by role ID.
    /// </summary>
    // Temporarily disabled - type mismatch issue
    // public static readonly Func<ThinkOnErpDbContext, long, IAsyncEnumerable<SysRoleScreenPermission>> GetRoleScreenPermissionsByRoleId =
    //     EF.CompileAsyncQuery((ThinkOnErpDbContext context, long roleId) =>
    //         context.RoleScreenPermissions
    //             .AsNoTracking()
    //             .Include(p => p.Role)
    //             .Include(p => p.Screen)
    //             .Where(p => p.RoleId == roleId)
    //             .OrderBy(p => p.Screen!.DisplayOrder));

    /// <summary>
    /// Compiled query to retrieve user roles by user ID.
    /// </summary>
    // Temporarily disabled - type mismatch issue
    // public static readonly Func<ThinkOnErpDbContext, long, IAsyncEnumerable<SysUserRole>> GetUserRolesByUserId =
    //     EF.CompileAsyncQuery((ThinkOnErpDbContext context, long userId) =>
    //         context.UserRoles
    //             .AsNoTracking()
    //             .Include(ur => ur.User)
    //             .Include(ur => ur.Role)
    //             .Where(ur => ur.UserId == userId)
    //             .OrderBy(ur => ur.Role!.RowDesc));

    #endregion

    #region Ticket Type and Status Queries

    /// <summary>
    /// Compiled query to retrieve a ticket type by ID with default priority.
    /// </summary>
    public static readonly Func<ThinkOnErpDbContext, long, Task<SysTicketType?>> GetTicketTypeById =
        EF.CompileAsyncQuery((ThinkOnErpDbContext context, long rowId) =>
            context.TicketTypes
                .AsNoTracking()
                .Include(tt => tt.DefaultPriority)
                .FirstOrDefault(tt => tt.RowId == rowId));

    /// <summary>
    /// Compiled query to retrieve all active ticket types.
    /// </summary>
    // Temporarily disabled - type mismatch issue
    // public static readonly Func<ThinkOnErpDbContext, IAsyncEnumerable<SysTicketType>> GetAllTicketTypes =
    //     EF.CompileAsyncQuery((ThinkOnErpDbContext context) =>
    //         context.TicketTypes
    //             .AsNoTracking()
    //             .Include(tt => tt.DefaultPriority)
    //             .OrderBy(tt => tt.TypeNameEn));

    #endregion

    #region Saved Search Queries

    /// <summary>
    /// Compiled query to retrieve saved searches by user ID.
    /// Includes both user's private searches and public searches.
    /// </summary>
    // Temporarily disabled - type mismatch issue
    // public static readonly Func<ThinkOnErpDbContext, long, IAsyncEnumerable<SysSavedSearch>> GetSavedSearchesByUserId =
    //     EF.CompileAsyncQuery((ThinkOnErpDbContext context, long userId) =>
    //         context.SavedSearches
    //             .AsNoTracking()
    //             .Include(s => s.User)
    //             .Where(s => s.UserId == userId || s.IsPublic)
    //             .OrderBy(s => s.SearchName));

    #endregion
}
