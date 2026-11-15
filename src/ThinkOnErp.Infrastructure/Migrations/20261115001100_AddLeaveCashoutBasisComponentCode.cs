using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115001100_AddLeaveCashoutBasisComponentCode")]
public sealed class AddLeaveCashoutBasisComponentCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                parent_table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
                parent_key_count NUMBER;
                orphan_count NUMBER;
                constraint_count NUMBER;
                named_constraint_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_LEAVE_CASHOUT';
                IF table_count = 0 THEN RETURN; END IF;

                SELECT COUNT(*) INTO parent_table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_SALARY_COMPONENT';
                IF parent_table_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20186, 'HR_SALARY_COMPONENT is required before upgrading HR_LEAVE_CASHOUT.BASIS_COMPONENT_CODE.');
                END IF;

                SELECT COUNT(*) INTO compatible_count
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_SALARY_COMPONENT'
                  AND COLUMN_NAME = 'COMPONENT_CODE'
                  AND DATA_TYPE = 'NVARCHAR2'
                  AND CHAR_LENGTH = 50
                  AND CHAR_USED = 'C'
                  AND NULLABLE = 'N'
                  AND VIRTUAL_COLUMN = 'NO';
                IF compatible_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20187, 'HR_SALARY_COMPONENT.COMPONENT_CODE has an incompatible definition.');
                END IF;

                SELECT COUNT(*) INTO parent_key_count
                FROM ALL_CONSTRAINTS C
                WHERE C.OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND C.TABLE_NAME = 'HR_SALARY_COMPONENT'
                  AND C.CONSTRAINT_TYPE IN ('P','U')
                  AND C.STATUS = 'ENABLED'
                  AND C.VALIDATED = 'VALIDATED'
                  AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS CC
                       WHERE CC.OWNER = C.OWNER AND CC.TABLE_NAME = C.TABLE_NAME
                         AND CC.CONSTRAINT_NAME = C.CONSTRAINT_NAME) = 1
                  AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS CC
                              WHERE CC.OWNER = C.OWNER AND CC.TABLE_NAME = C.TABLE_NAME
                                AND CC.CONSTRAINT_NAME = C.CONSTRAINT_NAME
                                AND CC.COLUMN_NAME = 'COMPONENT_CODE' AND CC.POSITION = 1);
                IF parent_key_count = 0 THEN
                    RAISE_APPLICATION_ERROR(-20188, 'HR_SALARY_COMPONENT.COMPONENT_CODE has no enabled, validated single-column key.');
                END IF;

                SELECT COUNT(*) INTO column_count
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_LEAVE_CASHOUT'
                  AND COLUMN_NAME = 'BASIS_COMPONENT_CODE';
                IF column_count > 0 THEN
                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_CASHOUT'
                      AND COLUMN_NAME = 'BASIS_COMPONENT_CODE'
                      AND DATA_TYPE = 'NVARCHAR2'
                      AND CHAR_LENGTH = 100
                      AND NULLABLE = 'Y'
                      AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';
                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20189, 'HR_LEAVE_CASHOUT.BASIS_COMPONENT_CODE has an incompatible definition.');
                    END IF;

                    EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "HR_LEAVE_CASHOUT" C WHERE C."BASIS_COMPONENT_CODE" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "HR_SALARY_COMPONENT" P WHERE P."COMPONENT_CODE" = C."BASIS_COMPONENT_CODE")'
                        INTO orphan_count;
                    IF orphan_count <> 0 THEN
                        RAISE_APPLICATION_ERROR(-20190, 'HR_LEAVE_CASHOUT.BASIS_COMPONENT_CODE contains orphan values; no DDL was applied.');
                    END IF;
                END IF;

                SELECT COUNT(*) INTO named_constraint_count
                FROM ALL_CONSTRAINTS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND CONSTRAINT_NAME = 'FK_HR_LCO_BASIS_COMP';
                IF named_constraint_count > 0 THEN
                    SELECT COUNT(*) INTO constraint_count
                    FROM ALL_CONSTRAINTS C
                    JOIN ALL_CONSTRAINTS P
                      ON P.OWNER = C.R_OWNER AND P.CONSTRAINT_NAME = C.R_CONSTRAINT_NAME
                    WHERE C.OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND C.TABLE_NAME = 'HR_LEAVE_CASHOUT'
                      AND C.CONSTRAINT_NAME = 'FK_HR_LCO_BASIS_COMP'
                      AND C.CONSTRAINT_TYPE = 'R'
                      AND C.STATUS = 'ENABLED'
                      AND C.VALIDATED = 'VALIDATED'
                      AND C.DELETE_RULE = 'NO ACTION'
                      AND C.R_OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND P.TABLE_NAME = 'HR_SALARY_COMPONENT'
                      AND P.CONSTRAINT_TYPE IN ('P','U')
                      AND P.STATUS = 'ENABLED'
                      AND P.VALIDATED = 'VALIDATED'
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS CC WHERE CC.OWNER=C.OWNER AND CC.TABLE_NAME=C.TABLE_NAME AND CC.CONSTRAINT_NAME=C.CONSTRAINT_NAME) = 1
                      AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS CC WHERE CC.OWNER=C.OWNER AND CC.TABLE_NAME=C.TABLE_NAME AND CC.CONSTRAINT_NAME=C.CONSTRAINT_NAME AND CC.COLUMN_NAME='BASIS_COMPONENT_CODE' AND CC.POSITION=1)
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS CC WHERE CC.OWNER=P.OWNER AND CC.TABLE_NAME=P.TABLE_NAME AND CC.CONSTRAINT_NAME=P.CONSTRAINT_NAME) = 1
                      AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS CC WHERE CC.OWNER=P.OWNER AND CC.TABLE_NAME=P.TABLE_NAME AND CC.CONSTRAINT_NAME=P.CONSTRAINT_NAME AND CC.COLUMN_NAME='COMPONENT_CODE' AND CC.POSITION=1);
                    IF constraint_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20191, 'FK_HR_LCO_BASIS_COMP exists with an incompatible definition.');
                    END IF;
                END IF;

                IF column_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_CASHOUT" ADD ("BASIS_COMPONENT_CODE" NVARCHAR2(100) NULL)';
                END IF;
                IF named_constraint_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_CASHOUT" ADD CONSTRAINT "FK_HR_LCO_BASIS_COMP" FOREIGN KEY ("BASIS_COMPONENT_CODE") REFERENCES "HR_SALARY_COMPONENT" ("COMPONENT_CODE")';
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve tenant cashout configuration and historical component references.
    }
}
