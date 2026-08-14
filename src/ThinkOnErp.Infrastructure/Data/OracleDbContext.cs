using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

namespace ThinkOnErp.Infrastructure.Data;

public class OracleDbContext : DbContext
{
    public OracleDbContext(DbContextOptions<OracleDbContext> options) : base(options)
    {
    }

    public async Task<IQueryable<SysAuditLog>> GetCombinedAuditLogsAsync()
    {
        const string auditLogProjection = """
            "Id",
            CORRELATION_ID,
            ACTOR_TYPE,
            ACTOR_ID,
            COMPANY_ID,
            BRANCH_ID,
            ACTION,
            ENTITY_TYPE,
            ENTITY_ID,
            OLD_VALUE,
            NEW_VALUE,
            IP_ADDRESS,
            USER_AGENT,
            HTTP_METHOD,
            ENDPOINT_PATH,
            REQUEST_PAYLOAD,
            RESPONSE_PAYLOAD,
            EXECUTION_TIME_MS,
            STATUS_CODE,
            EXCEPTION_TYPE,
            EXCEPTION_MESSAGE,
            STACK_TRACE,
            SEVERITY,
            EVENT_CATEGORY,
            METADATA,
            STATUS,
            CREATION_DATE,
            BUSINESS_MODULE,
            DEVICE_IDENTIFIER,
            ERROR_CODE,
            BUSINESS_DESCRIPTION
            """;

        var connection = this.Database.GetDbConnection();
        var isMasterSchema = false;
        try
        {
            var builder = new Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder(connection.ConnectionString);
            isMasterSchema = builder.TryGetValue("User Id", out var userIdObj) && string.Equals(userIdObj?.ToString(), "THINKON_ERP", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            isMasterSchema = await this.SysCompanies.CountAsync() > 1;
        }

        if (!isMasterSchema)
        {
            return this.SysAuditLogs.AsQueryable();
        }

        var ownersWithAuditLogs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen) await connection.OpenAsync();
            
            using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText = "SELECT DISTINCT owner FROM all_tables WHERE table_name = 'SYS_AUDIT_LOG'";
                using (var reader = await checkCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        ownersWithAuditLogs.Add(reader.GetString(0));
                    }
                }
            }
        }
        catch
        {
            // Fallback: return only master logs if table check fails
            return this.SysAuditLogs.AsQueryable();
        }

        var sqlParts = new List<string>();
        sqlParts.Add($"SELECT {auditLogProjection} FROM \"SYS_AUDIT_LOG\"");

        var companySchemas = await this.SysCompanies
            .Where(c => c.IsActive && c.CompanySchema != null && c.CompanySchema != "")
            .Select(c => c.CompanySchema)
            .Distinct()
            .ToListAsync();

        foreach (var sch in companySchemas)
        {
            if (sch != null && ownersWithAuditLogs.Contains(sch))
            {
                sqlParts.Add($"SELECT {auditLogProjection} FROM \"{sch}\".\"SYS_AUDIT_LOG\"");
            }
        }

        var combinedSql = string.Join(" UNION ALL ", sqlParts);
        return this.SysAuditLogs.FromSqlRaw(combinedSql).AsQueryable();
    }

    // Core entities
    public DbSet<SysRole> SysRoles => Set<SysRole>();
    public DbSet<SysCurrency> SysCurrencies => Set<SysCurrency>();
    public DbSet<SysCompany> SysCompanies => Set<SysCompany>();
    public DbSet<SysBranch> SysBranches => Set<SysBranch>();
    public DbSet<SysUser> SysUsers => Set<SysUser>();
    public DbSet<SysFiscalYear> SysFiscalYears => Set<SysFiscalYear>();
    public DbSet<SysApiCategory> SysApiCategories => Set<SysApiCategory>();
    public DbSet<SysApiEndpoint> SysApiEndpoints => Set<SysApiEndpoint>();

    // Permission system entities
    public DbSet<SysSuperAdmin> SysSuperAdmins => Set<SysSuperAdmin>();
    public DbSet<SysSystem> SysSystems => Set<SysSystem>();
    public DbSet<SysScreen> SysScreens => Set<SysScreen>();
    public DbSet<SysFeature> SysFeatures => Set<SysFeature>();
    public DbSet<SysUserRole> SysUserRoles => Set<SysUserRole>();
    public DbSet<SysUserBranch> SysUserBranches => Set<SysUserBranch>();

    // Branch-system provisioning
    public DbSet<SysBranchSystem> SysBranchSystems => Set<SysBranchSystem>();
    public DbSet<SysBranchScreen> SysBranchScreens => Set<SysBranchScreen>();
    public DbSet<SysBranchFeature> SysBranchFeatures => Set<SysBranchFeature>();

    // Company-level permissions (tenant schemas)
    public DbSet<SysRoleScreenPermission> SysRoleScreenPermissions => Set<SysRoleScreenPermission>();
    public DbSet<SysUserScreenPermission> SysUserScreenPermissions => Set<SysUserScreenPermission>();

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

    // General ledger / chart of accounts (tenant schemas)
    public DbSet<GlAccount> GlAccounts => Set<GlAccount>();
    public DbSet<GlAccountBranch> GlAccountBranches => Set<GlAccountBranch>();
    public DbSet<GlAccountStructureConfig> GlAccountStructureConfigs => Set<GlAccountStructureConfig>();

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
        modelBuilder.ApplyConfiguration(new SysFeatureConfiguration());
        modelBuilder.ApplyConfiguration(new SysScreenFeatureConfiguration());
        modelBuilder.ApplyConfiguration(new SysUserRoleConfiguration());
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
        modelBuilder.ApplyConfiguration(new SysBranchSystemConfiguration());
        modelBuilder.ApplyConfiguration(new SysBranchScreenConfiguration());
        modelBuilder.ApplyConfiguration(new SysBranchFeatureConfiguration());
        modelBuilder.ApplyConfiguration(new SysRoleScreenPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new SysUserScreenPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new SysUserBranchConfiguration());
        modelBuilder.ApplyConfiguration(new GlAccountConfiguration());
        modelBuilder.ApplyConfiguration(new GlAccountBranchConfiguration());
        modelBuilder.ApplyConfiguration(new GlAccountStructureConfigConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.SysApiCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.SysApiEndpointConfiguration());
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
