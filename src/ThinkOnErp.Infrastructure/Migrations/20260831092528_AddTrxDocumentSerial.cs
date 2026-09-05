using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrxDocumentSerial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TRX_DOCUMENT_SERIAL",
                columns: table => new
                {
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    DOC_YEAR = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DOC_MONTH = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DOC_TYPE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LAST_SERIAL_NO = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValue: 0L),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRX_DOC_SERIAL", x => new { x.BRANCH_ID, x.DOC_YEAR, x.DOC_MONTH, x.DOC_TYPE });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TRX_DOCUMENT_SERIAL");
        }
    }
}
