<<<<<<< Updated upstream
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System.Data;
=======
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
>>>>>>> Stashed changes

namespace ThinkOnErp.Infrastructure.Data;

/// <summary>
<<<<<<< Updated upstream
/// Manages Oracle database connections for the ThinkOnErp API.
/// Reads connection string from configuration and provides connection creation method.
/// Supports audit logging through command interception.
/// Implements IDisposable for proper resource cleanup.
=======
/// Entity Framework Core database context for the ThinkOnErp ERP system.
/// Maps all domain entities to Oracle database tables using Fluent API configuration.
>>>>>>> Stashed changes
/// </summary>
public class ThinkOnErpDbContext : DbContext
{
<<<<<<< Updated upstream
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
=======
    public ThinkOnErpDbContext(DbContextOptions<ThinkOnErpDbContext> options) : base(options)
>>>>>>> Stashed changes
    {
    }

    // Core entities
    public DbSet<SysRole> SysRoles => Set<SysRole>();
    public DbSet<SysCurrency> SysCurrencies => Set<SysCurrency>();
    public DbSet<SysCompany> SysCompanies => Set<SysCompany>();
    public DbSet<SysBranch> SysBranches => Set<SysBranch>();
    public DbSet<SysUser> SysUsers => Set<SysUser>();
    public DbSet<SysFiscalYear> SysFiscalYears => Set<SysFiscalYear>();

    // Permission system entities
    public DbSet<SysSuperAdmin> SysSuperAdmins => Set<SysSuperAdmin>();
    public DbSet<SysSystem> SysSystems => Set<SysSystem>();
    public DbSet<SysScreen> SysScreens => Set<SysScreen>();
    public DbSet<SysUserRole> SysUserRoles => Set<SysUserRole>();
    public DbSet<SysRoleScreenPermission> SysRoleScreenPermissions => Set<SysRoleScreenPermission>();
    public DbSet<SysUserScreenPermission> SysUserScreenPermissions => Set<SysUserScreenPermission>();
    public DbSet<SysCompanySystem> SysCompanySystems => Set<SysCompanySystem>();
    public DbSet<SysBranchSystem> SysBranchSystems => Set<SysBranchSystem>();
    public DbSet<SysBranchScreenPermission> SysBranchScreenPermissions => Set<SysBranchScreenPermission>();
    public DbSet<SysCompanyScreenPermission> SysCompanyScreenPermissions => Set<SysCompanyScreenPermission>();

    // Ticket system entities
    public DbSet<SysRequestTicket> SysRequestTickets => Set<SysRequestTicket>();
    public DbSet<SysTicketType> SysTicketTypes => Set<SysTicketType>();
    public DbSet<SysTicketPriority> SysTicketPriorities => Set<SysTicketPriority>();
    public DbSet<SysTicketStatus> SysTicketStatuses => Set<SysTicketStatus>();
    public DbSet<SysTicketCategory> SysTicketCategories => Set<SysTicketCategory>();
    public DbSet<SysTicketComment> SysTicketComments => Set<SysTicketComment>();
    public DbSet<SysTicketAttachment> SysTicketAttachments => Set<SysTicketAttachment>();
    public DbSet<SysTicketConfig> SysTicketConfigs => Set<SysTicketConfig>();

    // Saved search / search analytics
    public DbSet<SysSavedSearch> SysSavedSearches => Set<SysSavedSearch>();
    public DbSet<SysSearchAnalytics> SysSearchAnalytics => Set<SysSearchAnalytics>();

    // Audit log
    public DbSet<SysAuditLog> SysAuditLogs => Set<SysAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations
        modelBuilder.ApplyConfiguration(new SysRoleConfiguration());
        modelBuilder.ApplyConfiguration(new SysCurrencyConfiguration());
        modelBuilder.ApplyConfiguration(new SysCompanyConfiguration());
        modelBuilder.ApplyConfiguration(new SysBranchConfiguration());
        modelBuilder.ApplyConfiguration(new SysUserConfiguration());
        modelBuilder.ApplyConfiguration(new SysFiscalYearConfiguration());
        modelBuilder.ApplyConfiguration(new SysSuperAdminConfiguration());
        modelBuilder.ApplyConfiguration(new SysSystemConfiguration());
        modelBuilder.ApplyConfiguration(new SysScreenConfiguration());
        modelBuilder.ApplyConfiguration(new SysUserRoleConfiguration());
        modelBuilder.ApplyConfiguration(new SysRoleScreenPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new SysUserScreenPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new SysCompanySystemConfiguration());
        modelBuilder.ApplyConfiguration(new SysBranchSystemConfiguration());
        modelBuilder.ApplyConfiguration(new SysBranchScreenPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new SysCompanyScreenPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new SysRequestTicketConfiguration());
        modelBuilder.ApplyConfiguration(new SysTicketTypeConfiguration());
        modelBuilder.ApplyConfiguration(new SysTicketPriorityConfiguration());
        modelBuilder.ApplyConfiguration(new SysTicketStatusConfiguration());
        modelBuilder.ApplyConfiguration(new SysTicketCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new SysTicketCommentConfiguration());
        modelBuilder.ApplyConfiguration(new SysTicketAttachmentConfiguration());
        modelBuilder.ApplyConfiguration(new SysTicketConfigConfiguration());
        modelBuilder.ApplyConfiguration(new SysSavedSearchConfiguration());
        modelBuilder.ApplyConfiguration(new SysSearchAnalyticsConfiguration());
        modelBuilder.ApplyConfiguration(new SysAuditLogConfiguration());
    }

    /// <summary>
<<<<<<< Updated upstream
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
    /// Returns an auditable connection wrapper when the audit interceptor is configured.
    /// The caller is responsible for opening and disposing the connection.
=======
    /// Configures global query filters and conventions
>>>>>>> Stashed changes
    /// </summary>
    /// <returns>A new OracleConnection instance (or AuditableOracleConnection if interceptor is configured).</returns>
    public OracleConnection CreateConnection()
    {
        var connection = new OracleConnection(_connectionString);
        return connection;
    }
<<<<<<< Updated upstream

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
=======
}
>>>>>>> Stashed changes
