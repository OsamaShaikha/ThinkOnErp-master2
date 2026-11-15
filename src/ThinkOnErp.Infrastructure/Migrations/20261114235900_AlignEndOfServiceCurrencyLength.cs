using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114235900_AlignEndOfServiceCurrencyLength")]
public sealed class AlignEndOfServiceCurrencyLength : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
                current_length NUMBER;
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_END_OF_SERVICE_SETTLEMENT';

                IF table_count = 0 THEN
                    RETURN;
                END IF;

                SELECT COUNT(*) INTO column_count
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_END_OF_SERVICE_SETTLEMENT'
                  AND COLUMN_NAME = 'CURRENCY_CODE';

                IF column_count = 0 THEN
                    RAISE_APPLICATION_ERROR(-20131, 'HR_END_OF_SERVICE_SETTLEMENT.CURRENCY_CODE is missing.');
                END IF;

                SELECT COUNT(*), MAX(CHAR_LENGTH)
                INTO compatible_count, current_length
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_END_OF_SERVICE_SETTLEMENT'
                  AND COLUMN_NAME = 'CURRENCY_CODE'
                  AND DATA_TYPE = 'NVARCHAR2'
                  AND NULLABLE = 'N'
                  AND NVL(DEFAULT_LENGTH, 0) = 0
                  AND VIRTUAL_COLUMN = 'NO';

                IF compatible_count <> 1 OR current_length < 1 THEN
                    RAISE_APPLICATION_ERROR(-20132, 'HR_END_OF_SERVICE_SETTLEMENT.CURRENCY_CODE has an incompatible definition.');
                END IF;

                IF current_length < 20 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_END_OF_SERVICE_SETTLEMENT" MODIFY ("CURRENCY_CODE" NVARCHAR2(20))';
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep the wider currency code domain to avoid truncating tenant data.
    }
}

