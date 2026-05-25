using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveLogoToFileSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "COMPANY_LOGO",
                table: "SYS_COMPANY");

            migrationBuilder.DropColumn(
                name: "BRANCH_LOGO",
                table: "SYS_BRANCH");

            migrationBuilder.AddColumn<string>(
                name: "COMPANY_LOGO_PATH",
                table: "SYS_COMPANY",
                type: "NVARCHAR2(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BRANCH_LOGO_PATH",
                table: "SYS_BRANCH",
                type: "NVARCHAR2(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "COMPANY_LOGO_PATH",
                table: "SYS_COMPANY");

            migrationBuilder.DropColumn(
                name: "BRANCH_LOGO_PATH",
                table: "SYS_BRANCH");

            migrationBuilder.AddColumn<string>(
                name: "COMPANY_LOGO",
                table: "SYS_COMPANY",
                type: "CLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BRANCH_LOGO",
                table: "SYS_BRANCH",
                type: "CLOB",
                nullable: true);
        }
    }
}
