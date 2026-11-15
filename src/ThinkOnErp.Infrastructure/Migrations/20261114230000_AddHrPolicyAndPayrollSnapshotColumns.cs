using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114230000_AddHrPolicyAndPayrollSnapshotColumns")]
public sealed class AddHrPolicyAndPayrollSnapshotColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;

                PROCEDURE check_table(p_table VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table;
                    IF table_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20111, p_table || ' must exist in the current schema.');
                    END IF;
                END;

                PROCEDURE check_column(
                    p_table VARCHAR2,
                    p_column VARCHAR2,
                    p_length NUMBER,
                    p_nullable CHAR
                ) IS
                BEGIN
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table
                      AND COLUMN_NAME = p_column;

                    IF column_count = 0 THEN
                        RETURN;
                    END IF;

                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table
                      AND COLUMN_NAME = p_column
                      AND DATA_TYPE = 'NVARCHAR2'
                      AND CHAR_LENGTH >= p_length
                      AND NULLABLE = p_nullable
                      AND VIRTUAL_COLUMN = 'NO';

                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20112, p_table || '.' || p_column || ' has an incompatible definition.');
                    END IF;
                END;

                PROCEDURE add_column_if_missing(
                    p_table VARCHAR2,
                    p_column VARCHAR2,
                    p_definition VARCHAR2
                ) IS
                BEGIN
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table
                      AND COLUMN_NAME = p_column;

                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "' || p_table || '" ADD ("' || p_column || '" ' || p_definition || ')';
                    END IF;
                END;
            BEGIN
                check_table('HR_LEAVE_POLICY');
                check_table('HR_LEAVE_TYPE');
                check_table('HR_PRORATION_POLICY');
                check_table('HR_PAYROLL_RUN');

                -- Preflight every existing definition before the first Oracle DDL statement.
                check_column('HR_LEAVE_POLICY', 'ACCRUAL_METHOD', 50, 'N');
                check_column('HR_LEAVE_POLICY', 'APPLICABLE_TO', 50, 'N');
                check_column('HR_LEAVE_POLICY', 'CYCLE_BASIS', 40, 'N');
                check_column('HR_LEAVE_TYPE', 'YEAR_END_EXCESS_ACTION', 20, 'N');
                check_column('HR_PRORATION_POLICY', 'SALARY_SEGMENT_ALLOCATION', 30, 'N');
                check_column('HR_PAYROLL_RUN', 'INPUT_FINGERPRINT', 64, 'Y');

                SELECT COUNT(*) INTO column_count
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_PAYROLL_RUN'
                  AND COLUMN_NAME = 'INPUT_FINGERPRINT';
                IF column_count = 1 THEN
                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_PAYROLL_RUN'
                      AND COLUMN_NAME = 'INPUT_FINGERPRINT'
                      AND NVL(DEFAULT_LENGTH, 0) = 0;
                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20113, 'HR_PAYROLL_RUN.INPUT_FINGERPRINT must not have a database default.');
                    END IF;
                END IF;

                add_column_if_missing('HR_LEAVE_POLICY', 'ACCRUAL_METHOD', 'NVARCHAR2(50) DEFAULT ''YEARLY'' NOT NULL');
                add_column_if_missing('HR_LEAVE_POLICY', 'APPLICABLE_TO', 'NVARCHAR2(50) DEFAULT ''ALL'' NOT NULL');
                add_column_if_missing('HR_LEAVE_POLICY', 'CYCLE_BASIS', 'NVARCHAR2(40) DEFAULT ''CALENDAR_YEAR'' NOT NULL');
                add_column_if_missing('HR_LEAVE_TYPE', 'YEAR_END_EXCESS_ACTION', 'NVARCHAR2(20) DEFAULT ''EXPIRE'' NOT NULL');
                add_column_if_missing('HR_PRORATION_POLICY', 'SALARY_SEGMENT_ALLOCATION', 'NVARCHAR2(30) DEFAULT ''UNCONFIGURED'' NOT NULL');
                add_column_if_missing('HR_PAYROLL_RUN', 'INPUT_FINGERPRINT', 'NVARCHAR2(64) NULL');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve tenant policy values and historical payroll fingerprints.
    }
}
