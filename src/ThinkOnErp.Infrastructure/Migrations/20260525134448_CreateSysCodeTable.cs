using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateSysCodeTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SYS_CODE",
                columns: table => new
                {
                    CODE_MGR = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CODE_MNR = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CODE_LANG = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CODE_DESC = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    IS_ACTIVE = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 1),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_CODE", x => new { x.CODE_MGR, x.CODE_MNR, x.CODE_LANG });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SYS_CODE");
        }
    }
}
