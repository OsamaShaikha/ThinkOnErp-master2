using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114150000_AddPayrollRecoveryAccounts")]
public sealed class AddPayrollRecoveryAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The schema tool may have installed these nullable columns already.
        // Accept only the expected shape and never infer or backfill accounts.
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;

                PROCEDURE preflight_account_column(p_column_name IN VARCHAR2) IS
                    column_count NUMBER;
                    compatible_count NUMBER;
                BEGIN
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_GL_POSTING_CONFIG'
                      AND COLUMN_NAME = p_column_name;

                    IF column_count > 1 THEN
                        RAISE_APPLICATION_ERROR(-20082,
                            'HR_GL_POSTING_CONFIG.' || p_column_name || ' metadata is ambiguous.');
                    ELSIF column_count = 1 THEN
                        SELECT COUNT(*) INTO compatible_count
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND TABLE_NAME = 'HR_GL_POSTING_CONFIG'
                          AND COLUMN_NAME = p_column_name
                          AND DATA_TYPE = 'NVARCHAR2'
                          AND CHAR_LENGTH = 100
                          AND NULLABLE = 'Y'
                          AND DATA_DEFAULT IS NULL
                          AND VIRTUAL_COLUMN = 'NO';

                        IF compatible_count <> 1 THEN
                            RAISE_APPLICATION_ERROR(-20081,
                                'HR_GL_POSTING_CONFIG.' || p_column_name || ' has an incompatible definition.');
                        END IF;
                    END IF;
                END;

                PROCEDURE ensure_account_column(p_column_name IN VARCHAR2) IS
                    column_count NUMBER;
                    compatible_count NUMBER;
                BEGIN
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_GL_POSTING_CONFIG'
                      AND COLUMN_NAME = p_column_name;

                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE
                            'ALTER TABLE HR_GL_POSTING_CONFIG ADD (' || p_column_name || ' NVARCHAR2(100) NULL)';
                    ELSE
                        SELECT COUNT(*) INTO compatible_count
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND TABLE_NAME = 'HR_GL_POSTING_CONFIG'
                          AND COLUMN_NAME = p_column_name
                          AND DATA_TYPE = 'NVARCHAR2'
                          AND CHAR_LENGTH = 100
                          AND NULLABLE = 'Y'
                          AND DATA_DEFAULT IS NULL
                          AND VIRTUAL_COLUMN = 'NO';

                        IF compatible_count <> 1 THEN
                            RAISE_APPLICATION_ERROR(-20081,
                                'HR_GL_POSTING_CONFIG.' || p_column_name || ' has an incompatible definition.');
                        END IF;
                    END IF;
                END;
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_GL_POSTING_CONFIG';

                IF table_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20080,
                        'HR_GL_POSTING_CONFIG must exist in the current schema.');
                END IF;

                -- Inspect both columns before the first Oracle DDL to avoid a
                -- partially applied migration if either existing definition is invalid.
                preflight_account_column('LOAN_RECEIVABLE_ACCOUNT');
                preflight_account_column('ADVANCE_RECEIVABLE_ACCOUNT');

                ensure_account_column('LOAN_RECEIVABLE_ACCOUNT');
                ensure_account_column('ADVANCE_RECEIVABLE_ACCOUNT');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The schema tool or prior data may own these columns. Dropping them
        // could destroy Accounting configuration, so this extension is roll-forward only.
    }
}
