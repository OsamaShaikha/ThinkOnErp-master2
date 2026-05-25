using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveTaxNumberToBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add TAX_NUMBER to SYS_BRANCH first (idempotent)
            migrationBuilder.Sql(@"
                BEGIN
                    EXECUTE IMMEDIATE 'ALTER TABLE ""SYS_BRANCH"" ADD ""TAX_NUMBER"" NVARCHAR2(50)';
                EXCEPTION WHEN OTHERS THEN
                    IF SQLCODE != -1430 THEN RAISE; END IF;
                END;
            ");

            // Step 2: Migrate existing TAX_NUMBER from each company to its default branch
            // (or the first head branch if no default is set)
            // Note: IS_HEAD_BRANCH is NUMBER(1) because SyncBoolModel was never populated
            migrationBuilder.Sql(@"
                MERGE INTO SYS_BRANCH b
                USING (
                    SELECT c.""Id"" AS COMPANY_ID, c.TAX_NUMBER,
                           COALESCE(c.DEFAULT_BRANCH_ID,
                               (SELECT MIN(b2.""Id"") FROM SYS_BRANCH b2
                                WHERE b2.COMPANY_ID = c.""Id"" AND b2.IS_HEAD_BRANCH = 1)
                           ) AS BRANCH_ID
                    FROM SYS_COMPANY c
                    WHERE c.TAX_NUMBER IS NOT NULL
                ) s
                ON (b.""Id"" = s.BRANCH_ID)
                WHEN MATCHED THEN
                    UPDATE SET b.TAX_NUMBER = s.TAX_NUMBER
            ");

            // Step 3: Drop TAX_NUMBER from SYS_COMPANY
            migrationBuilder.DropColumn(
                name: "TAX_NUMBER",
                table: "SYS_COMPANY");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Restore TAX_NUMBER on SYS_COMPANY from the default branch
            migrationBuilder.AddColumn<string>(
                name: "TAX_NUMBER",
                table: "SYS_COMPANY",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(@"
                MERGE INTO SYS_COMPANY c
                USING (
                    SELECT b.COMPANY_ID, b.TAX_NUMBER
                    FROM SYS_BRANCH b
                    WHERE b.TAX_NUMBER IS NOT NULL
                ) s
                ON (c.""Id"" = s.COMPANY_ID)
                WHEN MATCHED THEN
                    UPDATE SET c.TAX_NUMBER = s.TAX_NUMBER
            ");

            // Step 2: Drop TAX_NUMBER from SYS_BRANCH
            migrationBuilder.DropColumn(
                name: "TAX_NUMBER",
                table: "SYS_BRANCH");
        }
    }
}
