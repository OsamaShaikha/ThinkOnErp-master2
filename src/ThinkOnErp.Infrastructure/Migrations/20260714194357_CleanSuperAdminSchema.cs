using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanSuperAdminSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var tablesToDrop = new[]
            {
                "SYS_BRANCH_SCREENS",
                "SYS_BRANCH_SYSTEMS",
                "SYS_BRANCH_FEATURES",
                "SYS_USER_SCREEN_PERMISSIONS",
                "SYS_ROLE_SCREEN_PERMISSIONS",
                "SYS_USER_BRANCHES",
                "SYS_TICKET_ATTACHMENT",
                "SYS_TICKET_COMMENT",
                "SYS_REQUEST_TICKET",
                "SYS_TICKET_CATEGORY",
                "SYS_TICKET_CONFIG",
                "SYS_TICKET_PRIORITY",
                "SYS_TICKET_STATUS",
                "SYS_TICKET_TYPE",
                "SYS_FISCAL_YEAR",
                "SYS_SAVED_SEARCH",
                "SYS_SEARCH_ANALYTICS",
                "SYS_REPORT_SCHEDULE",
                "SYS_RETENTION_POLICIES",
                "SYS_BRANCH"
            };

            foreach (var table in tablesToDrop)
            {
                migrationBuilder.Sql($"BEGIN EXECUTE IMMEDIATE 'DROP TABLE \"{table}\" CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
