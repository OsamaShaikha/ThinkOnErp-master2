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
            DECLARE item_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO item_count FROM ALL_TAB_COLUMNS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE' AND COLUMN_NAME = 'USER_ID';
                IF item_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE HR_EMPLOYEE ADD (USER_ID NUMBER(19) NULL)';
                END IF;
            END;
            """);
        migrationBuilder.Sql("""
            DECLARE item_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO item_count FROM ALL_CONSTRAINTS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE' AND CONSTRAINT_NAME = 'UX_HR_EMPLOYEE_USER_ID';
                IF item_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE HR_EMPLOYEE ADD CONSTRAINT UX_HR_EMPLOYEE_USER_ID UNIQUE (USER_ID)';
                END IF;
            END;
            """);
        migrationBuilder.Sql("""
            DECLARE item_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO item_count FROM ALL_CONSTRAINTS
                WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                  AND TABLE_NAME = 'HR_EMPLOYEE' AND CONSTRAINT_NAME = 'FK_HR_EMPLOYEE_USER';
                IF item_count = 0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE HR_EMPLOYEE ADD CONSTRAINT FK_HR_EMPLOYEE_USER FOREIGN KEY (USER_ID) REFERENCES SYS_USERS ("Id")';
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_HR_EMPLOYEE_USER", table: "HR_EMPLOYEE");
        migrationBuilder.DropUniqueConstraint(name: "UX_HR_EMPLOYEE_USER_ID", table: "HR_EMPLOYEE");
        migrationBuilder.DropColumn(name: "USER_ID", table: "HR_EMPLOYEE");
    }
}
