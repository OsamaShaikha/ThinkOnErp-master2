using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115002200_AddEndOfServicePolicyEffectivePeriod")]
public sealed class AddEndOfServicePolicyEffectivePeriod : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
                owner_name VARCHAR2(128) := SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA');

                PROCEDURE inspect_optional_timestamp(p_column VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLS
                    WHERE OWNER = owner_name AND TABLE_NAME = 'HR_END_OF_SERVICE_POLICY' AND COLUMN_NAME = p_column;
                    IF column_count = 1 THEN
                        SELECT COUNT(*) INTO compatible_count FROM ALL_TAB_COLS
                        WHERE OWNER = owner_name AND TABLE_NAME = 'HR_END_OF_SERVICE_POLICY' AND COLUMN_NAME = p_column
                          AND DATA_TYPE = 'TIMESTAMP(7)' AND NULLABLE = 'Y' AND VIRTUAL_COLUMN = 'NO'
                          AND DEFAULT_LENGTH = 0;
                        IF compatible_count <> 1 THEN
                            RAISE_APPLICATION_ERROR(-20331, 'HR_END_OF_SERVICE_POLICY.' || p_column || ' is incompatible; no further DDL applied.');
                        END IF;
                    END IF;
                END;

                PROCEDURE add_if_missing_timestamp(p_column VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLS
                    WHERE OWNER = owner_name AND TABLE_NAME = 'HR_END_OF_SERVICE_POLICY' AND COLUMN_NAME = p_column;
                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_END_OF_SERVICE_POLICY" ADD (' || p_column || ' TIMESTAMP(7) NULL)';
                    END IF;
                END;
            BEGIN
                SELECT COUNT(*) INTO table_count FROM ALL_TABLES
                WHERE OWNER = owner_name AND TABLE_NAME = 'HR_END_OF_SERVICE_POLICY';
                IF table_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20330, 'HR_END_OF_SERVICE_POLICY is required before policy effective dates can be installed.');
                END IF;
                inspect_optional_timestamp('EFFECTIVE_FROM');
                inspect_optional_timestamp('EFFECTIVE_TO');
                add_if_missing_timestamp('EFFECTIVE_FROM');
                add_if_missing_timestamp('EFFECTIVE_TO');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Policy history must survive rollback; nullable columns are intentionally retained.
    }
}
