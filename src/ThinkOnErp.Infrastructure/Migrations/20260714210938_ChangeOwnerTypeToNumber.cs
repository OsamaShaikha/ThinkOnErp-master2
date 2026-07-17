using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeOwnerTypeToNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Oracle doesn't allow ALTER MODIFY to change datatype if column has data.
            // Strategy: add temp NUMBER column, copy data (mapping string values to codes), drop old, rename.
            
            // 1. Drop the index that references OWNER_TYPE
            migrationBuilder.Sql("DROP INDEX \"IX_DOC_OWNER\"");

            // 2. Add a temporary NUMBER column
            migrationBuilder.Sql("ALTER TABLE \"SYS_DOCUMENT\" ADD \"OWNER_TYPE_NEW\" NUMBER(10) DEFAULT 0 NOT NULL");

            // 3. Copy existing string values to numbers (Company=1, Branch=2, SuperAdmin=3)
            migrationBuilder.Sql(@"
                UPDATE ""SYS_DOCUMENT"" SET ""OWNER_TYPE_NEW"" = CASE 
                    WHEN ""OWNER_TYPE"" = 'Company' THEN 1
                    WHEN ""OWNER_TYPE"" = 'Branch' THEN 2
                    WHEN ""OWNER_TYPE"" = 'SuperAdmin' THEN 3
                    ELSE 0
                END");

            // 4. Drop the old VARCHAR2 column
            migrationBuilder.Sql("ALTER TABLE \"SYS_DOCUMENT\" DROP COLUMN \"OWNER_TYPE\"");

            // 5. Rename the new column to OWNER_TYPE
            migrationBuilder.Sql("ALTER TABLE \"SYS_DOCUMENT\" RENAME COLUMN \"OWNER_TYPE_NEW\" TO \"OWNER_TYPE\"");

            // 6. Recreate the composite index
            migrationBuilder.Sql("CREATE INDEX \"IX_DOC_OWNER\" ON \"SYS_DOCUMENT\" (\"OWNER_TYPE\", \"OWNER_ID\", \"IS_ACTIVE\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: convert NUMBER back to VARCHAR2
            migrationBuilder.Sql("DROP INDEX \"IX_DOC_OWNER\"");

            migrationBuilder.Sql("ALTER TABLE \"SYS_DOCUMENT\" ADD \"OWNER_TYPE_OLD\" NVARCHAR2(20) DEFAULT 'Company' NOT NULL");

            migrationBuilder.Sql(@"
                UPDATE ""SYS_DOCUMENT"" SET ""OWNER_TYPE_OLD"" = CASE 
                    WHEN ""OWNER_TYPE"" = 1 THEN 'Company'
                    WHEN ""OWNER_TYPE"" = 2 THEN 'Branch'
                    WHEN ""OWNER_TYPE"" = 3 THEN 'SuperAdmin'
                    ELSE 'Company'
                END");

            migrationBuilder.Sql("ALTER TABLE \"SYS_DOCUMENT\" DROP COLUMN \"OWNER_TYPE\"");
            migrationBuilder.Sql("ALTER TABLE \"SYS_DOCUMENT\" RENAME COLUMN \"OWNER_TYPE_OLD\" TO \"OWNER_TYPE\"");

            migrationBuilder.Sql("CREATE INDEX \"IX_DOC_OWNER\" ON \"SYS_DOCUMENT\" (\"OWNER_TYPE\", \"OWNER_ID\", \"IS_ACTIVE\")");
        }
    }
}
