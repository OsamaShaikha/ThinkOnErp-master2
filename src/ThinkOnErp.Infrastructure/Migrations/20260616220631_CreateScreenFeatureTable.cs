using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateScreenFeatureTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SYS_SCREEN_FEATURE",
                columns: table => new
                {
                    SCREEN_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    FEATURE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_SCREEN_FEATURE", x => new { x.SCREEN_ID, x.FEATURE_ID });
                    table.ForeignKey(
                        name: "FK_SYS_SCREEN_FEATURE_SYS_FEATURE_FEATURE_ID",
                        column: x => x.FEATURE_ID,
                        principalTable: "SYS_FEATURE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_SCREEN_FEATURE_SYS_SCREEN_SCREEN_ID",
                        column: x => x.SCREEN_ID,
                        principalTable: "SYS_SCREEN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SYS_SCREEN_FEATURE_FEATURE_ID",
                table: "SYS_SCREEN_FEATURE",
                column: "FEATURE_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SYS_SCREEN_FEATURE");
        }
    }
}
