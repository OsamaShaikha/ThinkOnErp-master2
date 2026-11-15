using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115002100_AlignLeaveAccrualAutoDefault")]
public sealed class AlignLeaveAccrualAutoDefault : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
                invalid_values NUMBER;
                nullable_columns NUMBER;
                owner_name VARCHAR2(128) := SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA');
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = owner_name AND TABLE_NAME = 'HR_LEAVE_SETTINGS';

                IF table_count = 0 THEN
                    RAISE_APPLICATION_ERROR(-20281, 'HR_LEAVE_SETTINGS is missing; leave accrual default migration refused.');
                END IF;

                SELECT COUNT(*) INTO column_count
                FROM ALL_TAB_COLS
                WHERE OWNER = owner_name AND TABLE_NAME = 'HR_LEAVE_SETTINGS'
                  AND COLUMN_NAME = 'ACCRUAL_AUTO_ENABLED';

                IF column_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_SETTINGS" ADD ("ACCRUAL_AUTO_ENABLED" NUMBER(10) DEFAULT 0 NOT NULL)';
                ELSE
                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = owner_name AND TABLE_NAME = 'HR_LEAVE_SETTINGS'
                      AND COLUMN_NAME = 'ACCRUAL_AUTO_ENABLED'
                      AND DATA_TYPE = 'NUMBER'
                      AND DATA_PRECISION IN (1, 10)
                      AND DATA_SCALE = 0
                      AND VIRTUAL_COLUMN = 'NO';

                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20282, 'ACCRUAL_AUTO_ENABLED is not a compatible NUMBER flag; no DDL applied.');
                    END IF;

                    EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "HR_LEAVE_SETTINGS" WHERE "ACCRUAL_AUTO_ENABLED" IS NULL OR "ACCRUAL_AUTO_ENABLED" NOT IN (0,1)'
                        INTO invalid_values;
                    IF invalid_values <> 0 THEN
                        RAISE_APPLICATION_ERROR(-20283, 'ACCRUAL_AUTO_ENABLED contains values outside 0/1; no DDL applied.');
                    END IF;

                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_SETTINGS" MODIFY ("ACCRUAL_AUTO_ENABLED" NUMBER(10))';
                    SELECT COUNT(*) INTO nullable_columns
                    FROM ALL_TAB_COLS
                    WHERE OWNER = owner_name AND TABLE_NAME = 'HR_LEAVE_SETTINGS'
                      AND COLUMN_NAME = 'ACCRUAL_AUTO_ENABLED' AND NULLABLE = 'Y';
                    IF nullable_columns = 1 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_SETTINGS" MODIFY ("ACCRUAL_AUTO_ENABLED" NOT NULL)';
                    END IF;
                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_SETTINGS" MODIFY ("ACCRUAL_AUTO_ENABLED" DEFAULT 0)';
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve the safe false default and any tenant configuration on rollback.
    }
}
