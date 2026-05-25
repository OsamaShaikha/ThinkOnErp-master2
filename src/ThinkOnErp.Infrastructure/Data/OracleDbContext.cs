using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

public class OracleDbContext : DbContext
{
    public OracleDbContext(DbContextOptions<OracleDbContext> options) : base(options)
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

    // Document management
    public DbSet<SysDocument> SysDocuments => Set<SysDocument>();

    // Audit log
    public DbSet<SysAuditLog> SysAuditLogs => Set<SysAuditLog>();

    // Security monitoring
    public DbSet<SysSecurityThreat> SysSecurityThreats => Set<SysSecurityThreat>();
    public DbSet<SysFailedLogin> SysFailedLogins => Set<SysFailedLogin>();

    // Performance monitoring
    public DbSet<SysPerformanceMetric> SysPerformanceMetrics => Set<SysPerformanceMetric>();
    public DbSet<SysSlowQuery> SysSlowQueries => Set<SysSlowQuery>();

    // Scheduled reporting
    public DbSet<SysReportSchedule> SysReportSchedules => Set<SysReportSchedule>();

    // Archival
    public DbSet<SysAuditLogArchive> SysAuditLogArchives => Set<SysAuditLogArchive>();
    public DbSet<SysRetentionPolicy> SysRetentionPolicies => Set<SysRetentionPolicy>();
    public DbSet<SysCode> SysCodes => Set<SysCode>();
    public DbSet<SysSetting> SysSettings => Set<SysSetting>();

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
        modelBuilder.ApplyConfiguration(new SysDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new SysAuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new SysSecurityThreatConfiguration());
        modelBuilder.ApplyConfiguration(new SysFailedLoginConfiguration());
        modelBuilder.ApplyConfiguration(new SysSlowQueryConfiguration());
        modelBuilder.ApplyConfiguration(new SysPerformanceMetricConfiguration());
        modelBuilder.ApplyConfiguration(new SysReportScheduleConfiguration());
        modelBuilder.ApplyConfiguration(new SysAuditLogArchiveConfiguration());
        modelBuilder.ApplyConfiguration(new SysRetentionPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new SysCodeConfiguration());
        modelBuilder.ApplyConfiguration(new SysSettingConfiguration());

        // Oracle doesn't support BOOLEAN as a SQL column type; map all bool to NUMBER(1)
        // Clear HasConversion<string> from individual configs — Oracle provider handles
        // bool <-> NUMBER(1) natively when there's no explicit value converter
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(bool))
                {
                    property.SetColumnType("NUMBER(1)");
                    property.SetValueConverter((Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter?)null);
                }
            }
        }
    }

}
