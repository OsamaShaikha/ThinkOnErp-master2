using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace ThinkOnErp.Infrastructure.Data;

/// <summary>
/// Manages Oracle database connections for the ThinkOnErp API.
/// Reads connection string from configuration and provides connection creation method.
/// Supports audit logging through command interception.
/// Implements IDisposable for proper resource cleanup.
/// </summary>
public class OracleDbContext : IDisposable
{
    private readonly string _connectionString;
    private readonly AuditCommandInterceptor? _auditInterceptor;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the OracleDbContext class.
    /// </summary>
    /// <param name="configuration">The configuration instance to read connection string from.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection string is not found in configuration.</exception>
    public OracleDbContext(IConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        _connectionString = configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("Oracle connection string 'OracleDb' not found in configuration.");
    }

    /// <summary>
    /// Initializes a new instance of the OracleDbContext class with audit interception support.
    /// </summary>
    /// <param name="configuration">The configuration instance to read connection string from.</param>
    /// <param name="auditInterceptor">The audit command interceptor for automatic audit logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection string is not found in configuration.</exception>
    public OracleDbContext(IConfiguration configuration, AuditCommandInterceptor auditInterceptor)
        : this(configuration)
    {
        _auditInterceptor = auditInterceptor;
    }

    /// <summary>
    /// Creates and returns a new Oracle database connection.
    /// The caller is responsible for opening and disposing the connection.
    /// </summary>
    /// <returns>A new OracleConnection instance.</returns>
    public OracleConnection CreateConnection()
    {
        return new OracleConnection(_connectionString);
    }

    /// <summary>
    /// Creates and returns a new auditable Oracle database connection.
    /// Commands executed through this connection will be automatically logged to the audit trail.
    /// The caller is responsible for opening and disposing the connection.
    /// </summary>
    /// <returns>A new AuditableOracleConnection instance that wraps an OracleConnection.</returns>
    /// <exception cref="InvalidOperationException">Thrown when audit interceptor is not configured.</exception>
    public IDbConnection CreateAuditableConnection()
    {
        if (_auditInterceptor == null)
        {
            throw new InvalidOperationException(
                "Audit interceptor is not configured. Use the constructor that accepts AuditCommandInterceptor.");
        }

        var connection = new OracleConnection(_connectionString);
        return new AuditableOracleConnection(connection, _auditInterceptor);
    }

    /// <summary>
    /// Disposes the OracleDbContext and releases any resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // No managed resources to dispose in this class
                // Connections are created and disposed by repositories
            }

            _disposed = true;
        }
    }
}
