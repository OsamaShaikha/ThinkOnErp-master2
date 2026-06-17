using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClearFeatureIcons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear hardcoded icon values from seed features (icons are now uploaded via API)
            migrationBuilder.Sql(@"UPDATE ""SYS_FEATURE"" SET ""ICON"" = NULL WHERE ""ICON"" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No rollback - icon data restoration is not provided
        }
    }
}
