using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateBranchSystemTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SYS_BRANCH_SYSTEMS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SYSTEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    GRANTED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    GRANTED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    REVOKED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_BRANCH_SYSTEMS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SYSTEMS_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SYSTEMS_SYS_SUPER_ADMIN_GRANTED_BY",
                        column: x => x.GRANTED_BY,
                        principalTable: "SYS_SUPER_ADMIN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SYSTEMS_SYS_SYSTEM_SYSTEM_ID",
                        column: x => x.SYSTEM_ID,
                        principalTable: "SYS_SYSTEM",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SYSTEMS_GRANTED_BY",
                table: "SYS_BRANCH_SYSTEMS",
                column: "GRANTED_BY");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SYSTEMS_SYSTEM_ID",
                table: "SYS_BRANCH_SYSTEMS",
                column: "SYSTEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SYSTEMS_UK",
                table: "SYS_BRANCH_SYSTEMS",
                columns: new[] { "BRANCH_ID", "SYSTEM_ID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SYS_BRANCH_SYSTEMS");
        }
    }
}
