using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115001400_AddRequiredEosAndShortLeaveTextColumns")]
public sealed class AddRequiredEosAndShortLeaveTextColumns : Migration
{
    private static readonly (string Table, string Column, int Length)[] RequiredColumns =
    [
        ("HR_END_OF_SERVICE_POLICY", "CALCULATION_BASIS", 50),
        ("HR_END_OF_SERVICE_POLICY", "POLICY_NAME", 200),
        ("HR_END_OF_SERVICE_SETTLEMENT", "TERMINATION_TYPE", 50),
        ("HR_LEAVE_CASHOUT", "PAY_COMPONENT_CODE", 100),
        ("HR_LEAVE_CASHOUT", "SETTLEMENT_KEY", 200),
        ("HR_LEAVE_CASHOUT", "WAGE_BASIS_CODE", 30),
        ("HR_SHORT_LEAVE_POLICY", "EXCESS_ACTION", 50),
        ("HR_SHORT_LEAVE_POLICY", "LEAVE_TYPE_CODE_FOR_DEDUCTION", 50),
        ("HR_SHORT_LEAVE_REQUEST", "COMPENSATION_TYPE", 50),
        ("HR_SHORT_LEAVE_REQUEST", "PERMISSION_TYPE", 50)
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(BuildPreflightSql());
        migrationBuilder.Sql(BuildAddColumnsSql());
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Required operational text may contain historical data; rollback preserves it.
    }

    private static string BuildPreflightSql()
    {
        var statements = new List<string>();
        for (var i = 0; i < RequiredColumns.Length; i++)
        {
            var (table, column, length) = RequiredColumns[i];
            statements.Add($"""
                SELECT COUNT(*) INTO table_count_{i}
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = '{table}';
                IF table_count_{i} > 0 THEN
                    SELECT COUNT(*) INTO column_count_{i}
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = '{table}'
                      AND COLUMN_NAME = '{column}';
                    IF column_count_{i} > 0 THEN
                        SELECT COUNT(*) INTO compatible_count_{i}
                        FROM ALL_TAB_COLS
                        WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                          AND TABLE_NAME = '{table}'
                          AND COLUMN_NAME = '{column}'
                          AND DATA_TYPE = 'NVARCHAR2'
                          AND CHAR_LENGTH = {length}
                          AND NULLABLE = 'N'
                          AND NVL(DEFAULT_LENGTH, 0) = 0
                          AND VIRTUAL_COLUMN = 'NO';
                        IF compatible_count_{i} <> 1 THEN
                            RAISE_APPLICATION_ERROR(-20220, '{table}.{column} has an incompatible definition.');
                        END IF;
                    ELSE
                        EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "{table}"' INTO row_count_{i};
                        IF row_count_{i} <> 0 THEN
                            RAISE_APPLICATION_ERROR(-20221, '{table}.{column} is required; the table contains rows and needs an approved backfill before this migration.');
                        END IF;
                    END IF;
                END IF;
                """);
        }

        return $$"""
            DECLARE
                {{string.Join("\n", RequiredColumns.Select((_, i) => $"table_count_{i} NUMBER; column_count_{i} NUMBER; compatible_count_{i} NUMBER; row_count_{i} NUMBER;"))}}
            BEGIN
                {{string.Join("\n", statements)}}
            END;
            """;
    }

    private static string BuildAddColumnsSql()
    {
        var statements = string.Join("\n", RequiredColumns.Select(item => $"""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = '{item.Table}';
                IF table_count > 0 THEN
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = '{item.Table}'
                      AND COLUMN_NAME = '{item.Column}';
                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "{item.Table}" ADD ("{item.Column}" NVARCHAR2({item.Length}) NOT NULL)';
                    END IF;
                END IF;
            END;
            """));

        return $$"""
            BEGIN
                {{statements}}
            END;
            """;
    }
}
