using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.API.Middleware;

public class SchemaRoutingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SchemaRoutingMiddleware> _logger;
    private readonly string _connectionString;

    public SchemaRoutingMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<SchemaRoutingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("Connection string 'OracleDb' not found.");
    }

    public async Task InvokeAsync(HttpContext context, OracleDbContext dbContext)
    {
        var schema = context.User?.FindFirst("companySchema")?.Value;
        
        // If the user is SuperAdmin and there is no companySchema claim, check if they are requesting a company-specific resource
        if (string.IsNullOrEmpty(schema) && 
            string.Equals(context.User?.FindFirst("isSuperAdmin")?.Value, "true", StringComparison.OrdinalIgnoreCase))
        {
            long? companyId = null;
            
            // 1. Check Route Values
            var pathParts = context.Request.Path.Value?.Split('/');
            if (pathParts != null)
            {
                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    if (string.Equals(pathParts[i], "company", StringComparison.OrdinalIgnoreCase))
                    {
                        if (long.TryParse(pathParts[i + 1], out var id))
                        {
                            companyId = id;
                            break;
                        }
                    }
                }
            }
            
            // 2. Check Query String
            if (!companyId.HasValue && context.Request.Query.TryGetValue("companyId", out var qIdStr))
            {
                if (long.TryParse(qIdStr, out var id))
                {
                    companyId = id;
                }
            }

            // 3. Check Headers
            if (!companyId.HasValue && context.Request.Headers.TryGetValue("X-Company-Id", out var hIdStr))
            {
                if (long.TryParse(hIdStr, out var id))
                {
                    companyId = id;
                }
            }

            string? companyCode = null;
            if (!companyId.HasValue && context.Request.Headers.TryGetValue("X-Company-Code", out var hCodeStr))
            {
                companyCode = hCodeStr.ToString();
            }
            
            if (companyId.HasValue || !string.IsNullOrEmpty(companyCode))
            {
                try
                {
                    // Open connection explicitly first so the query and session alteration share the same connection
                    await dbContext.Database.OpenConnectionAsync();

                    if (companyId.HasValue)
                    {
                        var company = await dbContext.SysCompanies
                            .FirstOrDefaultAsync(c => c.Id == companyId.Value);
                        if (company != null && !string.IsNullOrEmpty(company.CompanySchema))
                        {
                            schema = company.CompanySchema;
                        }
                    }
                    else if (!string.IsNullOrEmpty(companyCode))
                    {
                        var company = await dbContext.SysCompanies
                            .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);
                        if (company != null && !string.IsNullOrEmpty(company.CompanySchema))
                        {
                            schema = company.CompanySchema;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking company schema for SuperAdmin routing");
                }
            }
        }

        if (!string.IsNullOrEmpty(schema))
        {
            try
            {
                // Ensure connection is open
                if (dbContext.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
                {
                    await dbContext.Database.OpenConnectionAsync();
                }
                
                await dbContext.Database.ExecuteSqlRawAsync(
                    $"ALTER SESSION SET CURRENT_SCHEMA = \"{schema}\"");

                _logger.LogDebug("Schema routing set to: {Schema}", schema);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to route schema to: {Schema}", schema);
                throw;
            }
        }
        await _next(context);
    }
}
