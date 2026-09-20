using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItemVariantsAndSerials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HAS_VARIANTS",
                table: "INV_ITEM",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SKU",
                table: "INV_ITEM",
                type: "NVARCHAR2(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "INV_ITEM_ATTRIBUTE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ATTRIBUTE_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ITEM_ATTRIBUTE", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "INV_ITEM_VARIANT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SKU = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    VARIANT_NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(255)", maxLength: 255, nullable: false),
                    VARIANT_NAME_EN = table.Column<string>(type: "NVARCHAR2(255)", maxLength: 255, nullable: true),
                    BARCODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    ADDITIONAL_PRICE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    COST_PRICE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    IMAGE_BASE64 = table.Column<string>(type: "CLOB", nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ITEM_VARIANT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_ITEM_VARIANT_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "INV_ITEM_ATTRIBUTE_VALUE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ATTRIBUTE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    VALUE_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    VALUE_LOCAL = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    VALUE_EN = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    COLOR_HEX = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    SORT_ORDER = table.Column<int>(type: "NUMBER(6)", nullable: false, defaultValue: 0),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ITEM_ATTRIBUTE_VALUE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_ITEM_ATTRIBUTE_VALUE_INV_ITEM_ATTRIBUTE_ATTRIBUTE_ID",
                        column: x => x.ATTRIBUTE_ID,
                        principalTable: "INV_ITEM_ATTRIBUTE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "INV_ITEM_VARIANT_VALUE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    VARIANT_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ATTRIBUTE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ATTRIBUTE_VALUE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ITEM_VARIANT_VALUE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_ITEM_VARIANT_VALUE_INV_ITEM_ATTRIBUTE_ATTRIBUTE_ID",
                        column: x => x.ATTRIBUTE_ID,
                        principalTable: "INV_ITEM_ATTRIBUTE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_ITEM_VARIANT_VALUE_INV_ITEM_ATTRIBUTE_VALUE_ATTRIBUTE_VALUE_ID",
                        column: x => x.ATTRIBUTE_VALUE_ID,
                        principalTable: "INV_ITEM_ATTRIBUTE_VALUE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_ITEM_VARIANT_VALUE_INV_ITEM_VARIANT_VARIANT_ID",
                        column: x => x.VARIANT_ID,
                        principalTable: "INV_ITEM_VARIANT",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_ATTRIBUTE_VALUE_ATTRIBUTE_ID",
                table: "INV_ITEM_ATTRIBUTE_VALUE",
                column: "ATTRIBUTE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_VARIANT_BARCODE",
                table: "INV_ITEM_VARIANT",
                column: "BARCODE");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_VARIANT_ITEM_ID",
                table: "INV_ITEM_VARIANT",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_VARIANT_SKU",
                table: "INV_ITEM_VARIANT",
                column: "SKU");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_VARIANT_VALUE_ATTRIBUTE_ID",
                table: "INV_ITEM_VARIANT_VALUE",
                column: "ATTRIBUTE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_VARIANT_VALUE_ATTRIBUTE_VALUE_ID",
                table: "INV_ITEM_VARIANT_VALUE",
                column: "ATTRIBUTE_VALUE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_VARIANT_VALUE_VARIANT_ID",
                table: "INV_ITEM_VARIANT_VALUE",
                column: "VARIANT_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INV_ITEM_VARIANT_VALUE");

            migrationBuilder.DropTable(
                name: "INV_ITEM_ATTRIBUTE_VALUE");

            migrationBuilder.DropTable(
                name: "INV_ITEM_VARIANT");

            migrationBuilder.DropTable(
                name: "INV_ITEM_ATTRIBUTE");

            migrationBuilder.DropColumn(
                name: "HAS_VARIANTS",
                table: "INV_ITEM");

            migrationBuilder.DropColumn(
                name: "SKU",
                table: "INV_ITEM");
        }
    }
}
