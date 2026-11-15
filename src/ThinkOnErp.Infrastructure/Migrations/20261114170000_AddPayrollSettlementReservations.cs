using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114170000_AddPayrollSettlementReservations")]
public sealed class AddPayrollSettlementReservations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Reservation ownership is nullable so existing advances and installments
        // remain untouched. The schema tool may already have installed the columns.
        migrationBuilder.Sql("""
            DECLARE
                advance_table_count NUMBER;
                schedule_table_count NUMBER;

                PROCEDURE preflight_reservation_column(
                    p_table_name IN VARCHAR2,
                    p_column_name IN VARCHAR2) IS
                    column_count NUMBER;
                    compatible_count NUMBER;
                BEGIN
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table_name
                      AND COLUMN_NAME = p_column_name;

                    IF column_count > 1 THEN
                        RAISE_APPLICATION_ERROR(-20096,
                            p_table_name || '.' || p_column_name || ' metadata is ambiguous.');
                    ELSIF column_count = 1 THEN
                        SELECT COUNT(*) INTO compatible_count
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND TABLE_NAME = p_table_name
                          AND COLUMN_NAME = p_column_name
                          AND DATA_TYPE = 'NUMBER'
                          AND DATA_PRECISION = 19
                          AND NVL(DATA_SCALE, 0) = 0
                          AND NULLABLE = 'Y'
                          AND DATA_DEFAULT IS NULL
                          AND VIRTUAL_COLUMN = 'NO';

                        IF compatible_count <> 1 THEN
                            RAISE_APPLICATION_ERROR(-20095,
                                p_table_name || '.' || p_column_name || ' has an incompatible definition.');
                        END IF;
                    END IF;
                END;

                PROCEDURE ensure_reservation_column(
                    p_table_name IN VARCHAR2,
                    p_column_name IN VARCHAR2) IS
                    column_count NUMBER;
                    compatible_count NUMBER;
                BEGIN
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table_name
                      AND COLUMN_NAME = p_column_name;

                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE
                            'ALTER TABLE ' || p_table_name ||
                            ' ADD (' || p_column_name || ' NUMBER(19) NULL)';
                    ELSE
                        SELECT COUNT(*) INTO compatible_count
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND TABLE_NAME = p_table_name
                          AND COLUMN_NAME = p_column_name
                          AND DATA_TYPE = 'NUMBER'
                          AND DATA_PRECISION = 19
                          AND NVL(DATA_SCALE, 0) = 0
                          AND NULLABLE = 'Y'
                          AND DATA_DEFAULT IS NULL
                          AND VIRTUAL_COLUMN = 'NO';

                        IF compatible_count <> 1 THEN
                            RAISE_APPLICATION_ERROR(-20095,
                                p_table_name || '.' || p_column_name || ' has an incompatible definition.');
                        END IF;
                    END IF;
                END;
            BEGIN
                SELECT COUNT(*) INTO advance_table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE_ADVANCE';
                SELECT COUNT(*) INTO schedule_table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_LOAN_REPAYMENT_SCHEDULE';

                IF advance_table_count <> 1 OR schedule_table_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20094,
                        'HR_EMPLOYEE_ADVANCE and HR_LOAN_REPAYMENT_SCHEDULE must exist in the current schema.');
                END IF;

                -- Validate both tenant columns before the first Oracle DDL.
                preflight_reservation_column('HR_EMPLOYEE_ADVANCE', 'RESERVED_BY_HANDOFF_ID');
                preflight_reservation_column('HR_LOAN_REPAYMENT_SCHEDULE', 'RESERVED_BY_HANDOFF_ID');

                ensure_reservation_column('HR_EMPLOYEE_ADVANCE', 'RESERVED_BY_HANDOFF_ID');
                ensure_reservation_column('HR_LOAN_REPAYMENT_SCHEDULE', 'RESERVED_BY_HANDOFF_ID');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A schema-tool patch or payroll activity may already own reservation data.
        // Preserve it when reverting the migration record.
    }
}
