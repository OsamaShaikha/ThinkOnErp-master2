using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115001300_AddLeaveYearCloseKey")]
public sealed class AddLeaveYearCloseKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                table_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
                row_count NUMBER;
                duplicate_count NUMBER;
                named_index_count NUMBER;
                named_index_shape_count NUMBER;
                equivalent_unique_count NUMBER;
                constraint_count NUMBER;
                named_constraint_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO table_count
                FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE';
                IF table_count = 0 THEN RETURN; END IF;

                SELECT COUNT(*) INTO column_count
                FROM ALL_TAB_COLS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE'
                  AND COLUMN_NAME = 'CLOSE_KEY';
                IF column_count > 0 THEN
                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE'
                      AND COLUMN_NAME = 'CLOSE_KEY'
                      AND DATA_TYPE = 'NVARCHAR2'
                      AND CHAR_LENGTH = 300
                      AND NULLABLE = 'N'
                      AND NVL(DEFAULT_LENGTH, 0) = 0
                      AND VIRTUAL_COLUMN = 'NO';
                    IF compatible_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20197, 'HR_LEAVE_YEAR_CLOSE.CLOSE_KEY has an incompatible definition.');
                    END IF;
                    EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM (SELECT "CLOSE_KEY" FROM "HR_LEAVE_YEAR_CLOSE" GROUP BY "CLOSE_KEY" HAVING COUNT(*) > 1)'
                        INTO duplicate_count;
                    IF duplicate_count <> 0 THEN
                        RAISE_APPLICATION_ERROR(-20198, 'HR_LEAVE_YEAR_CLOSE.CLOSE_KEY contains duplicates; no DDL was applied.');
                    END IF;
                ELSE
                    EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "HR_LEAVE_YEAR_CLOSE"' INTO row_count;
                    IF row_count <> 0 THEN
                        RAISE_APPLICATION_ERROR(-20199, 'HR_LEAVE_YEAR_CLOSE.CLOSE_KEY is required; the table contains rows and needs an approved backfill before this migration.');
                    END IF;
                END IF;

                SELECT COUNT(*) INTO named_constraint_count
                FROM ALL_CONSTRAINTS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND CONSTRAINT_NAME = 'UX_HR_LEAVE_CLOSE_KEY';
                IF named_constraint_count > 0 THEN
                    SELECT COUNT(*) INTO constraint_count
                    FROM ALL_CONSTRAINTS C
                    WHERE C.OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND C.TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE'
                      AND C.CONSTRAINT_NAME = 'UX_HR_LEAVE_CLOSE_KEY'
                      AND C.CONSTRAINT_TYPE = 'U'
                      AND C.STATUS = 'ENABLED'
                      AND C.VALIDATED = 'VALIDATED'
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS CC
                           WHERE CC.OWNER = C.OWNER AND CC.TABLE_NAME = C.TABLE_NAME
                             AND CC.CONSTRAINT_NAME = C.CONSTRAINT_NAME) = 1
                      AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS CC
                                  WHERE CC.OWNER = C.OWNER AND CC.TABLE_NAME = C.TABLE_NAME
                                    AND CC.CONSTRAINT_NAME = C.CONSTRAINT_NAME
                                    AND CC.COLUMN_NAME = 'CLOSE_KEY' AND CC.POSITION = 1);
                    IF constraint_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20200, 'UX_HR_LEAVE_CLOSE_KEY exists with an incompatible definition.');
                    END IF;
                END IF;

                SELECT COUNT(*) INTO named_index_count
                FROM ALL_INDEXES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND INDEX_NAME = 'UX_HR_LEAVE_CLOSE_KEY';
                IF named_index_count > 0 THEN
                    SELECT COUNT(*) INTO named_index_shape_count
                    FROM ALL_INDEXES I
                    WHERE I.OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND I.INDEX_NAME = 'UX_HR_LEAVE_CLOSE_KEY'
                      AND I.TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE'
                      AND I.UNIQUENESS = 'UNIQUE'
                      AND I.STATUS = 'VALID'
                      AND I.INDEX_TYPE = 'NORMAL'
                      AND (SELECT COUNT(*) FROM ALL_IND_COLUMNS IC
                           WHERE IC.INDEX_OWNER = I.OWNER AND IC.INDEX_NAME = I.INDEX_NAME
                             AND IC.TABLE_NAME = I.TABLE_NAME) = 1
                      AND EXISTS (SELECT 1 FROM ALL_IND_COLUMNS IC
                                  WHERE IC.INDEX_OWNER = I.OWNER AND IC.INDEX_NAME = I.INDEX_NAME
                                    AND IC.TABLE_NAME = I.TABLE_NAME
                                    AND IC.COLUMN_NAME = 'CLOSE_KEY' AND IC.COLUMN_POSITION = 1)
                      AND NOT EXISTS (SELECT 1 FROM ALL_IND_EXPRESSIONS IE
                                      WHERE IE.INDEX_OWNER = I.OWNER AND IE.INDEX_NAME = I.INDEX_NAME
                                        AND IE.TABLE_NAME = I.TABLE_NAME);
                    IF named_index_shape_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20201, 'UX_HR_LEAVE_CLOSE_KEY index name is occupied by an incompatible index.');
                    END IF;
                END IF;

                SELECT COUNT(*) INTO equivalent_unique_count
                FROM ALL_INDEXES I
                WHERE I.OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND I.TABLE_NAME = 'HR_LEAVE_YEAR_CLOSE'
                  AND I.UNIQUENESS = 'UNIQUE'
                  AND I.STATUS = 'VALID'
                  AND I.INDEX_TYPE = 'NORMAL'
                  AND (SELECT COUNT(*) FROM ALL_IND_COLUMNS IC
                       WHERE IC.INDEX_OWNER = I.OWNER AND IC.INDEX_NAME = I.INDEX_NAME
                         AND IC.TABLE_NAME = I.TABLE_NAME) = 1
                  AND EXISTS (SELECT 1 FROM ALL_IND_COLUMNS IC
                              WHERE IC.INDEX_OWNER = I.OWNER AND IC.INDEX_NAME = I.INDEX_NAME
                                AND IC.TABLE_NAME = I.TABLE_NAME
                                AND IC.COLUMN_NAME = 'CLOSE_KEY' AND IC.COLUMN_POSITION = 1)
                  AND NOT EXISTS (SELECT 1 FROM ALL_IND_EXPRESSIONS IE
                                  WHERE IE.INDEX_OWNER = I.OWNER AND IE.INDEX_NAME = I.INDEX_NAME
                                    AND IE.TABLE_NAME = I.TABLE_NAME);

                IF column_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_YEAR_CLOSE" ADD ("CLOSE_KEY" NVARCHAR2(300) NOT NULL)';
                END IF;
                IF equivalent_unique_count = 0 AND named_constraint_count = 0 THEN
                    EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX "UX_HR_LEAVE_CLOSE_KEY" ON "HR_LEAVE_YEAR_CLOSE" ("CLOSE_KEY")';
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve deterministic close keys and historical year-close records.
    }
}
