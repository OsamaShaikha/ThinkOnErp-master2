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
        if (!string.IsNullOrEmpty(schema))
        {
            try
            {
                // Open connection explicitly so EF Core keeps it open for the request
                await dbContext.Database.OpenConnectionAsync();
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
