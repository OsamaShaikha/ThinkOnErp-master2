using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyIdToBranchPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "COMPANY_ID",
                table: "SYS_BRANCH_SYSTEMS",
                type: "NUMBER(19)",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "COMPANY_ID",
                table: "SYS_BRANCH_SCREEN_PERMISSIONS",
                type: "NUMBER(19)",
                nullable: false,
                defaultValue: 0L);

            // Update COMPANY_ID from SYS_BRANCH for existing rows
            migrationBuilder.Sql(@"
                UPDATE ""SYS_BRANCH_SYSTEMS"" bs
                SET bs.""COMPANY_ID"" = (
                    SELECT b.""COMPANY_ID""
                    FROM ""SYS_BRANCH"" b
                    WHERE b.""Id"" = bs.""BRANCH_ID""
                )
                WHERE bs.""COMPANY_ID"" = 0");

            migrationBuilder.Sql(@"
                UPDATE ""SYS_BRANCH_SCREEN_PERMISSIONS"" bsp
                SET bsp.""COMPANY_ID"" = (
                    SELECT b.""COMPANY_ID""
                    FROM ""SYS_BRANCH"" b
                    WHERE b.""Id"" = bsp.""BRANCH_ID""
                )
                WHERE bsp.""COMPANY_ID"" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SYSTEMS_COMPANY_ID",
                table: "SYS_BRANCH_SYSTEMS",
                column: "COMPANY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SCREEN_PERMISSIONS_COMPANY_ID",
                table: "SYS_BRANCH_SCREEN_PERMISSIONS",
                column: "COMPANY_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_SYS_BRANCH_SCREEN_PERMISSIONS_SYS_COMPANY_COMPANY_ID",
                table: "SYS_BRANCH_SCREEN_PERMISSIONS",
                column: "COMPANY_ID",
                principalTable: "SYS_COMPANY",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SYS_BRANCH_SYSTEMS_SYS_COMPANY_COMPANY_ID",
                table: "SYS_BRANCH_SYSTEMS",
                column: "COMPANY_ID",
                principalTable: "SYS_COMPANY",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SYS_BRANCH_SCREEN_PERMISSIONS_SYS_COMPANY_COMPANY_ID",
                table: "SYS_BRANCH_SCREEN_PERMISSIONS");

            migrationBuilder.DropForeignKey(
                name: "FK_SYS_BRANCH_SYSTEMS_SYS_COMPANY_COMPANY_ID",
                table: "SYS_BRANCH_SYSTEMS");

            migrationBuilder.DropIndex(
                name: "IX_SYS_BRANCH_SYSTEMS_COMPANY_ID",
                table: "SYS_BRANCH_SYSTEMS");

            migrationBuilder.DropIndex(
                name: "IX_SYS_BRANCH_SCREEN_PERMISSIONS_COMPANY_ID",
                table: "SYS_BRANCH_SCREEN_PERMISSIONS");

            migrationBuilder.DropColumn(
                name: "COMPANY_ID",
                table: "SYS_BRANCH_SYSTEMS");

            migrationBuilder.DropColumn(
                name: "COMPANY_ID",
                table: "SYS_BRANCH_SCREEN_PERMISSIONS");
        }
    }
}
