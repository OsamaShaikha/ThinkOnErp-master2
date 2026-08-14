namespace ThinkOnErp.API.Swagger;

/// <summary>
/// Dynamic Swagger document predicate evaluator mapping endpoints to module categories.
/// </summary>
public static class ApiSwaggerDocuments
{
    public const string SuperAdmin = ApiCategories.SuperAdmin;
    public const string Company = ApiCategories.Company;
    public const string Accounting = ApiCategories.Accounting;
    public const string Auth = ApiCategories.Auth;
    public const string Audit = ApiCategories.Audit;
    public const string Support = ApiCategories.Support;
    public const string System = ApiCategories.System;

    private static readonly Dictionary<string, string> ControllerToCategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // SuperAdmin
        ["SuperAdmin"] = SuperAdmin,
        ["SuperAdminAuth"] = SuperAdmin,

        // Company
        ["Company"] = Company,
        ["Branch"] = Company,
        ["BranchAccess"] = Company,
        ["CompanyPermissions"] = Company,

        // Accounting
        ["GlAccounts"] = Accounting,
        ["AccountCategories"] = Accounting,
        ["CoaImport"] = Accounting,
        ["Currency"] = Accounting,
        ["FiscalYear"] = Accounting,

        // Auth & User Management
        ["Auth"] = Auth,
        ["Users"] = Auth,
        ["Roles"] = Auth,
        ["Permissions"] = Auth,

        // Audit & Security Monitoring
        ["AuditLogs"] = Audit,
        ["AuditTrail"] = Audit,
        ["AuditHealth"] = Audit,
        ["Alerts"] = Audit,
        ["Monitoring"] = Audit,
        ["Compliance"] = Audit,
        ["KeyManagement"] = Audit,

        // Support & Ticketing
        ["Tickets"] = Support,
        ["TicketTypes"] = Support,

        // System Settings & Metadata
        ["SysCodes"] = System,
        ["SysSettings"] = System,
        ["Modules"] = System,
        ["Screens"] = System,
        ["Features"] = System,
        ["Health"] = System,
        ["Documents"] = System,
        ["Configuration"] = System,
        ["SavedSearches"] = System
    };

    /// <summary>
    /// Evaluates whether an endpoint belongs to the specified Swagger document tab.
    /// </summary>
    public static bool Includes(
        string documentName,
        string? groupName,
        string? controllerName,
        string? relativePath)
    {
        // 1. Explicit GroupName from [ApiExplorerSettings(GroupName = "...")]
        if (!string.IsNullOrWhiteSpace(groupName))
        {
            return string.Equals(documentName, groupName, StringComparison.OrdinalIgnoreCase);
        }

        // 2. Controller Name Mapping
        if (!string.IsNullOrWhiteSpace(controllerName) &&
            ControllerToCategoryMap.TryGetValue(controllerName, out var targetCategory))
        {
            return string.Equals(documentName, targetCategory, StringComparison.OrdinalIgnoreCase);
        }

        // 3. Fallback Route matching
        if (string.Equals(documentName, Accounting, StringComparison.OrdinalIgnoreCase) && relativePath?.StartsWith("api/accounting", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        if (string.Equals(documentName, SuperAdmin, StringComparison.OrdinalIgnoreCase) && relativePath?.StartsWith("api/superadmin", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        if (string.Equals(documentName, Auth, StringComparison.OrdinalIgnoreCase) && relativePath?.StartsWith("api/auth", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        if (string.Equals(documentName, Company, StringComparison.OrdinalIgnoreCase) && relativePath?.StartsWith("api/companies", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        // Default fallback to System document
        return string.Equals(documentName, System, StringComparison.OrdinalIgnoreCase);
    }
}
