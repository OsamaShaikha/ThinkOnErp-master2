using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxIntegrationToInvItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IS_TAX_EXEMPT",
                table: "INV_ITEM",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TAX_EXEMPTION_REASON_CODE",
                table: "INV_ITEM",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TAX_GROUP_ID",
                table: "INV_ITEM",
                type: "NUMBER(19)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TAX_RATE_ID",
                table: "INV_ITEM",
                type: "NUMBER(19)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_TAX_GROUP_ID",
                table: "INV_ITEM",
                column: "TAX_GROUP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_TAX_RATE_ID",
                table: "INV_ITEM",
                column: "TAX_RATE_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_INV_ITEM_TAX_GROUP_TAX_GROUP_ID",
                table: "INV_ITEM",
                column: "TAX_GROUP_ID",
                principalTable: "TAX_GROUP",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_ITEM_TAX_RATE_TAX_RATE_ID",
                table: "INV_ITEM",
                column: "TAX_RATE_ID",
                principalTable: "TAX_RATE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_INV_ITEM_TAX_GROUP_TAX_GROUP_ID",
                table: "INV_ITEM");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_ITEM_TAX_RATE_TAX_RATE_ID",
                table: "INV_ITEM");

            migrationBuilder.DropIndex(
                name: "IX_INV_ITEM_TAX_GROUP_ID",
                table: "INV_ITEM");

            migrationBuilder.DropIndex(
                name: "IX_INV_ITEM_TAX_RATE_ID",
                table: "INV_ITEM");

            migrationBuilder.DropColumn(
                name: "IS_TAX_EXEMPT",
                table: "INV_ITEM");

            migrationBuilder.DropColumn(
                name: "TAX_EXEMPTION_REASON_CODE",
                table: "INV_ITEM");

            migrationBuilder.DropColumn(
                name: "TAX_GROUP_ID",
                table: "INV_ITEM");

            migrationBuilder.DropColumn(
                name: "TAX_RATE_ID",
                table: "INV_ITEM");
        }
    }
}
