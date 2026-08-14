-- ====================================================================
-- Script Name: 94_Create_SYS_API_CATEGORIES.sql
-- Description: Creates SYS_API_CATEGORIES table to persist Swagger documents & controller mappings in Oracle Database.
-- Author: ThinkOnErp Development Team
-- Date: 2026-08-11
-- Schema: Master / DEV_TEMPLATE
-- ====================================================================

DECLARE
    v_table_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_table_exists 
    FROM all_tables 
    WHERE table_name = 'SYS_API_CATEGORIES' AND owner = USER;

    IF v_table_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE SYS_API_CATEGORIES (
                CATEGORY_CODE VARCHAR2(50) NOT NULL,
                DISPLAY_TITLE VARCHAR2(150) NOT NULL,
                DESCRIPTION VARCHAR2(500),
                DISPLAY_ORDER NUMBER DEFAULT 0,
                CONTROLLER_NAMES VARCHAR2(1000),
                IS_ACTIVE NUMBER(1) DEFAULT 1,
                CONSTRAINT PK_SYS_API_CATEGORIES PRIMARY KEY (CATEGORY_CODE)
            )
        ';
        DBMS_OUTPUT.PUT_LINE('Table SYS_API_CATEGORIES created successfully.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Table SYS_API_CATEGORIES already exists.');
    END IF;
END;
/

-- Seed / Merge Categories
MERGE INTO SYS_API_CATEGORIES target
USING (
    SELECT 'superadmin' AS CATEGORY_CODE, '1. SuperAdmin API' AS DISPLAY_TITLE, 'Platform-wide management: super admin accounts, company/branch registry, schema provisioning & sync.' AS DESCRIPTION, 1 AS DISPLAY_ORDER, 'SuperAdminController,SuperAdminAuthController' AS CONTROLLER_NAMES, 1 AS IS_ACTIVE FROM DUAL UNION ALL
    SELECT 'company', '2. Company & Tenant Management API', 'Tenant-specific company management: company registration, default branch configuration, branch access, company permissions, and DEV_TEMPLATE schema export.', 2, 'CompanyController,BranchController,BranchAccessController,CompanyPermissionsController', 1 FROM DUAL UNION ALL
    SELECT 'accounting', '3. Accounting & COA API', 'Accounting operations: chart of accounts CRUD, level 1/2 categories, next child code generator, COA Excel validation/import, postable accounts, fiscal years, and currency reference data.', 3, 'GlAccountsController,AccountCategoriesController,CoaImportController,CurrencyController,FiscalYearController', 1 FROM DUAL UNION ALL
    SELECT 'auth', '4. Auth & Security API', 'Authentication and Authorization: login, JWT tokens, user management, roles, and fine-grained permissions.', 4, 'AuthController,UsersController,RolesController,PermissionsController', 1 FROM DUAL UNION ALL
    SELECT 'audit', '5. Audit & Security Monitoring API', 'Audit and monitoring: audit logs, entity audit trail, audit health, threat alerts, performance metrics, compliance reporting, and key management.', 5, 'AuditLogsController,AuditTrailController,AuditHealthController,AlertsController,MonitoringController,ComplianceController,KeyManagementController', 1 FROM DUAL UNION ALL
    SELECT 'support', '6. Tickets & Support API', 'Customer support & ticket management: support tickets, ticket status, and ticket types.', 6, 'TicketsController,TicketTypesController', 1 FROM DUAL UNION ALL
    SELECT 'system', '7. System Settings & Codes API', 'System configuration & metadata: system lookup codes, global settings, modules, screens, feature toggles, health checks, document uploads, and saved searches.', 7, 'SysCodesController,SysSettingsController,ModulesController,ScreensController,FeaturesController,HealthController,DocumentsController,ConfigurationController,SavedSearchesController', 1 FROM DUAL
) src
ON (target.CATEGORY_CODE = src.CATEGORY_CODE)
WHEN MATCHED THEN
    UPDATE SET 
        target.DISPLAY_TITLE = src.DISPLAY_TITLE,
        target.DESCRIPTION = src.DESCRIPTION,
        target.DISPLAY_ORDER = src.DISPLAY_ORDER,
        target.CONTROLLER_NAMES = src.CONTROLLER_NAMES,
        target.IS_ACTIVE = src.IS_ACTIVE
WHEN NOT MATCHED THEN
    INSERT (CATEGORY_CODE, DISPLAY_TITLE, DESCRIPTION, DISPLAY_ORDER, CONTROLLER_NAMES, IS_ACTIVE)
    VALUES (src.CATEGORY_CODE, src.DISPLAY_TITLE, src.DESCRIPTION, src.DISPLAY_ORDER, src.CONTROLLER_NAMES, src.IS_ACTIVE);

COMMIT;
/
