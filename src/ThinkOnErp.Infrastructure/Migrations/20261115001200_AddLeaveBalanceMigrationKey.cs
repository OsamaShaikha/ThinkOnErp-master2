using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115001200_AddLeaveBalanceMigrationKey")]
public sealed class AddLeaveBalanceMigrationKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
                row_count NUMBER;
                duplicate_count NUMBER;
                constraint_count NUMBER;
                named_constraint_count NUMBER;
                named_index_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_LEAVE_BALANCE_MIGRATION';
                IF table_count = 0 THEN RETURN; END IF;

                SELECT COUNT(*) INTO column_count
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_LEAVE_BALANCE_MIGRATION'
                  AND COLUMN_NAME = 'MIGRATION_KEY';
                IF column_count > 0 THEN
                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_BALANCE_MIGRATION'
                      AND COLUMN_NAME = 'MIGRATION_KEY'
                      AND DATA_TYPE = 'NVARCHAR2'
                      AND CHAR_LENGTH = 200
                      AND NULLABLE = 'N'
                      AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';
                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20192, 'HR_LEAVE_BALANCE_MIGRATION.MIGRATION_KEY has an incompatible definition.');
                    END IF;
                    EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM (SELECT "MIGRATION_KEY" FROM "HR_LEAVE_BALANCE_MIGRATION" GROUP BY "MIGRATION_KEY" HAVING COUNT(*) > 1)'
                        INTO duplicate_count;
                    IF duplicate_count <> 0 THEN
                        RAISE_APPLICATION_ERROR(-20193, 'HR_LEAVE_BALANCE_MIGRATION.MIGRATION_KEY contains duplicates; no DDL was applied.');
                    END IF;
                ELSE
                    EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "HR_LEAVE_BALANCE_MIGRATION"' INTO row_count;
                    IF row_count <> 0 THEN
                        RAISE_APPLICATION_ERROR(-20194, 'HR_LEAVE_BALANCE_MIGRATION.MIGRATION_KEY is required; the table contains rows and needs an approved backfill before this migration.');
                    END IF;
                END IF;

                SELECT COUNT(*) INTO named_constraint_count
                FROM ALL_CONSTRAINTS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND CONSTRAINT_NAME = 'UX_HR_BAL_MIGRATION_KEY';
                IF named_constraint_count > 0 THEN
                    SELECT COUNT(*) INTO constraint_count
                    FROM ALL_CONSTRAINTS C
                    WHERE C.OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND C.TABLE_NAME = 'HR_LEAVE_BALANCE_MIGRATION'
                      AND C.CONSTRAINT_NAME = 'UX_HR_BAL_MIGRATION_KEY'
                      AND C.CONSTRAINT_TYPE = 'U'
                      AND C.STATUS = 'ENABLED'
                      AND C.VALIDATED = 'VALIDATED'
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS CC
                           WHERE CC.OWNER = C.OWNER AND CC.TABLE_NAME = C.TABLE_NAME
                             AND CC.CONSTRAINT_NAME = C.CONSTRAINT_NAME) = 1
                      AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS CC
                                  WHERE CC.OWNER = C.OWNER AND CC.TABLE_NAME = C.TABLE_NAME
                                    AND CC.CONSTRAINT_NAME = C.CONSTRAINT_NAME
                                    AND CC.COLUMN_NAME = 'MIGRATION_KEY' AND CC.POSITION = 1);
                    IF constraint_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20195, 'UX_HR_BAL_MIGRATION_KEY exists with an incompatible definition.');
                    END IF;
                ELSE
                    SELECT COUNT(*) INTO named_index_count
                    FROM ALL_INDEXES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND INDEX_NAME = 'UX_HR_BAL_MIGRATION_KEY';
                    IF named_index_count > 0 THEN
                        RAISE_APPLICATION_ERROR(-20196, 'UX_HR_BAL_MIGRATION_KEY is already used by an index; manual review is required before DDL.');
                    END IF;
                END IF;

                IF column_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_BALANCE_MIGRATION" ADD ("MIGRATION_KEY" NVARCHAR2(200) NOT NULL)';
                END IF;
                IF named_constraint_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_BALANCE_MIGRATION" ADD CONSTRAINT "UX_HR_BAL_MIGRATION_KEY" UNIQUE ("MIGRATION_KEY") ENABLE VALIDATE';
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep idempotency keys and leave migration history during rollback.
    }
}
