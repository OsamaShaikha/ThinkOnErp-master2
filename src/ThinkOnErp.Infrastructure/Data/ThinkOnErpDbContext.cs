using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

/// <summary>
/// Entity Framework Core DbContext for the ThinkOnErp ERP system.
/// Manages database connections, entity tracking, and query translation for Oracle database.
/// Configured with QueryTrackingBehavior.NoTracking by default for read-only queries.
/// </summary>
public class ThinkOnErpDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the ThinkOnErpDbContext with the specified options.
    /// </summary>
    /// <param name="options">The options to be used by the DbContext.</param>
    public ThinkOnErpDbContext(DbContextOptions<ThinkOnErpDbContext> options)
        : base(options)
    {
    }

    #region Core Entity DbSets

    /// <summary>
    /// Gets or sets the Companies entity set.
    /// Maps to SYS_COMPANY table.
    /// </summary>
    public DbSet<SysCompany> Companies { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Branches entity set.
    /// Maps to SYS_BRANCH table.
    /// </summary>
    public DbSet<SysBranch> Branches { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Users entity set.
    /// Maps to SYS_USERS table.
    /// </summary>
    public DbSet<SysUser> Users { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Roles entity set.
    /// Maps to SYS_ROLE table.
    /// </summary>
    public DbSet<SysRole> Roles { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Currencies entity set.
    /// Maps to SYS_CURRENCY table.
    /// </summary>
    public DbSet<SysCurrency> Currencies { get; set; } = null!;

    /// <summary>
    /// Gets or sets the FiscalYears entity set.
    /// Maps to SYS_FISCAL_YEAR table.
    /// </summary>
    public DbSet<SysFiscalYear> FiscalYears { get; set; } = null!;

    #endregion

    #region Permission Entity DbSets

    /// <summary>
    /// Gets or sets the RoleScreenPermissions entity set.
    /// Maps to SYS_ROLE_SCREEN_PERMISSION table.
    /// </summary>
    public DbSet<SysRoleScreenPermission> RoleScreenPermissions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the UserScreenPermissions entity set.
    /// Maps to SYS_USER_SCREEN_PERMISSION table.
    /// </summary>
    public DbSet<SysUserScreenPermission> UserScreenPermissions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the UserRoles entity set.
    /// Maps to SYS_USER_ROLE table.
    /// </summary>
    public DbSet<SysUserRole> UserRoles { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Screens entity set.
    /// Maps to SYS_SCREEN table.
    /// </summary>
    public DbSet<SysScreen> Screens { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Systems entity set.
    /// Maps to SYS_SYSTEM table.
    /// </summary>
    public DbSet<SysSystem> Systems { get; set; } = null!;

    /// <summary>
    /// Gets or sets the CompanySystems entity set.
    /// Maps to SYS_COMPANY_SYSTEM table.
    /// </summary>
    public DbSet<SysCompanySystem> CompanySystems { get; set; } = null!;

    #endregion

    #region Ticket Management Entity DbSets

    /// <summary>
    /// Gets or sets the Tickets entity set.
    /// Maps to SYS_REQUEST_TICKET table.
    /// </summary>
    public DbSet<SysRequestTicket> Tickets { get; set; } = null!;

    /// <summary>
    /// Gets or sets the TicketTypes entity set.
    /// Maps to SYS_TICKET_TYPE table.
    /// </summary>
    public DbSet<SysTicketType> TicketTypes { get; set; } = null!;

    /// <summary>
    /// Gets or sets the TicketStatuses entity set.
    /// Maps to SYS_TICKET_STATUS table.
    /// </summary>
    public DbSet<SysTicketStatus> TicketStatuses { get; set; } = null!;

    /// <summary>
    /// Gets or sets the TicketPriorities entity set.
    /// Maps to SYS_TICKET_PRIORITY table.
    /// </summary>
    public DbSet<SysTicketPriority> TicketPriorities { get; set; } = null!;

    /// <summary>
    /// Gets or sets the TicketComments entity set.
    /// Maps to SYS_TICKET_COMMENT table.
    /// </summary>
    public DbSet<SysTicketComment> TicketComments { get; set; } = null!;

    /// <summary>
    /// Gets or sets the TicketAttachments entity set.
    /// Maps to SYS_TICKET_ATTACHMENT table.
    /// </summary>
    public DbSet<SysTicketAttachment> TicketAttachments { get; set; } = null!;

    /// <summary>
    /// Gets or sets the TicketConfigs entity set.
    /// Maps to SYS_TICKET_CONFIG table.
    /// </summary>
    public DbSet<SysTicketConfig> TicketConfigs { get; set; } = null!;

    /// <summary>
    /// Gets or sets the TicketCategories entity set.
    /// Maps to SYS_TICKET_CATEGORY table.
    /// </summary>
    public DbSet<SysTicketCategory> TicketCategories { get; set; } = null!;

    #endregion

    #region Admin and Audit Entity DbSets

    /// <summary>
    /// Gets or sets the SuperAdmins entity set.
    /// Maps to SYS_SUPER_ADMIN table.
    /// </summary>
    public DbSet<SysSuperAdmin> SuperAdmins { get; set; } = null!;

    /// <summary>
    /// Gets or sets the AuditLogs entity set.
    /// Maps to SYS_AUDIT_LOG table.
    /// </summary>
    public DbSet<SysAuditLog> AuditLogs { get; set; } = null!;

    /// <summary>
    /// Gets or sets the SavedSearches entity set.
    /// Maps to SYS_SAVED_SEARCH table.
    /// </summary>
    public DbSet<SysSavedSearch> SavedSearches { get; set; } = null!;

    /// <summary>
    /// Gets or sets the SearchAnalytics entity set.
    /// Maps to SYS_SEARCH_ANALYTICS table.
    /// </summary>
    public DbSet<SysSearchAnalytics> SearchAnalytics { get; set; } = null!;

    #endregion

    /// <summary>
    /// Configures the model that was discovered by convention from the entity types
    /// exposed in DbSet properties on the derived context.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from the current assembly
        // This will automatically discover and apply all IEntityTypeConfiguration<T> implementations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ThinkOnErpDbContext).Assembly);
    }

    /// <summary>
    /// Configures the database context options.
    /// Sets QueryTrackingBehavior to NoTracking by default for better read performance.
    /// </summary>
    /// <param name="optionsBuilder">The builder being used to configure the context.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Configure default query tracking behavior to NoTracking for read-only queries
        // This improves performance by not tracking entities that won't be modified
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }
}
