using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeBusinessModuleToSystemId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Ensure all module system codes exist in SYS_SYSTEM
            // (MERGE to avoid duplicates if seed data already ran)
            // Map old BUSINESS_MODULE strings → system codes
            // Support→support, HR→hr, Administration→administration,
            // Security→security, Accounting→accounting, Inventory→inventory,
            // POS→pos, CRM→crm, Procurement→procurement, System→system,
            // FINANCE→accounting (alias)
            Up_InsertSystems(migrationBuilder);

            // 2. Add temp numeric column
            Up_AddTempColumns(migrationBuilder);

            // 3. Migrate old string values to SysSystem IDs
            Up_MigrateData(migrationBuilder);

            // 4. Drop old string column, rename temp column
            Up_ReplaceColumn(migrationBuilder);

            // 5. Index and FK
            Up_AddIndexAndForeignKey(migrationBuilder);
        }

        private static void Up_InsertSystems(MigrationBuilder mb)
        {
            // Use individual INSERTs with NOT EXISTS checks instead of MERGE
            // because Oracle identity columns don't work correctly in MERGE
            mb.Sql(@"
                BEGIN
                    INSERT INTO ""SYS_SYSTEM"" (""SYSTEM_CODE"", ""SYSTEM_NAME"", ""SYSTEM_NAME_E"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'administration', 'Administration', 'Administration', 3, 1, CAST('migration' AS NVARCHAR2(100)), SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'administration');

                    INSERT INTO ""SYS_SYSTEM"" (""SYSTEM_CODE"", ""SYSTEM_NAME"", ""SYSTEM_NAME_E"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'security', 'Security', 'Security', 4, 1, CAST('migration' AS NVARCHAR2(100)), SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'security');

                    INSERT INTO ""SYS_SYSTEM"" (""SYSTEM_CODE"", ""SYSTEM_NAME"", ""SYSTEM_NAME_E"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'accounting', 'Accounting', 'Accounting', 5, 1, CAST('migration' AS NVARCHAR2(100)), SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'accounting');

                    INSERT INTO ""SYS_SYSTEM"" (""SYSTEM_CODE"", ""SYSTEM_NAME"", ""SYSTEM_NAME_E"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'inventory', 'Inventory', 'Inventory', 6, 1, CAST('migration' AS NVARCHAR2(100)), SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'inventory');

                    INSERT INTO ""SYS_SYSTEM"" (""SYSTEM_CODE"", ""SYSTEM_NAME"", ""SYSTEM_NAME_E"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'pos', 'POS', 'POS', 7, 1, CAST('migration' AS NVARCHAR2(100)), SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'pos');

                    INSERT INTO ""SYS_SYSTEM"" (""SYSTEM_CODE"", ""SYSTEM_NAME"", ""SYSTEM_NAME_E"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'crm', 'CRM', 'CRM', 8, 1, CAST('migration' AS NVARCHAR2(100)), SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'crm');

                    INSERT INTO ""SYS_SYSTEM"" (""SYSTEM_CODE"", ""SYSTEM_NAME"", ""SYSTEM_NAME_E"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'procurement', 'Procurement', 'Procurement', 9, 1, CAST('migration' AS NVARCHAR2(100)), SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'procurement');

                    INSERT INTO ""SYS_SYSTEM"" (""SYSTEM_CODE"", ""SYSTEM_NAME"", ""SYSTEM_NAME_E"", ""DISPLAY_ORDER"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'system', 'System', 'System', 10, 1, CAST('migration' AS NVARCHAR2(100)), SYSTIMESTAMP FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'system');
                END;
            ");
        }

        private static void Up_AddTempColumns(MigrationBuilder mb)
        {
            mb.AddColumn<long>(
                name: "BUSINESS_MODULE_NEW",
                table: "SYS_AUDIT_LOG",
                type: "NUMBER(19)",
                nullable: true);

            mb.AddColumn<long>(
                name: "BUSINESS_MODULE_NEW",
                table: "SYS_AUDIT_LOG_ARCHIVE",
                type: "NUMBER(19)",
                nullable: true);
        }

        private static void Up_MigrateData(MigrationBuilder mb)
        {
            // Migrate SYS_AUDIT_LOG
            mb.Sql(@"
                MERGE INTO ""SYS_AUDIT_LOG"" t
                USING (SELECT ""Id"", ""BUSINESS_MODULE"" FROM ""SYS_AUDIT_LOG"") s
                ON (t.""Id"" = s.""Id"")
                WHEN MATCHED THEN UPDATE SET
                    ""BUSINESS_MODULE_NEW"" = CASE
                        WHEN s.""BUSINESS_MODULE"" IS NULL THEN NULL
                        WHEN UPPER(s.""BUSINESS_MODULE"") IN ('SUPPORT', 'CUSTOMER SUPPORT') THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'support' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") = 'HR' THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'hr' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") IN ('ADMINISTRATION', 'ADMIN') THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'administration' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") = 'SECURITY' THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'security' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") IN ('ACCOUNTING', 'FINANCE') THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'accounting' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") = 'INVENTORY' THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'inventory' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") IN ('POS', 'POINT OF SALE', 'SALES') THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'pos' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") = 'CRM' THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'crm' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") = 'PROCUREMENT' THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'procurement' AND ROWNUM = 1)
                        ELSE (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'system' AND ROWNUM = 1)
                    END
            ");

            // Same for SYS_AUDIT_LOG_ARCHIVE
            mb.Sql(@"
                MERGE INTO ""SYS_AUDIT_LOG_ARCHIVE"" t
                USING (SELECT ""Id"", ""BUSINESS_MODULE"" FROM ""SYS_AUDIT_LOG_ARCHIVE"") s
                ON (t.""Id"" = s.""Id"")
                WHEN MATCHED THEN UPDATE SET
                    ""BUSINESS_MODULE_NEW"" = CASE
                        WHEN s.""BUSINESS_MODULE"" IS NULL THEN NULL
                        WHEN UPPER(s.""BUSINESS_MODULE"") IN ('SUPPORT', 'CUSTOMER SUPPORT') THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'support' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") = 'HR' THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'hr' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") IN ('ADMINISTRATION', 'ADMIN') THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'administration' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") = 'SECURITY' THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'security' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") IN ('ACCOUNTING', 'FINANCE') THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'accounting' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") = 'INVENTORY' THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'inventory' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") IN ('POS', 'POINT OF SALE', 'SALES') THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'pos' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") = 'CRM' THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'crm' AND ROWNUM = 1)
                        WHEN UPPER(s.""BUSINESS_MODULE"") = 'PROCUREMENT' THEN (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'procurement' AND ROWNUM = 1)
                        ELSE (SELECT ""Id"" FROM ""SYS_SYSTEM"" WHERE LOWER(""SYSTEM_CODE"") = 'system' AND ROWNUM = 1)
                    END
            ");
        }

        private static void Up_ReplaceColumn(MigrationBuilder mb)
        {
            // Drop old string column
            mb.DropColumn(name: "BUSINESS_MODULE", table: "SYS_AUDIT_LOG");
            mb.DropColumn(name: "BUSINESS_MODULE", table: "SYS_AUDIT_LOG_ARCHIVE");

            // Re-add BUSINESS_MODULE as NUMBER(19) (name is free after DROP)
            mb.AddColumn<long>(
                name: "BUSINESS_MODULE",
                table: "SYS_AUDIT_LOG",
                type: "NUMBER(19)",
                nullable: true);

            mb.AddColumn<long>(
                name: "BUSINESS_MODULE",
                table: "SYS_AUDIT_LOG_ARCHIVE",
                type: "NUMBER(19)",
                nullable: true);

            // Copy data from temp column
            mb.Sql(@"UPDATE ""SYS_AUDIT_LOG"" SET ""BUSINESS_MODULE"" = ""BUSINESS_MODULE_NEW""");
            mb.Sql(@"UPDATE ""SYS_AUDIT_LOG_ARCHIVE"" SET ""BUSINESS_MODULE"" = ""BUSINESS_MODULE_NEW""");

            // Drop temp column
            mb.DropColumn(name: "BUSINESS_MODULE_NEW", table: "SYS_AUDIT_LOG");
            mb.DropColumn(name: "BUSINESS_MODULE_NEW", table: "SYS_AUDIT_LOG_ARCHIVE");
        }

        private static void Up_AddIndexAndForeignKey(MigrationBuilder mb)
        {
            mb.CreateIndex(
                name: "IX_SYS_AUDIT_LOG_BUSINESS_MODULE",
                table: "SYS_AUDIT_LOG",
                column: "BUSINESS_MODULE");

            mb.AddForeignKey(
                name: "FK_SYS_AUDIT_LOG_SYSTEM",
                table: "SYS_AUDIT_LOG",
                column: "BUSINESS_MODULE",
                principalTable: "SYS_SYSTEM",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Down_DropForeignKeyAndIndex(migrationBuilder);
            Down_RevertColumn(migrationBuilder);
        }

        private static void Down_DropForeignKeyAndIndex(MigrationBuilder mb)
        {
            mb.DropForeignKey(name: "FK_SYS_AUDIT_LOG_SYSTEM", table: "SYS_AUDIT_LOG");
            mb.DropIndex(name: "IX_SYS_AUDIT_LOG_BUSINESS_MODULE", table: "SYS_AUDIT_LOG");
        }

        private static void Down_RevertColumn(MigrationBuilder mb)
        {
            // SYS_AUDIT_LOG: add string temp, populate, drop numeric, add string, copy, drop temp
            mb.AddColumn<string>(
                name: "BUSINESS_MODULE_OLD", table: "SYS_AUDIT_LOG",
                type: "NVARCHAR2(100)", maxLength: 100, nullable: true);
            mb.Sql(@"UPDATE ""SYS_AUDIT_LOG"" al SET ""BUSINESS_MODULE_OLD"" = (SELECT s.""SYSTEM_CODE"" FROM ""SYS_SYSTEM"" s WHERE s.""Id"" = al.""BUSINESS_MODULE"")");
            mb.DropColumn(name: "BUSINESS_MODULE", table: "SYS_AUDIT_LOG");
            mb.AddColumn<string>(
                name: "BUSINESS_MODULE", table: "SYS_AUDIT_LOG",
                type: "NVARCHAR2(100)", maxLength: 100, nullable: true);
            mb.Sql(@"UPDATE ""SYS_AUDIT_LOG"" SET ""BUSINESS_MODULE"" = ""BUSINESS_MODULE_OLD""");
            mb.DropColumn(name: "BUSINESS_MODULE_OLD", table: "SYS_AUDIT_LOG");

            // SYS_AUDIT_LOG_ARCHIVE: same
            mb.AddColumn<string>(
                name: "BUSINESS_MODULE_OLD", table: "SYS_AUDIT_LOG_ARCHIVE",
                type: "NVARCHAR2(100)", maxLength: 100, nullable: true);
            mb.Sql(@"UPDATE ""SYS_AUDIT_LOG_ARCHIVE"" al SET ""BUSINESS_MODULE_OLD"" = (SELECT s.""SYSTEM_CODE"" FROM ""SYS_SYSTEM"" s WHERE s.""Id"" = al.""BUSINESS_MODULE"")");
            mb.DropColumn(name: "BUSINESS_MODULE", table: "SYS_AUDIT_LOG_ARCHIVE");
            mb.AddColumn<string>(
                name: "BUSINESS_MODULE", table: "SYS_AUDIT_LOG_ARCHIVE",
                type: "NVARCHAR2(100)", maxLength: 100, nullable: true);
            mb.Sql(@"UPDATE ""SYS_AUDIT_LOG_ARCHIVE"" SET ""BUSINESS_MODULE"" = ""BUSINESS_MODULE_OLD""");
            mb.DropColumn(name: "BUSINESS_MODULE_OLD", table: "SYS_AUDIT_LOG_ARCHIVE");
        }
    }
}
