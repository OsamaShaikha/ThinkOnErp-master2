using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115000800_AddLeavePolicyName")]
public sealed class AddLeavePolicyName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
                row_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_LEAVE_POLICY';

                IF table_count = 0 THEN
                    RETURN;
                END IF;

                SELECT COUNT(*) INTO column_count
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_LEAVE_POLICY'
                  AND COLUMN_NAME = 'POLICY_NAME';

                IF column_count > 0 THEN
                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_POLICY'
                      AND COLUMN_NAME = 'POLICY_NAME'
                      AND DATA_TYPE = 'NVARCHAR2'
                      AND CHAR_LENGTH = 200
                      AND NULLABLE = 'N'
                      AND VIRTUAL_COLUMN = 'NO';

                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20182, 'HR_LEAVE_POLICY.POLICY_NAME has an incompatible definition.');
                    END IF;
                    RETURN;
                END IF;

                EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "HR_LEAVE_POLICY"' INTO row_count;
                IF row_count <> 0 THEN
                    RAISE_APPLICATION_ERROR(-20183, 'HR_LEAVE_POLICY.POLICY_NAME is required; the table contains rows and needs an approved backfill before this migration.');
                END IF;

                EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_POLICY" ADD ("POLICY_NAME" NVARCHAR2(200) NOT NULL)';
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve tenant leave policy definitions during rollback.
    }
}
