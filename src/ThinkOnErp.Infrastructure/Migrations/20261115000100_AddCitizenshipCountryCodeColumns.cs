using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115000100_AddCitizenshipCountryCodeColumns")]
public sealed class AddCitizenshipCountryCodeColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;

                PROCEDURE inspect_column(p_table VARCHAR2, p_column VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table;

                    IF table_count = 0 THEN
                        RETURN;
                    END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table
                      AND COLUMN_NAME = p_column;

                    IF column_count = 0 THEN
                        RETURN;
                    END IF;

                    BEGIN
                        EXECUTE IMMEDIATE
                            'SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER=:owner AND TABLE_NAME=:table_name AND COLUMN_NAME=:column_name AND DATA_TYPE IN (''VARCHAR2'',''NVARCHAR2'') AND CHAR_LENGTH=2 AND CHAR_USED=''C'' AND NULLABLE=''Y'' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN=''NO'''
                            INTO compatible_count USING SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA'), p_table, p_column;
                    EXCEPTION WHEN OTHERS THEN
                        IF SQLCODE != -904 THEN RAISE; END IF;
                        EXECUTE IMMEDIATE
                            'SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER=:owner AND TABLE_NAME=:table_name AND COLUMN_NAME=:column_name AND DATA_TYPE IN (''VARCHAR2'',''NVARCHAR2'') AND CHAR_LENGTH=2 AND CHAR_USED=''C'' AND NULLABLE=''Y'' AND NVL(DEFAULT_LENGTH,0)=0'
                            INTO compatible_count USING SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA'), p_table, p_column;
                    END;

                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20161, p_table || '.' || p_column || ' has an incompatible definition.');
                    END IF;
                END;

                PROCEDURE add_if_missing(p_table VARCHAR2, p_column VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO table_count
                    FROM ALL_TABLES
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table;

                    IF table_count = 0 THEN
                        RETURN;
                    END IF;

                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = p_table
                      AND COLUMN_NAME = p_column;

                    IF column_count = 0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "' || p_table || '" ADD ("' || p_column || '" VARCHAR2(2 CHAR) NULL)';
                    END IF;
                END;
            BEGIN
                -- Check both existing definitions before the first DDL statement.
                inspect_column('HR_EMPLOYEE', 'CITIZENSHIP_COUNTRY_CODE');
                inspect_column('HR_LEAVE_SETTINGS', 'ELIGIBLE_CITIZENSHIP_COUNTRY_CODE');

                add_if_missing('HR_EMPLOYEE', 'CITIZENSHIP_COUNTRY_CODE');
                add_if_missing('HR_LEAVE_SETTINGS', 'ELIGIBLE_CITIZENSHIP_COUNTRY_CODE');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve nullable country-code values on tenant schemas during rollback.
    }
}
