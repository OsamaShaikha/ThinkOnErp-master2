using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

public class SysApiCategoryConfiguration : IEntityTypeConfiguration<SysApiCategory>
{
    public void Configure(EntityTypeBuilder<SysApiCategory> builder)
    {
        builder.ToTable("SYS_API_CATEGORIES");

        builder.HasKey(c => c.CategoryCode);

        builder.Property(c => c.CategoryCode)
            .HasColumnName("CATEGORY_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.DisplayTitle)
            .HasColumnName("DISPLAY_TITLE")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasColumnName("DESCRIPTION")
            .HasMaxLength(500);

        builder.Property(c => c.DisplayOrder)
            .HasColumnName("DISPLAY_ORDER")
            .HasDefaultValue(0);

        builder.Property(c => c.ControllerNames)
            .HasColumnName("CONTROLLER_NAMES")
            .HasMaxLength(1000);

        builder.Property(c => c.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true);

        // Seed Default Categories
        builder.HasData(
            new SysApiCategory
            {
                CategoryCode = "superadmin",
                DisplayTitle = "1. SuperAdmin API",
                Description = "Platform-wide management: super admin accounts, company/branch registry, schema provisioning & sync.",
                DisplayOrder = 1,
                ControllerNames = "SuperAdminController,SuperAdminAuthController",
                IsActive = true
            },
            new SysApiCategory
            {
                CategoryCode = "company",
                DisplayTitle = "2. Company & Tenant Management API",
                Description = "Tenant-specific company management: company registration, default branch configuration, branch access, company permissions, and DEV_TEMPLATE schema export.",
                DisplayOrder = 2,
                ControllerNames = "CompanyController,BranchController,BranchAccessController,CompanyPermissionsController",
                IsActive = true
            },
            new SysApiCategory
            {
                CategoryCode = "accounting",
                DisplayTitle = "3. Accounting & COA API",
                Description = "Accounting operations: chart of accounts CRUD, level 1/2 categories, next child code generator, COA Excel validation/import, postable accounts, fiscal years, and currency reference data.",
                DisplayOrder = 3,
                ControllerNames = "GlAccountsController,AccountCategoriesController,CoaImportController,CurrencyController,FiscalYearController",
                IsActive = true
            },
            new SysApiCategory
            {
                CategoryCode = "auth",
                DisplayTitle = "4. Auth & Security API",
                Description = "Authentication and Authorization: login, JWT tokens, user management, roles, and fine-grained permissions.",
                DisplayOrder = 4,
                ControllerNames = "AuthController,UsersController,RolesController,PermissionsController",
                IsActive = true
            },
            new SysApiCategory
            {
                CategoryCode = "audit",
                DisplayTitle = "5. Audit & Security Monitoring API",
                Description = "Audit and monitoring: audit logs, entity audit trail, audit health, threat alerts, performance metrics, compliance reporting, and key management.",
                DisplayOrder = 5,
                ControllerNames = "AuditLogsController,AuditTrailController,AuditHealthController,AlertsController,MonitoringController,ComplianceController,KeyManagementController",
                IsActive = true
            },
            new SysApiCategory
            {
                CategoryCode = "support",
                DisplayTitle = "6. Tickets & Support API",
                Description = "Customer support & ticket management: support tickets, ticket status, and ticket types.",
                DisplayOrder = 6,
                ControllerNames = "TicketsController,TicketTypesController",
                IsActive = true
            },
            new SysApiCategory
            {
                CategoryCode = "system",
                DisplayTitle = "7. System Settings & Codes API",
                Description = "System configuration & metadata: system lookup codes, global settings, modules, screens, feature toggles, health checks, document uploads, and saved searches.",
                DisplayOrder = 7,
                ControllerNames = "SysCodesController,SysSettingsController,ModulesController,ScreensController,FeaturesController,HealthController,DocumentsController,ConfigurationController,SavedSearchesController",
                IsActive = true
            }
        );
    }
}
