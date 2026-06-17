using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOldPermissionEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only SYS_BRANCH_SYSTEMS exists in master schema.
            // Legacy permission tables (SYS_BRANCH_SCREEN_PERMISSIONS,
            // SYS_ROLE_SCREEN_PERMISSIONS, SYS_USER_SCREEN_PERMISSIONS)
            // reside only in tenant schemas and will be handled separately.
            // IX_SYS_COMPANY_SCHEMA already exists from prior migration.
            migrationBuilder.DropTable(
                name: "SYS_BRANCH_SYSTEMS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SYS_BRANCH_SYSTEMS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    GRANTED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    SYSTEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    GRANTED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    IS_ALLOWED = table.Column<bool>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    REVOKED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_BRANCH_SYSTEMS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_BRANCH_SYSTEMS_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_SYS_BRANCH_SYSTEMS_BRANCH_ID",
                table: "SYS_BRANCH_SYSTEMS",
                column: "BRANCH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SYSTEMS_GRANTED_BY",
                table: "SYS_BRANCH_SYSTEMS",
                column: "GRANTED_BY");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SYSTEMS_SYSTEM_ID",
                table: "SYS_BRANCH_SYSTEMS",
                column: "SYSTEM_ID");
        }
    }
}
