using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

public partial class LinkEmployeeUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // CURRENT_SCHEMA respects the application's tenant routing and permits an
        // already-applied DEV_TEMPLATE SQL patch to be recorded without duplicate DDL.
        migrationBuilder.Sql("""
            DECLARE table_count NUMBER;
                    item_count NUMBER;
                    compatible_column_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO table_count FROM ALL_TABLES
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE';
                IF table_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20061, 'HR_EMPLOYEE is missing in the current schema.');
                END IF;

                SELECT COUNT(*) INTO item_count FROM ALL_TAB_COLUMNS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE' AND COLUMN_NAME = 'USER_ID';
                IF item_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE HR_EMPLOYEE ADD (USER_ID NUMBER(19) NULL)';
                ELSE
                    SELECT COUNT(*) INTO compatible_column_count FROM ALL_TAB_COLUMNS
                    WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND TABLE_NAME = 'HR_EMPLOYEE' AND COLUMN_NAME = 'USER_ID'
                      AND DATA_TYPE = 'NUMBER' AND DATA_PRECISION = 19
                      AND NVL(DATA_SCALE, 0) = 0 AND NULLABLE = 'Y';
                    IF compatible_column_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20062, 'HR_EMPLOYEE.USER_ID has an incompatible definition.');
                    END IF;
                END IF;
            END;
            """);
        migrationBuilder.Sql("""
            DECLARE item_count NUMBER;
                    compatible_constraint_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO item_count FROM ALL_CONSTRAINTS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE' AND CONSTRAINT_NAME = 'UX_HR_EMPLOYEE_USER_ID';
                IF item_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE HR_EMPLOYEE ADD CONSTRAINT UX_HR_EMPLOYEE_USER_ID UNIQUE (USER_ID)';
                ELSE
                    SELECT COUNT(*) INTO compatible_constraint_count
                    FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND c.TABLE_NAME = 'HR_EMPLOYEE'
                      AND c.CONSTRAINT_NAME = 'UX_HR_EMPLOYEE_USER_ID'
                      AND c.CONSTRAINT_TYPE = 'U' AND c.STATUS = 'ENABLED'
                      AND c.VALIDATED = 'VALIDATED'
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc
                           WHERE cc.OWNER = c.OWNER AND cc.CONSTRAINT_NAME = c.CONSTRAINT_NAME
                             AND cc.TABLE_NAME = c.TABLE_NAME) = 1
                      AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc
                                  WHERE cc.OWNER = c.OWNER AND cc.CONSTRAINT_NAME = c.CONSTRAINT_NAME
                                    AND cc.TABLE_NAME = c.TABLE_NAME AND cc.COLUMN_NAME = 'USER_ID'
                                    AND cc.POSITION = 1);
                    IF compatible_constraint_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20063, 'UX_HR_EMPLOYEE_USER_ID has an incompatible definition.');
                    END IF;
                END IF;
            END;
            """);
        migrationBuilder.Sql("""
            DECLARE item_count NUMBER;
                    compatible_constraint_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO item_count FROM ALL_CONSTRAINTS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE' AND CONSTRAINT_NAME = 'FK_HR_EMPLOYEE_USER';
                IF item_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE HR_EMPLOYEE ADD CONSTRAINT FK_HR_EMPLOYEE_USER FOREIGN KEY (USER_ID) REFERENCES SYS_USERS ("Id")';
                ELSE
                    SELECT COUNT(*) INTO compatible_constraint_count
                    FROM ALL_CONSTRAINTS fk
                    JOIN ALL_CONSTRAINTS referenced_key
                      ON referenced_key.OWNER = fk.R_OWNER
                     AND referenced_key.CONSTRAINT_NAME = fk.R_CONSTRAINT_NAME
                    WHERE fk.OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND fk.TABLE_NAME = 'HR_EMPLOYEE'
                      AND fk.CONSTRAINT_NAME = 'FK_HR_EMPLOYEE_USER'
                      AND fk.CONSTRAINT_TYPE = 'R' AND fk.STATUS = 'ENABLED'
                      AND fk.VALIDATED = 'VALIDATED'
                      AND referenced_key.TABLE_NAME = 'SYS_USERS'
                      AND referenced_key.CONSTRAINT_TYPE IN ('P', 'U')
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc
                           WHERE cc.OWNER = fk.OWNER AND cc.CONSTRAINT_NAME = fk.CONSTRAINT_NAME
                             AND cc.TABLE_NAME = fk.TABLE_NAME) = 1
                      AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc
                                  WHERE cc.OWNER = fk.OWNER AND cc.CONSTRAINT_NAME = fk.CONSTRAINT_NAME
                                    AND cc.TABLE_NAME = fk.TABLE_NAME AND cc.COLUMN_NAME = 'USER_ID'
                                    AND cc.POSITION = 1)
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS rc
                           WHERE rc.OWNER = referenced_key.OWNER
                             AND rc.CONSTRAINT_NAME = referenced_key.CONSTRAINT_NAME
                             AND rc.TABLE_NAME = referenced_key.TABLE_NAME) = 1
                      AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS rc
                                  WHERE rc.OWNER = referenced_key.OWNER
                                    AND rc.CONSTRAINT_NAME = referenced_key.CONSTRAINT_NAME
                                    AND rc.TABLE_NAME = referenced_key.TABLE_NAME
                                    AND rc.COLUMN_NAME = 'Id' AND rc.POSITION = 1);
                    IF compatible_constraint_count <> 1 THEN
                        RAISE_APPLICATION_ERROR(-20064, 'FK_HR_EMPLOYEE_USER has an incompatible definition.');
                    END IF;
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Up is intentionally idempotent so a preexisting SQL patch can be
        // recorded as an EF migration. Down therefore cannot know whether it
        // owns USER_ID, and dropping it could erase existing employee links.
    }
}
