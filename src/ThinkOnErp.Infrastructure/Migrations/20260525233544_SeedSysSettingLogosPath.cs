using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedSysSettingLogosPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "SYS_SETTINGS",
                columns: new[] { "SETTING_CODE", "SETTING_DESC", "SETTING_VALUE" },
                values: new object[] { 6, "Logos path", "/THINKON_FILES/LOGOS/" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SYS_SETTINGS",
                keyColumn: "SETTING_CODE",
                keyValue: 6);
        }
    }
}
