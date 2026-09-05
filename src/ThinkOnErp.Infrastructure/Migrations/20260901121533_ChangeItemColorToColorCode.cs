using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeItemColorToColorCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "COLOR",
                table: "INV_ITEM");

            migrationBuilder.AddColumn<int>(
                name: "COLOR_CODE",
                table: "INV_ITEM",
                type: "NUMBER(10)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "COLOR_CODE",
                table: "INV_ITEM");

            migrationBuilder.AddColumn<string>(
                name: "COLOR",
                table: "INV_ITEM",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
