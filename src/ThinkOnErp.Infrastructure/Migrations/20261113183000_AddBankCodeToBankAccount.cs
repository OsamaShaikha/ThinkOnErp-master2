using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261113183000_AddBankCodeToBankAccount")]
public partial class AddBankCodeToBankAccount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE column_count NUMBER;
            compatible_column_count NUMBER;
            table_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'BANK_ACCOUNT';

                IF table_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20051, 'BANK_ACCOUNT is missing in the current schema.');
                END IF;

                SELECT COUNT(*) INTO column_count
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'BANK_ACCOUNT'
                  AND COLUMN_NAME = 'BANK_CODE';

                IF column_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE BANK_ACCOUNT ADD (BANK_CODE NVARCHAR2(50) NULL)';
                ELSE
                    SELECT COUNT(*) INTO compatible_column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'BANK_ACCOUNT'
                      AND COLUMN_NAME = 'BANK_CODE'
                      AND DATA_TYPE = 'NVARCHAR2'
                      AND CHAR_LENGTH = 50
                      AND NULLABLE = 'Y'
                      AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';

                    IF compatible_column_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20052, 'BANK_ACCOUNT.BANK_CODE has an incompatible definition.');
                    END IF;
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // BANK_CODE may have existed before this idempotent migration ran.
        // Removing it here could destroy preexisting bank-account data, so this
        // nullable schema extension is intentionally roll-forward only.
    }
}
