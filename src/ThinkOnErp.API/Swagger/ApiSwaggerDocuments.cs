namespace ThinkOnErp.API.Swagger;

/// <summary>
/// Defines the available Swagger documents and assigns API controllers to them.
/// </summary>
public static class ApiSwaggerDocuments
{
    public const string SuperAdmin = "superadmin";
    public const string Company = "company";
    public const string Accounting = "accounting";

    private static readonly HashSet<string> SuperAdminControllers = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "SuperAdminAuth", "Alerts", "AuditHealth", "AuditTrail",
        "Company", "Health",
        "Features", "KeyManagement", "Modules", "Monitoring", "Screens", "SuperAdmin",
        "AuditLogs", "SysCodes", "SysSettings", "Currency"
    };

    private static readonly HashSet<string> AdditionalAccountingControllers = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "FiscalYear",
        "Currency"
    };

    /// <summary>
    /// Returns whether an API description belongs to the requested Swagger document.
    /// Tenant accounting APIs remain visible in the company document for backward compatibility.
    /// </summary>
    public static bool Includes(
        string documentName,
        string? controllerName,
        string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(controllerName))
        {
            return false;
        }

        if (string.Equals(documentName, Accounting, StringComparison.OrdinalIgnoreCase))
        {
            return IsAccountingApi(controllerName, relativePath);
        }

        var isDocumentsController = string.Equals(
            controllerName,
            "Documents",
            StringComparison.OrdinalIgnoreCase);

        if (string.Equals(documentName, SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return isDocumentsController || SuperAdminControllers.Contains(controllerName);
        }

        if (string.Equals(documentName, Company, StringComparison.OrdinalIgnoreCase))
        {
            return isDocumentsController || !SuperAdminControllers.Contains(controllerName);
        }

        return false;
    }

    private static bool IsAccountingApi(string controllerName, string? relativePath)
    {
        if (AdditionalAccountingControllers.Contains(controllerName))
        {
            return true;
        }

        const string accountingRoute = "api/accounting";
        if (string.IsNullOrWhiteSpace(relativePath) ||
            !relativePath.StartsWith(accountingRoute, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return relativePath.Length == accountingRoute.Length ||
               relativePath[accountingRoute.Length] == '/';
    }
}
