using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateBranchScreenAndFeatureTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SYS_BRANCH_FEATURES",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SCREEN_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    FEATURE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    REVOKED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    REVOKED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_BRANCH_FEATURES", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_FEATURES_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_FEATURES_SYS_FEATURE_FEATURE_ID",
                        column: x => x.FEATURE_ID,
                        principalTable: "SYS_FEATURE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_FEATURES_SYS_SCREEN_SCREEN_ID",
                        column: x => x.SCREEN_ID,
                        principalTable: "SYS_SCREEN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_FEATURES_SYS_SUPER_ADMIN_REVOKED_BY",
                        column: x => x.REVOKED_BY,
                        principalTable: "SYS_SUPER_ADMIN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SYS_BRANCH_SCREENS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SCREEN_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    REVOKED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    REVOKED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_BRANCH_SCREENS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SCREENS_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SCREENS_SYS_SCREEN_SCREEN_ID",
                        column: x => x.SCREEN_ID,
                        principalTable: "SYS_SCREEN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SCREENS_SYS_SUPER_ADMIN_REVOKED_BY",
                        column: x => x.REVOKED_BY,
                        principalTable: "SYS_SUPER_ADMIN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_FEATURES_FEATURE_ID",
                table: "SYS_BRANCH_FEATURES",
                column: "FEATURE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_FEATURES_REVOKED_BY",
                table: "SYS_BRANCH_FEATURES",
                column: "REVOKED_BY");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_FEATURES_SCREEN_ID",
                table: "SYS_BRANCH_FEATURES",
                column: "SCREEN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_FEATURES_UK",
                table: "SYS_BRANCH_FEATURES",
                columns: new[] { "BRANCH_ID", "SCREEN_ID", "FEATURE_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SCREENS_REVOKED_BY",
                table: "SYS_BRANCH_SCREENS",
                column: "REVOKED_BY");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SCREENS_SCREEN_ID",
                table: "SYS_BRANCH_SCREENS",
                column: "SCREEN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SCREENS_UK",
                table: "SYS_BRANCH_SCREENS",
                columns: new[] { "BRANCH_ID", "SCREEN_ID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SYS_BRANCH_FEATURES");

            migrationBuilder.DropTable(
                name: "SYS_BRANCH_SCREENS");
        }
    }
}
