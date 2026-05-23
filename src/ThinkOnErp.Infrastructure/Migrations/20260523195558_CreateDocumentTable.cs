using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateDocumentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SYS_DOCUMENT",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    FILE_NAME = table.Column<string>(type: "NVARCHAR2(255)", maxLength: 255, nullable: false),
                    FILE_SIZE = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    MIME_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    FILE_EXTENSION = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    FILE_PATH = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CATEGORY = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    TAGS = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    OWNER_TYPE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    OWNER_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    IS_ACTIVE = table.Column<string>(type: "NVARCHAR2(1)", maxLength: 1, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_DOCUMENT", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DOC_CATEGORY",
                table: "SYS_DOCUMENT",
                column: "CATEGORY");

            migrationBuilder.CreateIndex(
                name: "IX_DOC_OWNER",
                table: "SYS_DOCUMENT",
                columns: new[] { "OWNER_TYPE", "OWNER_ID", "IS_ACTIVE" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SYS_DOCUMENT");
        }
    }
}
