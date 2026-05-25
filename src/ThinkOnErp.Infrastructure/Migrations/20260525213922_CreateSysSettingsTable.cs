using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateSysSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SYS_SETTINGS",
                columns: table => new
                {
                    SETTING_CODE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SETTING_DESC = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    SETTING_VALUE = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_SETTINGS", x => x.SETTING_CODE);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SYS_SETTINGS");
        }
    }
}
