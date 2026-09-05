using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace ThinkOnErp.API.Swagger;

public class SwaggerCategoryModel
{
    public string CategoryCode { get; set; } = string.Empty;
    public string DisplayTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public List<string> ControllerNames { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class SwaggerEndpointModel
{
    public long Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string ControllerName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string RoutePath { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Loads API Categories and individual Endpoints directly from SYS_API_CATEGORIES and SYS_API_ENDPOINTS tables in Oracle DB.
/// </summary>
public class SwaggerCategoryLoader
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SwaggerCategoryLoader> _logger;
    private readonly List<SwaggerCategoryModel> _categories = new();
    private readonly Dictionary<string, string> _controllerToCategoryMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _endpointToCategoryMap = new(StringComparer.OrdinalIgnoreCase);
    private bool _isLoaded;
    private readonly object _lock = new();

    public SwaggerCategoryLoader(IConfiguration configuration, ILogger<SwaggerCategoryLoader> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public IReadOnlyList<SwaggerCategoryModel> GetCategories()
    {
        EnsureLoaded();
        return _categories;
    }

    public bool Includes(string documentName, string? groupName, string? controllerName, string? relativePath, string? httpMethod = null)
    {
        EnsureLoaded();

        // 1. Endpoint-level Database Mapping (SYS_API_ENDPOINTS)
        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            var normalizedRoute = relativePath.Trim('/');
            var endpointKey = string.IsNullOrWhiteSpace(httpMethod)
                ? normalizedRoute
                : $"{httpMethod.ToUpperInvariant()}:{normalizedRoute}";

            if (_endpointToCategoryMap.TryGetValue(endpointKey, out var targetCatByEndpoint))
            {
                return string.Equals(documentName, targetCatByEndpoint, StringComparison.OrdinalIgnoreCase);
            }

            // Fallback route key without method
            if (_endpointToCategoryMap.TryGetValue(normalizedRoute, out var targetCatByRoute))
            {
                return string.Equals(documentName, targetCatByRoute, StringComparison.OrdinalIgnoreCase);
            }
        }

        // 2. Explicit GroupName from [ApiExplorerSettings(GroupName = "...")]
        if (!string.IsNullOrWhiteSpace(groupName))
        {
            return string.Equals(documentName, groupName, StringComparison.OrdinalIgnoreCase);
        }

        // 3. Controller Name Mapping loaded from Oracle DB (SYS_API_CATEGORIES)
        if (!string.IsNullOrWhiteSpace(controllerName) &&
            _controllerToCategoryMap.TryGetValue(controllerName, out var targetCategory))
        {
            return string.Equals(documentName, targetCategory, StringComparison.OrdinalIgnoreCase);
        }

        // 4. Dynamic Route Fallback Conventions (Exclusive matching)
        if (relativePath?.StartsWith("api/accounting", StringComparison.OrdinalIgnoreCase) == true)
            return string.Equals(documentName, ApiCategories.Accounting, StringComparison.OrdinalIgnoreCase);

        if (relativePath?.StartsWith("api/superadmin", StringComparison.OrdinalIgnoreCase) == true)
            return string.Equals(documentName, ApiCategories.SuperAdmin, StringComparison.OrdinalIgnoreCase);

        if (relativePath?.StartsWith("api/auth", StringComparison.OrdinalIgnoreCase) == true)
            return string.Equals(documentName, ApiCategories.Auth, StringComparison.OrdinalIgnoreCase);

        if (relativePath?.StartsWith("api/companies", StringComparison.OrdinalIgnoreCase) == true || relativePath?.StartsWith("api/branches", StringComparison.OrdinalIgnoreCase) == true)
            return string.Equals(documentName, ApiCategories.Company, StringComparison.OrdinalIgnoreCase);

        if (relativePath?.StartsWith("api/audit", StringComparison.OrdinalIgnoreCase) == true)
            return string.Equals(documentName, ApiCategories.Audit, StringComparison.OrdinalIgnoreCase);

        if (relativePath?.StartsWith("api/tickets", StringComparison.OrdinalIgnoreCase) == true || relativePath?.StartsWith("api/support", StringComparison.OrdinalIgnoreCase) == true)
            return string.Equals(documentName, ApiCategories.Support, StringComparison.OrdinalIgnoreCase);

        if (relativePath?.StartsWith("api/inventory", StringComparison.OrdinalIgnoreCase) == true || relativePath?.StartsWith("api/documents", StringComparison.OrdinalIgnoreCase) == true)
            return string.Equals(documentName, ApiCategories.Inventory, StringComparison.OrdinalIgnoreCase);

        return string.Equals(documentName, ApiCategories.System, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Automatically scans discovered ASP.NET Core endpoints and persists them to SYS_API_ENDPOINTS in Oracle DB.
    /// </summary>
    public async Task SyncDiscoveredEndpointsAsync(IApiDescriptionGroupCollectionProvider apiExplorer)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        try
        {
            await using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync();

            const string mergeSql = @"
                MERGE INTO SYS_API_ENDPOINTS target
                USING (SELECT :catCode AS CATEGORY_CODE, :ctrlName AS CONTROLLER_NAME, :actName AS ACTION_NAME, :method AS HTTP_METHOD, :route AS ROUTE_PATH FROM DUAL) src
                ON (target.HTTP_METHOD = src.HTTP_METHOD AND target.ROUTE_PATH = src.ROUTE_PATH)
                WHEN MATCHED THEN
                    UPDATE SET target.CATEGORY_CODE = src.CATEGORY_CODE, target.CONTROLLER_NAME = src.CONTROLLER_NAME, target.ACTION_NAME = src.ACTION_NAME
                WHEN NOT MATCHED THEN
                    INSERT (ID, CATEGORY_CODE, CONTROLLER_NAME, ACTION_NAME, HTTP_METHOD, ROUTE_PATH, IS_ACTIVE)
                    VALUES (SEQ_SYS_API_ENDPOINTS.NEXTVAL, src.CATEGORY_CODE, src.CONTROLLER_NAME, src.ACTION_NAME, src.HTTP_METHOD, src.ROUTE_PATH, 1)";

            int syncedCount = 0;
            foreach (var group in apiExplorer.ApiDescriptionGroups.Items)
            {
                foreach (var api in group.Items)
                {
                    if (string.IsNullOrWhiteSpace(api.RelativePath)) continue;

                    var controller = api.ActionDescriptor.RouteValues["controller"] ?? "Unknown";
                    var action = api.ActionDescriptor.RouteValues["action"] ?? "Index";
                    var httpMethod = api.HttpMethod?.ToUpperInvariant() ?? "GET";
                    var routePath = api.RelativePath.Trim('/');

                    // Determine Category
                    var category = api.GroupName;
                    if (string.IsNullOrWhiteSpace(category) && _controllerToCategoryMap.TryGetValue(controller, out var mappedCat))
                    {
                        category = mappedCat;
                    }
                    if (string.IsNullOrWhiteSpace(category))
                    {
                        if (routePath.StartsWith("api/accounting", StringComparison.OrdinalIgnoreCase)) category = ApiCategories.Accounting;
                        else if (routePath.StartsWith("api/superadmin", StringComparison.OrdinalIgnoreCase)) category = ApiCategories.SuperAdmin;
                        else if (routePath.StartsWith("api/auth", StringComparison.OrdinalIgnoreCase)) category = ApiCategories.Auth;
                        else if (routePath.StartsWith("api/companies", StringComparison.OrdinalIgnoreCase)) category = ApiCategories.Company;
                        else category = ApiCategories.System;
                    }

                    await using var cmd = new OracleCommand(mergeSql, connection);
                    cmd.Parameters.Add(new OracleParameter("catCode", category));
                    cmd.Parameters.Add(new OracleParameter("ctrlName", controller));
                    cmd.Parameters.Add(new OracleParameter("actName", action));
                    cmd.Parameters.Add(new OracleParameter("method", httpMethod));
                    cmd.Parameters.Add(new OracleParameter("route", routePath));

                    await cmd.ExecuteNonQueryAsync();
                    syncedCount++;
                }
            }

            _logger.LogInformation("Successfully synced {Count} API endpoints to Oracle DB SYS_API_ENDPOINTS table.", syncedCount);
            
            // Reload cached maps
            lock (_lock)
            {
                _isLoaded = false;
                EnsureLoaded();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-sync endpoints to SYS_API_ENDPOINTS table.");
        }
    }

    private void EnsureLoaded()
    {
        if (_isLoaded) return;

        lock (_lock)
        {
            if (_isLoaded) return;

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    using var connection = new OracleConnection(connectionString);
                    connection.Open();

                    // 1. Load Categories
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT CATEGORY_CODE, DISPLAY_TITLE, DESCRIPTION, DISPLAY_ORDER, CONTROLLER_NAMES, IS_ACTIVE FROM SYS_API_CATEGORIES WHERE IS_ACTIVE = 1 ORDER BY DISPLAY_ORDER ASC";
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            var catCode = reader.GetString(0);
                            var title = reader.GetString(1);
                            var desc = reader.IsDBNull(2) ? null : reader.GetString(2);
                            var order = reader.GetInt32(3);
                            var controllersRaw = reader.IsDBNull(4) ? null : reader.GetString(4);

                            var controllerList = controllersRaw?
                                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .ToList() ?? new List<string>();

                            _categories.Add(new SwaggerCategoryModel
                            {
                                CategoryCode = catCode,
                                DisplayTitle = title,
                                Description = desc,
                                DisplayOrder = order,
                                ControllerNames = controllerList,
                                IsActive = true
                            });

                            foreach (var ctrlName in controllerList)
                            {
                                var normalizedCtrlName = ctrlName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
                                    ? ctrlName[..^10]
                                    : ctrlName;

                                _controllerToCategoryMap[ctrlName] = catCode;
                                _controllerToCategoryMap[normalizedCtrlName] = catCode;
                            }
                        }
                    }

                    // 2. Load Endpoint-level Mappings from SYS_API_ENDPOINTS
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT CATEGORY_CODE, HTTP_METHOD, ROUTE_PATH, CONTROLLER_NAME FROM SYS_API_ENDPOINTS WHERE IS_ACTIVE = 1";
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            var catCode = reader.GetString(0);
                            var method = reader.GetString(1).ToUpperInvariant();
                            var route = reader.GetString(2).Trim('/');
                            var ctrlName = reader.IsDBNull(3) ? null : reader.GetString(3);

                            var keyWithMethod = $"{method}:{route}";
                            _endpointToCategoryMap[keyWithMethod] = catCode;
                            _endpointToCategoryMap[route] = catCode;

                            if (!string.IsNullOrEmpty(ctrlName))
                            {
                                _controllerToCategoryMap[ctrlName] = catCode;
                            }
                        }
                    }

                    if (_categories.Count > 0)
                    {
                        _logger.LogInformation("Loaded {CategoriesCount} categories and {EndpointsCount} endpoints from Oracle DB.", _categories.Count, _endpointToCategoryMap.Count);
                        _isLoaded = true;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load SYS_API_CATEGORIES/ENDPOINTS from Oracle DB. Using default fallbacks.");
            }

            LoadDefaultFallbackCategories();
            _isLoaded = true;
        }
    }

    private void LoadDefaultFallbackCategories()
    {
        _categories.Clear();
        _controllerToCategoryMap.Clear();

        var defaults = new List<(string Code, string Title, string Desc, int Order, string Controllers)>
        {
            (ApiCategories.SuperAdmin, "1. SuperAdmin API", "Platform-wide management: super admin accounts, company/branch registry, schema provisioning & sync.", 1, "SuperAdminController,SuperAdminAuthController"),
            (ApiCategories.Company, "2. Company & Tenant Management API", "Tenant-specific company management: company registration, default branch configuration, branch access, company permissions, and DEV_TEMPLATE schema export.", 2, "CompanyController,BranchController,BranchAccessController,CompanyPermissionsController"),
            (ApiCategories.Accounting, "3. Accounting & COA API", "Accounting operations: chart of accounts CRUD, level 1/2 categories, next child code generator, COA Excel validation/import, postable accounts, fiscal years, vouchers, receipts, payments, customers, vendors, PDC cheques, bank accounts, cash registers, bank reconciliations, fiscal closing, opening balances, account statements, and financial reports.", 3, "GlAccountsController,AccountCategoriesController,CoaImportController,CurrencyController,FiscalYearController,CostCentersController,GlVouchersController,ReceiptsController,PaymentsController,CustomersController,VendorsController,PdcController,PdcRegisterController,BankAccountsController,CashRegistersController,BankReconciliationController,PostingRulesController,AccountBalancesController,FiscalClosingController,SubledgerController,GlReportsController,FinancialReportsController,GlAccountStructureController,VoucherTypesController,OpeningBalancesController,AccountStatementController"),
            (ApiCategories.Auth, "4. Auth & Security API", "Authentication and Authorization: login, JWT tokens, user management, roles, and fine-grained permissions.", 4, "AuthController,UsersController,RolesController,PermissionsController"),
            (ApiCategories.Audit, "5. Audit & Security Monitoring API", "Audit and monitoring: audit logs, entity audit trail, audit health, threat alerts, performance metrics, compliance reporting, and key management.", 5, "AuditLogsController,AuditTrailController,AuditHealthController,AlertsController,MonitoringController,ComplianceController,KeyManagementController"),
            (ApiCategories.Support, "6. Tickets & Support API", "Customer support & ticket management: support tickets, ticket status, and ticket types.", 6, "TicketsController,TicketTypesController"),
            (ApiCategories.System, "7. System", "System configuration & metadata: system lookup codes, global settings, modules, screens, feature toggles, health checks, document uploads, and saved searches.", 7, "SysCodesController,SysSettingsController,ModulesController,ScreensController,FeaturesController,HealthController,DocumentsController,ConfigurationController,SavedSearchesController"),
            (ApiCategories.Inventory, "8. Inventory & Trade Documents API", "Inventory management, items master, main/sub groups, BOM kits & assemblies, warehouses, stock movements, FIFO costing, and universal trade documents.", 8, "TrxDocumentsController,InvItemsController,InvItemGroupsController,InvBomController,InvWarehousesController,InvStockController,InvOpeningBalancesController")
        };

        foreach (var d in defaults)
        {
            var controllerList = d.Controllers.Split(',').ToList();
            _categories.Add(new SwaggerCategoryModel
            {
                CategoryCode = d.Code,
                DisplayTitle = d.Title,
                Description = d.Desc,
                DisplayOrder = d.Order,
                ControllerNames = controllerList
            });

            foreach (var ctrlName in controllerList)
            {
                var normalizedCtrlName = ctrlName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
                    ? ctrlName[..^10]
                    : ctrlName;

                _controllerToCategoryMap[ctrlName] = d.Code;
                _controllerToCategoryMap[normalizedCtrlName] = d.Code;
            }
        }
    }
}
