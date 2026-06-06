using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySchemaAndSuperAdminId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SYS_BRANCH_SCREEN_PERMISSIONS_SYS_COMPANY_COMPANY_ID",
                table: "SYS_BRANCH_SCREEN_PERMISSIONS");

            migrationBuilder.DropForeignKey(
                name: "FK_SYS_BRANCH_SYSTEMS_SYS_COMPANY_COMPANY_ID",
                table: "SYS_BRANCH_SYSTEMS");

            migrationBuilder.DropTable(
                name: "SYS_COMPANY_SCREEN_PERMISSIONS");

            migrationBuilder.DropTable(
                name: "SYS_COMPANY_SYSTEMS");

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

            migrationBuilder.AddColumn<string>(
                name: "COMPANY_SCHEMA",
                table: "SYS_COMPANY",
                type: "NVARCHAR2(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CREATED_BY_SUPER_ADMIN_ID",
                table: "SYS_COMPANY",
                type: "NUMBER(19)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SYS_COMPANY_CREATED_BY_SUPER_ADMIN_ID",
                table: "SYS_COMPANY",
                column: "CREATED_BY_SUPER_ADMIN_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_SYS_COMPANY_SYS_SUPER_ADMIN_CREATED_BY_SUPER_ADMIN_ID",
                table: "SYS_COMPANY",
                column: "CREATED_BY_SUPER_ADMIN_ID",
                principalTable: "SYS_SUPER_ADMIN",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SYS_COMPANY_SYS_SUPER_ADMIN_CREATED_BY_SUPER_ADMIN_ID",
                table: "SYS_COMPANY");

            migrationBuilder.DropIndex(
                name: "IX_SYS_COMPANY_CREATED_BY_SUPER_ADMIN_ID",
                table: "SYS_COMPANY");

            migrationBuilder.DropColumn(
                name: "COMPANY_SCHEMA",
                table: "SYS_COMPANY");

            migrationBuilder.DropColumn(
                name: "CREATED_BY_SUPER_ADMIN_ID",
                table: "SYS_COMPANY");

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

            migrationBuilder.CreateTable(
                name: "SYS_COMPANY_SCREEN_PERMISSIONS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    GRANTED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    SCREEN_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CAN_DELETE = table.Column<bool>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_INSERT = table.Column<bool>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_UPDATE = table.Column<bool>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CAN_VIEW = table.Column<bool>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    GRANTED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_COMPANY_SCREEN_PERMISSIONS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SYS_COMPANY_SCREEN_PERMISSIONS_SYS_COMPANY_COMPANY_ID",
                        column: x => x.COMPANY_ID,
                        principalTable: "SYS_COMPANY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SYS_COMPANY_SCREEN_PERMISSIONS_SYS_SCREEN_SCREEN_ID",
                        column: x => x.SCREEN_ID,
                        principalTable: "SYS_SCREEN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SYS_COMPANY_SCREEN_PERMISSIONS_SYS_SUPER_ADMIN_GRANTED_BY",
                        column: x => x.GRANTED_BY,
                        principalTable: "SYS_SUPER_ADMIN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SYS_COMPANY_SYSTEMS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COMPANY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    GRANTED_BY = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    GRANTED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    IS_ALLOWED = table.Column<bool>(type: "NUMBER(1)", maxLength: 1, nullable: false),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    REVOKED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    SYSTEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_COMPANY_SYSTEMS", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SYSTEMS_COMPANY_ID",
                table: "SYS_BRANCH_SYSTEMS",
                column: "COMPANY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_BRANCH_SCREEN_PERMISSIONS_COMPANY_ID",
                table: "SYS_BRANCH_SCREEN_PERMISSIONS",
                column: "COMPANY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_COMPANY_SCREEN_PERMISSIONS_COMPANY_ID",
                table: "SYS_COMPANY_SCREEN_PERMISSIONS",
                column: "COMPANY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_COMPANY_SCREEN_PERMISSIONS_GRANTED_BY",
                table: "SYS_COMPANY_SCREEN_PERMISSIONS",
                column: "GRANTED_BY");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_COMPANY_SCREEN_PERMISSIONS_SCREEN_ID",
                table: "SYS_COMPANY_SCREEN_PERMISSIONS",
                column: "SCREEN_ID");

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
    }
}
