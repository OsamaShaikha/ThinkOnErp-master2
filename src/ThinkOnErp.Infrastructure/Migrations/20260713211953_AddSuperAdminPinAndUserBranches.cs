using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminPinAndUserBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PIN_HASH",
                table: "SYS_SUPER_ADMIN",
                type: "NVARCHAR2(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "USERS_LIMIT",
                table: "SYS_BRANCH",
                type: "NUMBER(10)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PIN_HASH",
                table: "SYS_SUPER_ADMIN");

            migrationBuilder.DropColumn(
                name: "USERS_LIMIT",
                table: "SYS_BRANCH");
        }
    }
}
