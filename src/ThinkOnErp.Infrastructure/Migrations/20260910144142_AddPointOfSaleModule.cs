using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPointOfSaleModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_INV_ITEM_UOM_CONVERSION_INV_ITEM_ITEM_ID",
                table: "INV_ITEM_UOM_CONVERSION");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_LOT_MASTER_INV_ITEM_ITEM_ID",
                table: "INV_LOT_MASTER");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_SERIAL_MASTER_INV_ITEM_ITEM_ID",
                table: "INV_SERIAL_MASTER");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_STOCK_BALANCE_INV_ITEM_ITEM_ID",
                table: "INV_STOCK_BALANCE");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_STOCK_BALANCE_INV_WAREHOUSE_WAREHOUSE_ID",
                table: "INV_STOCK_BALANCE");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_STOCK_LEDGER_ENTRY_INV_ITEM_ITEM_ID",
                table: "INV_STOCK_LEDGER_ENTRY");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_STOCK_LEDGER_ENTRY_INV_WAREHOUSE_WAREHOUSE_ID",
                table: "INV_STOCK_LEDGER_ENTRY");

            migrationBuilder.DropPrimaryKey(
                name: "PK_INV_STOCK_LEDGER_ENTRY",
                table: "INV_STOCK_LEDGER_ENTRY");

            migrationBuilder.DropPrimaryKey(
                name: "PK_INV_ITEM_UOM_CONVERSION",
                table: "INV_ITEM_UOM_CONVERSION");

            migrationBuilder.RenameTable(
                name: "INV_STOCK_LEDGER_ENTRY",
                newName: "INV_STOCK_LEDGER");

            migrationBuilder.RenameTable(
                name: "INV_ITEM_UOM_CONVERSION",
                newName: "INV_ITEM_UOM");

            migrationBuilder.RenameIndex(
                name: "IX_INV_STOCK_LEDGER_ENTRY_WAREHOUSE_ID",
                table: "INV_STOCK_LEDGER",
                newName: "IX_INV_STOCK_LEDGER_WAREHOUSE_ID");

            migrationBuilder.RenameIndex(
                name: "IX_INV_STOCK_LEDGER_ENTRY_ITEM_ID",
                table: "INV_STOCK_LEDGER",
                newName: "IX_INV_STOCK_LEDGER_ITEM_ID");

            migrationBuilder.RenameIndex(
                name: "IX_INV_ITEM_UOM_CONVERSION_ITEM_ID",
                table: "INV_ITEM_UOM",
                newName: "IX_INV_ITEM_UOM_ITEM_ID");

            migrationBuilder.AlterColumn<int>(
                name: "STOCK_DIRECTION",
                table: "TRX_TRANSACTION_TYPE",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(byte),
                oldType: "NUMBER(2)",
                oldDefaultValue: (byte)0);

            migrationBuilder.AlterColumn<int>(
                name: "UOM_CODE",
                table: "TRX_DOCUMENT_LINE",
                type: "NUMBER(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<decimal>(
                name: "LINE_COST",
                table: "TRX_DOCUMENT_LINE",
                type: "NUMBER(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LINE_PROFIT",
                table: "TRX_DOCUMENT_LINE",
                type: "NUMBER(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PROFIT_MARGIN_PERCENT",
                table: "TRX_DOCUMENT_LINE",
                type: "NUMBER(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PROFIT_MARGIN_PERCENT",
                table: "TRX_DOCUMENT_HEADER",
                type: "NUMBER(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TOTAL_COST",
                table: "TRX_DOCUMENT_HEADER",
                type: "NUMBER(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TOTAL_PROFIT",
                table: "TRX_DOCUMENT_HEADER",
                type: "NUMBER(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "ZONE_CODE",
                table: "INV_ZONE",
                type: "NUMBER(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "WAREHOUSE_CODE",
                table: "INV_WAREHOUSE",
                type: "NUMBER(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "UOM_CODE",
                table: "INV_OPENING_LINE",
                type: "NUMBER(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "GROUP_CODE",
                table: "INV_ITEM_GROUP",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<bool>(
                name: "IS_SHOW_IN_POS",
                table: "INV_ITEM_GROUP",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<int>(
                name: "UOM_CODE",
                table: "INV_ITEM_BARCODE",
                type: "NUMBER(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "WEIGHT_UNIT",
                table: "INV_ITEM",
                type: "NUMBER(6)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UOM_BASE",
                table: "INV_ITEM",
                type: "NUMBER(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "UOM_CODE",
                table: "INV_BOM_LINE",
                type: "NUMBER(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "UOM_CODE",
                table: "INV_BOM_HEADER",
                type: "NUMBER(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "BOM_CODE",
                table: "INV_BOM_HEADER",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<int>(
                name: "BIN_CODE",
                table: "INV_BIN",
                type: "NUMBER(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "UOM_CODE",
                table: "INV_STOCK_LEDGER",
                type: "NUMBER(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "TRANSACTION_TYPE",
                table: "INV_STOCK_LEDGER",
                type: "NUMBER(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "DIRECTION",
                table: "INV_STOCK_LEDGER",
                type: "NVARCHAR2(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "UOM_CODE",
                table: "INV_ITEM_UOM",
                type: "NUMBER(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(50)",
                oldMaxLength: 50);

            migrationBuilder.AddPrimaryKey(
                name: "PK_INV_STOCK_LEDGER",
                table: "INV_STOCK_LEDGER",
                column: "ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_INV_ITEM_UOM",
                table: "INV_ITEM_UOM",
                column: "ID");

            migrationBuilder.CreateTable(
                name: "POS_BATCH_PREP",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BATCH_NUMBER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    PREP_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    OUTPUT_ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    PLANNED_OUTPUT_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    ACTUAL_OUTPUT_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    OUTPUT_UOM_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    YIELD_FACTOR = table.Column<decimal>(type: "NUMBER(8,4)", nullable: false, defaultValue: 1.0m),
                    TOTAL_RAW_COST = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    UNIT_COST_PRODUCED = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    PREP_NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    IS_POSTED_TO_INVENTORY = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    POSTED_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_BATCH_PREP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_BATCH_PREP_INV_ITEM_OUTPUT_ITEM_ID",
                        column: x => x.OUTPUT_ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_BATCH_PREP_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_FLOOR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    FLOOR_CODE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    FLOOR_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    SORT_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_FLOOR", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_FLOOR_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_GIFT_CARD",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CARD_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    PIN_HASH = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    INITIAL_BALANCE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    CURRENT_BALANCE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    ISSUE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    EXPIRY_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_GIFT_CARD", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_GIFT_CARD_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_PRICE_LIST",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    PRICE_LIST_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    PRICE_LIST_NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    PRICE_LIST_NAME_EN = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    APPLICABLE_ORDER_TYPE = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CURRENCY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    IS_DEFAULT = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_PRICE_LIST", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_PRICE_LIST_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_PRINT_TEMPLATE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TEMPLATE_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    TEMPLATE_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    TEMPLATE_TYPE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    RAW_ESC_POS_PATTERN = table.Column<string>(type: "CLOB", nullable: false),
                    IS_DEFAULT = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_PRINT_TEMPLATE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_PRINT_TEMPLATE_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_PRINTER_ROUTING",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    STATION_NAME = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    PRINTER_NAME_OR_IP = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    ITEM_GROUP_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    COPIES = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 1),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_PRINTER_ROUTING", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_PRINTER_ROUTING_INV_ITEM_GROUP_ITEM_GROUP_ID",
                        column: x => x.ITEM_GROUP_ID,
                        principalTable: "INV_ITEM_GROUP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_POS_PRINTER_ROUTING_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_PROMOTION",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    PROMOTION_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    PROMOTION_NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    PROMOTION_NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    PROMOTION_TYPE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    START_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    END_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    START_TIME = table.Column<TimeSpan>(type: "INTERVAL DAY(8) TO SECOND(7)", nullable: true),
                    END_TIME = table.Column<TimeSpan>(type: "INTERVAL DAY(8) TO SECOND(7)", nullable: true),
                    DAYS_OF_WEEK_MASK = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    PRIORITY = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    CAN_COMBINE_DISCOUNTS = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    MINIMUM_CART_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_PROMOTION", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_PROMOTION_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_TILL",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TILL_CODE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    TILL_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    MACHINE_IDENTIFIER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    IP_ADDRESS = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    DEFAULT_FLOAT_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_TILL", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_TILL_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SYS_ENTITY_TRANSLATION",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ENTITY_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ENTITY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    FIELD_NAME = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    LANG_CODE = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false),
                    TRANSLATION_TEXT = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYS_ENTITY_TRANSLATION", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "POS_BATCH_PREP_LINE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BATCH_PREP_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    RAW_ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    QUANTITY_USED = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    UOM_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    UNIT_COST = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    LINE_COST = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    SCRAP_WASTE_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_BATCH_PREP_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_BATCH_PREP_LINE_INV_ITEM_RAW_ITEM_ID",
                        column: x => x.RAW_ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_BATCH_PREP_LINE_POS_BATCH_PREP_BATCH_PREP_ID",
                        column: x => x.BATCH_PREP_ID,
                        principalTable: "POS_BATCH_PREP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "POS_PRICE_LIST_ITEM",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PRICE_LIST_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    PRICE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    MIN_PRICE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_PRICE_LIST_ITEM", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_PRICE_LIST_ITEM_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_PRICE_LIST_ITEM_POS_PRICE_LIST_PRICE_LIST_ID",
                        column: x => x.PRICE_LIST_ID,
                        principalTable: "POS_PRICE_LIST",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "POS_PROMOTION_RULE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PROMOTION_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    REQUIRED_GROUP_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    REQUIRED_ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    REQUIRED_QUANTITY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 1m),
                    REWARD_ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    REWARD_GROUP_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    REWARD_QUANTITY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 1m),
                    DISCOUNT_PERCENT = table.Column<decimal>(type: "NUMBER(8,4)", nullable: false, defaultValue: 0m),
                    FIXED_BUNDLE_PRICE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_PROMOTION_RULE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_PROMOTION_RULE_POS_PROMOTION_PROMOTION_ID",
                        column: x => x.PROMOTION_ID,
                        principalTable: "POS_PROMOTION",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "POS_SHIFT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TILL_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CASHIER_USER_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SHIFT_NUMBER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    OPENED_AT = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    BLIND_CLOSED_AT = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    CLOSED_AT = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    OPENING_FLOAT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_CASH_SALES = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_CARD_SALES = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_OTHER_SALES = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_CASH_REFUNDS = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_CARD_REFUNDS = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_FLOAT_IN = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_CASH_DROP = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_PAY_OUT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_TIP_PAYOUT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    EXPECTED_CASH_IN_DRAWER = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    COUNTED_CASH_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: true),
                    VARIANCE_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: true),
                    IS_AUDITED = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    AUDITED_BY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    AUDITED_AT = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    AUDIT_NOTES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_SHIFT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_SHIFT_POS_TILL_TILL_ID",
                        column: x => x.TILL_ID,
                        principalTable: "POS_TILL",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_SHIFT_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_SHIFT_SYS_USERS_CASHIER_USER_ID",
                        column: x => x.CASHIER_USER_ID,
                        principalTable: "SYS_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_SHIFT_CASH_MOVEMENT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    SHIFT_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    MOVEMENT_TYPE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    REASON = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    APPROVED_BY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_SHIFT_CASH_MOVEMENT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_SHIFT_CASH_MOVEMENT_POS_SHIFT_SHIFT_ID",
                        column: x => x.SHIFT_ID,
                        principalTable: "POS_SHIFT",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "POS_STAFF_ATTENDANCE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    USER_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SHIFT_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CLOCK_IN_TIME = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    CLOCK_OUT_TIME = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    TOTAL_HOURS_WORKED = table.Column<decimal>(type: "NUMBER(8,2)", nullable: false, defaultValue: 0m),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_STAFF_ATTENDANCE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_STAFF_ATTENDANCE_POS_SHIFT_SHIFT_ID",
                        column: x => x.SHIFT_ID,
                        principalTable: "POS_SHIFT",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_POS_STAFF_ATTENDANCE_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_STAFF_ATTENDANCE_SYS_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "SYS_USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_Z_REPORT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TILL_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    SHIFT_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    Z_SEQUENCE_NUMBER = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    REPORT_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    FIRST_INVOICE_NUMBER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    LAST_INVOICE_NUMBER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    TOTAL_INVOICE_COUNT = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    TOTAL_RETURN_COUNT = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    GROSS_SALES_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_DISCOUNT_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    NET_SALES_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_TAX_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_SERVICE_CHARGE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_REFUND_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    FINAL_TOTAL_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    CASH_PAYMENTS_TOTAL = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    CARD_PAYMENTS_TOTAL = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    CUSTOMER_ACCOUNT_TOTAL = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    OTHER_PAYMENTS_TOTAL = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    OPENING_FLOAT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    CASH_DROP_TOTAL = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    PAY_OUT_TOTAL = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    EXPECTED_DRAWER_CASH = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    ACTUAL_COUNTED_CASH = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    CASH_VARIANCE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    IS_POSTED_TO_GL = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    GL_VOUCHER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    GENERATED_BY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_Z_REPORT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_Z_REPORT_POS_SHIFT_SHIFT_ID",
                        column: x => x.SHIFT_ID,
                        principalTable: "POS_SHIFT",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_POS_Z_REPORT_POS_TILL_TILL_ID",
                        column: x => x.TILL_ID,
                        principalTable: "POS_TILL",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_POS_Z_REPORT_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_EXTERNAL_ORDER",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    PROVIDER = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    EXTERNAL_ORDER_ID = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    EXTERNAL_ORDER_DISPLAY_NO = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    RAW_PAYLOAD_JSON = table.Column<string>(type: "CLOB", nullable: true),
                    CUSTOMER_NAME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    CUSTOMER_PHONE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    DELIVERY_ADDRESS = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    SUBTOTAL_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    COMMISSION_RATE = table.Column<decimal>(type: "NUMBER(8,4)", nullable: false, defaultValue: 0m),
                    COMMISSION_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    NET_PAYABLE_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false, defaultValue: "Received"),
                    CREATED_POS_ORDER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    RECEIVED_AT = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_EXTERNAL_ORDER", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_EXTERNAL_ORDER_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_GIFT_CARD_TRANSACTION",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    GIFT_CARD_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ORDER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    TRANSACTION_TYPE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    BALANCE_BEFORE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    BALANCE_AFTER = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    TRANSACTION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_GIFT_CARD_TRANSACTION", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_GIFT_CARD_TRANSACTION_POS_GIFT_CARD_GIFT_CARD_ID",
                        column: x => x.GIFT_CARD_ID,
                        principalTable: "POS_GIFT_CARD",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "POS_INVENTORY_CONFLICT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ORDER_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SOLD_QUANTITY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    AVAILABLE_STOCK_AT_SYNC = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    DEFICIT_QUANTITY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    RESOLUTION_NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    RESOLVED_BY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    RESOLVED_AT = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_INVENTORY_CONFLICT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_INVENTORY_CONFLICT_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_INVENTORY_CONFLICT_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_ORDER_HEADER",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SHIFT_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TILL_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ORDER_NUMBER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    INVOICE_NUMBER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    CLIENT_UUID = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ORDER_TYPE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CUSTOMER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CUSTOMER_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    TABLE_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    COVERS = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 1),
                    PRICE_LIST_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    SUBTOTAL_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    DISCOUNT_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    DISCOUNT_PERCENT = table.Column<decimal>(type: "NUMBER(8,4)", nullable: false, defaultValue: 0m),
                    DISCOUNT_REASON = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    TAX_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    SERVICE_CHARGE_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    DELIVERY_FEE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TIP_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    PAID_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    CHANGE_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    IS_PAID = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    IS_REFUND = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    ORIGINAL_ORDER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    REFUND_REASON = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    Z_REPORT_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    GL_VOUCHER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    E_INVOICE_QR_CODE = table.Column<string>(type: "CLOB", nullable: true),
                    E_INVOICE_HASH = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: true),
                    INVOICE_COUNTER_VALUE = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_ORDER_HEADER", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_ORDER_HEADER_CUSTOMER_CUSTOMER_ID",
                        column: x => x.CUSTOMER_ID,
                        principalTable: "CUSTOMER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_ORDER_HEADER_GL_VOUCHER_HEADER_GL_VOUCHER_ID",
                        column: x => x.GL_VOUCHER_ID,
                        principalTable: "GL_VOUCHER_HEADER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_ORDER_HEADER_POS_SHIFT_SHIFT_ID",
                        column: x => x.SHIFT_ID,
                        principalTable: "POS_SHIFT",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_ORDER_HEADER_POS_TILL_TILL_ID",
                        column: x => x.TILL_ID,
                        principalTable: "POS_TILL",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_ORDER_HEADER_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_ORDER_LINE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ORDER_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    LINE_NUMBER = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ITEM_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ITEM_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    QUANTITY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    UOM_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    UNIT_PRICE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    COST_PRICE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    DISCOUNT_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    DISCOUNT_PERCENT = table.Column<decimal>(type: "NUMBER(8,4)", nullable: false, defaultValue: 0m),
                    DISCOUNT_REASON = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    TAX_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TAX_PERCENT = table.Column<decimal>(type: "NUMBER(8,4)", nullable: false, defaultValue: 0m),
                    LINE_TOTAL = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    KDS_STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PREP_STATION = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    KDS_SENT_AT = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    KDS_READY_AT = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    IS_SCALE_ITEM = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    SCALE_WEIGHT = table.Column<decimal>(type: "NUMBER(14,4)", nullable: true),
                    SCALE_BARCODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    IS_VOIDED = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    VOID_REASON = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    VOID_APPROVED_BY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    SALES_EMPLOYEE_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    COMMISSION_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    SPECIAL_INSTRUCTIONS = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_ORDER_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_ORDER_LINE_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_ORDER_LINE_POS_ORDER_HEADER_ORDER_ID",
                        column: x => x.ORDER_ID,
                        principalTable: "POS_ORDER_HEADER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "POS_ORDER_PAYMENT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ORDER_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    PAYMENT_METHOD = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    TENDERED_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    CHANGE_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    CARD_NUMBER_MASKED = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: true),
                    CARD_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    TRANSACTION_REFERENCE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    AUTH_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    TERMINAL_ID = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    CHEQUE_NUMBER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    GIFT_CARD_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    LOYALTY_POINTS_REDEEMED = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    PAYMENT_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_ORDER_PAYMENT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_ORDER_PAYMENT_POS_ORDER_HEADER_ORDER_ID",
                        column: x => x.ORDER_ID,
                        principalTable: "POS_ORDER_HEADER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "POS_ORDER_TAX",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ORDER_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ORDER_LINE_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    TAX_RATE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TAX_RATE_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    TAX_PERCENT = table.Column<decimal>(type: "NUMBER(8,4)", nullable: false),
                    TAXABLE_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    TAX_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    IS_INCLUSIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_ORDER_TAX", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_ORDER_TAX_POS_ORDER_HEADER_ORDER_ID",
                        column: x => x.ORDER_ID,
                        principalTable: "POS_ORDER_HEADER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "POS_TABLE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    FLOOR_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TABLE_NUMBER = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    TABLE_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    CAPACITY = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 4),
                    STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    POSITION_X = table.Column<decimal>(type: "NUMBER(10,2)", nullable: false, defaultValue: 0m),
                    POSITION_Y = table.Column<decimal>(type: "NUMBER(10,2)", nullable: false, defaultValue: 0m),
                    WIDTH = table.Column<decimal>(type: "NUMBER(10,2)", nullable: false, defaultValue: 80m),
                    HEIGHT = table.Column<decimal>(type: "NUMBER(10,2)", nullable: false, defaultValue: 80m),
                    SHAPE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false, defaultValue: "Square"),
                    ACTIVE_ORDER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    STATUS_CHANGED_AT = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_TABLE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_TABLE_POS_FLOOR_FLOOR_ID",
                        column: x => x.FLOOR_ID,
                        principalTable: "POS_FLOOR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_POS_TABLE_POS_ORDER_HEADER_ACTIVE_ORDER_ID",
                        column: x => x.ACTIVE_ORDER_ID,
                        principalTable: "POS_ORDER_HEADER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "POS_ORDER_LINE_MODIFIER",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ORDER_LINE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    MODIFIER_ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    MODIFIER_NAME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    QUANTITY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 1m),
                    UNIT_PRICE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    EXTRA_PRICE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_ORDER_LINE_MODIFIER", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_ORDER_LINE_MODIFIER_POS_ORDER_LINE_ORDER_LINE_ID",
                        column: x => x.ORDER_LINE_ID,
                        principalTable: "POS_ORDER_LINE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "POS_RESERVATION",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    RESERVATION_NUMBER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    CUSTOMER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CUSTOMER_NAME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    CUSTOMER_PHONE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    CUSTOMER_EMAIL = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    RESERVATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    START_TIME = table.Column<TimeSpan>(type: "INTERVAL DAY(8) TO SECOND(7)", nullable: false),
                    END_TIME = table.Column<TimeSpan>(type: "INTERVAL DAY(8) TO SECOND(7)", nullable: false),
                    GUEST_COUNT = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 1),
                    TABLE_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    RESOURCE_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    RESOURCE_IDENTIFIER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    STAFF_EMPLOYEE_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DEPOSIT_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    IS_DEPOSIT_PAID = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    DEPOSIT_PAYMENT_REF = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    SPECIAL_REQUESTS = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_RESERVATION", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_RESERVATION_CUSTOMER_CUSTOMER_ID",
                        column: x => x.CUSTOMER_ID,
                        principalTable: "CUSTOMER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_RESERVATION_POS_TABLE_TABLE_ID",
                        column: x => x.TABLE_ID,
                        principalTable: "POS_TABLE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_POS_RESERVATION_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_POS_BATCH_PREP_BRANCH_ID_BATCH_NUMBER",
                table: "POS_BATCH_PREP",
                columns: new[] { "BRANCH_ID", "BATCH_NUMBER" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_BATCH_PREP_OUTPUT_ITEM_ID",
                table: "POS_BATCH_PREP",
                column: "OUTPUT_ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_BATCH_PREP_LINE_BATCH_PREP_ID",
                table: "POS_BATCH_PREP_LINE",
                column: "BATCH_PREP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_BATCH_PREP_LINE_RAW_ITEM_ID",
                table: "POS_BATCH_PREP_LINE",
                column: "RAW_ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_EXTERNAL_ORDER_BRANCH_ID_PROVIDER_EXTERNAL_ORDER_ID",
                table: "POS_EXTERNAL_ORDER",
                columns: new[] { "BRANCH_ID", "PROVIDER", "EXTERNAL_ORDER_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_EXTERNAL_ORDER_CREATED_POS_ORDER_ID",
                table: "POS_EXTERNAL_ORDER",
                column: "CREATED_POS_ORDER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_FLOOR_BRANCH_ID_FLOOR_CODE",
                table: "POS_FLOOR",
                columns: new[] { "BRANCH_ID", "FLOOR_CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_GIFT_CARD_BRANCH_ID_CARD_CODE",
                table: "POS_GIFT_CARD",
                columns: new[] { "BRANCH_ID", "CARD_CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_GIFT_CARD_TRANSACTION_GIFT_CARD_ID",
                table: "POS_GIFT_CARD_TRANSACTION",
                column: "GIFT_CARD_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_GIFT_CARD_TRANSACTION_ORDER_ID",
                table: "POS_GIFT_CARD_TRANSACTION",
                column: "ORDER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_INVENTORY_CONFLICT_BRANCH_ID",
                table: "POS_INVENTORY_CONFLICT",
                column: "BRANCH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_INVENTORY_CONFLICT_ITEM_ID",
                table: "POS_INVENTORY_CONFLICT",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_INVENTORY_CONFLICT_ORDER_ID",
                table: "POS_INVENTORY_CONFLICT",
                column: "ORDER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_ORDER_HEADER_BRANCH_ID_CLIENT_UUID",
                table: "POS_ORDER_HEADER",
                columns: new[] { "BRANCH_ID", "CLIENT_UUID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_ORDER_HEADER_BRANCH_ID_ORDER_NUMBER",
                table: "POS_ORDER_HEADER",
                columns: new[] { "BRANCH_ID", "ORDER_NUMBER" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_ORDER_HEADER_CUSTOMER_ID",
                table: "POS_ORDER_HEADER",
                column: "CUSTOMER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_ORDER_HEADER_GL_VOUCHER_ID",
                table: "POS_ORDER_HEADER",
                column: "GL_VOUCHER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_ORDER_HEADER_SHIFT_ID",
                table: "POS_ORDER_HEADER",
                column: "SHIFT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_ORDER_HEADER_TABLE_ID",
                table: "POS_ORDER_HEADER",
                column: "TABLE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_ORDER_HEADER_TILL_ID",
                table: "POS_ORDER_HEADER",
                column: "TILL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_ORDER_LINE_ITEM_ID",
                table: "POS_ORDER_LINE",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_ORDER_LINE_ORDER_ID",
                table: "POS_ORDER_LINE",
                column: "ORDER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_ORDER_LINE_MODIFIER_ORDER_LINE_ID",
                table: "POS_ORDER_LINE_MODIFIER",
                column: "ORDER_LINE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_ORDER_PAYMENT_ORDER_ID",
                table: "POS_ORDER_PAYMENT",
                column: "ORDER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_ORDER_TAX_ORDER_ID",
                table: "POS_ORDER_TAX",
                column: "ORDER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_PRICE_LIST_BRANCH_ID_PRICE_LIST_CODE",
                table: "POS_PRICE_LIST",
                columns: new[] { "BRANCH_ID", "PRICE_LIST_CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_PRICE_LIST_ITEM_ITEM_ID",
                table: "POS_PRICE_LIST_ITEM",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_PRICE_LIST_ITEM_PRICE_LIST_ID_ITEM_ID",
                table: "POS_PRICE_LIST_ITEM",
                columns: new[] { "PRICE_LIST_ID", "ITEM_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_PRINT_TEMPLATE_BRANCH_ID_TEMPLATE_CODE",
                table: "POS_PRINT_TEMPLATE",
                columns: new[] { "BRANCH_ID", "TEMPLATE_CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_PRINTER_ROUTING_BRANCH_ID",
                table: "POS_PRINTER_ROUTING",
                column: "BRANCH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_PRINTER_ROUTING_ITEM_GROUP_ID",
                table: "POS_PRINTER_ROUTING",
                column: "ITEM_GROUP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_PROMOTION_BRANCH_ID_PROMOTION_CODE",
                table: "POS_PROMOTION",
                columns: new[] { "BRANCH_ID", "PROMOTION_CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_PROMOTION_RULE_PROMOTION_ID",
                table: "POS_PROMOTION_RULE",
                column: "PROMOTION_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_RESERVATION_BRANCH_ID_RESERVATION_NUMBER",
                table: "POS_RESERVATION",
                columns: new[] { "BRANCH_ID", "RESERVATION_NUMBER" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_RESERVATION_CUSTOMER_ID",
                table: "POS_RESERVATION",
                column: "CUSTOMER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_RESERVATION_TABLE_ID",
                table: "POS_RESERVATION",
                column: "TABLE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_SHIFT_BRANCH_ID_SHIFT_NUMBER",
                table: "POS_SHIFT",
                columns: new[] { "BRANCH_ID", "SHIFT_NUMBER" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_SHIFT_CASHIER_USER_ID",
                table: "POS_SHIFT",
                column: "CASHIER_USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_SHIFT_TILL_ID",
                table: "POS_SHIFT",
                column: "TILL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_SHIFT_CASH_MOVEMENT_SHIFT_ID",
                table: "POS_SHIFT_CASH_MOVEMENT",
                column: "SHIFT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_STAFF_ATTENDANCE_BRANCH_ID",
                table: "POS_STAFF_ATTENDANCE",
                column: "BRANCH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_STAFF_ATTENDANCE_SHIFT_ID",
                table: "POS_STAFF_ATTENDANCE",
                column: "SHIFT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_STAFF_ATTENDANCE_USER_ID",
                table: "POS_STAFF_ATTENDANCE",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_TABLE_ACTIVE_ORDER_ID",
                table: "POS_TABLE",
                column: "ACTIVE_ORDER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_TABLE_FLOOR_ID_TABLE_NUMBER",
                table: "POS_TABLE",
                columns: new[] { "FLOOR_ID", "TABLE_NUMBER" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_TILL_BRANCH_ID_TILL_CODE",
                table: "POS_TILL",
                columns: new[] { "BRANCH_ID", "TILL_CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_Z_REPORT_BRANCH_ID_Z_SEQUENCE_NUMBER",
                table: "POS_Z_REPORT",
                columns: new[] { "BRANCH_ID", "Z_SEQUENCE_NUMBER" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_Z_REPORT_SHIFT_ID",
                table: "POS_Z_REPORT",
                column: "SHIFT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_Z_REPORT_TILL_ID",
                table: "POS_Z_REPORT",
                column: "TILL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_ENTITY_TRANSLATION_ENTITY_TYPE_ENTITY_ID_FIELD_NAME_LANG_CODE",
                table: "SYS_ENTITY_TRANSLATION",
                columns: new[] { "ENTITY_TYPE", "ENTITY_ID", "FIELD_NAME", "LANG_CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SYS_ENTITY_TRANSLATION_ENTITY_TYPE_ENTITY_ID_LANG_CODE",
                table: "SYS_ENTITY_TRANSLATION",
                columns: new[] { "ENTITY_TYPE", "ENTITY_ID", "LANG_CODE" });

            migrationBuilder.AddForeignKey(
                name: "FK_INV_ITEM_UOM_INV_ITEM_ITEM_ID",
                table: "INV_ITEM_UOM",
                column: "ITEM_ID",
                principalTable: "INV_ITEM",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_LOT_MASTER_INV_ITEM_ITEM_ID",
                table: "INV_LOT_MASTER",
                column: "ITEM_ID",
                principalTable: "INV_ITEM",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_SERIAL_MASTER_INV_ITEM_ITEM_ID",
                table: "INV_SERIAL_MASTER",
                column: "ITEM_ID",
                principalTable: "INV_ITEM",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_STOCK_BALANCE_INV_ITEM_ITEM_ID",
                table: "INV_STOCK_BALANCE",
                column: "ITEM_ID",
                principalTable: "INV_ITEM",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_STOCK_BALANCE_INV_WAREHOUSE_WAREHOUSE_ID",
                table: "INV_STOCK_BALANCE",
                column: "WAREHOUSE_ID",
                principalTable: "INV_WAREHOUSE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_STOCK_LEDGER_INV_ITEM_ITEM_ID",
                table: "INV_STOCK_LEDGER",
                column: "ITEM_ID",
                principalTable: "INV_ITEM",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_STOCK_LEDGER_INV_WAREHOUSE_WAREHOUSE_ID",
                table: "INV_STOCK_LEDGER",
                column: "WAREHOUSE_ID",
                principalTable: "INV_WAREHOUSE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_POS_EXTERNAL_ORDER_POS_ORDER_HEADER_CREATED_POS_ORDER_ID",
                table: "POS_EXTERNAL_ORDER",
                column: "CREATED_POS_ORDER_ID",
                principalTable: "POS_ORDER_HEADER",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_POS_GIFT_CARD_TRANSACTION_POS_ORDER_HEADER_ORDER_ID",
                table: "POS_GIFT_CARD_TRANSACTION",
                column: "ORDER_ID",
                principalTable: "POS_ORDER_HEADER",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_POS_INVENTORY_CONFLICT_POS_ORDER_HEADER_ORDER_ID",
                table: "POS_INVENTORY_CONFLICT",
                column: "ORDER_ID",
                principalTable: "POS_ORDER_HEADER",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_POS_ORDER_HEADER_POS_TABLE_TABLE_ID",
                table: "POS_ORDER_HEADER",
                column: "TABLE_ID",
                principalTable: "POS_TABLE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_INV_ITEM_UOM_INV_ITEM_ITEM_ID",
                table: "INV_ITEM_UOM");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_LOT_MASTER_INV_ITEM_ITEM_ID",
                table: "INV_LOT_MASTER");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_SERIAL_MASTER_INV_ITEM_ITEM_ID",
                table: "INV_SERIAL_MASTER");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_STOCK_BALANCE_INV_ITEM_ITEM_ID",
                table: "INV_STOCK_BALANCE");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_STOCK_BALANCE_INV_WAREHOUSE_WAREHOUSE_ID",
                table: "INV_STOCK_BALANCE");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_STOCK_LEDGER_INV_ITEM_ITEM_ID",
                table: "INV_STOCK_LEDGER");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_STOCK_LEDGER_INV_WAREHOUSE_WAREHOUSE_ID",
                table: "INV_STOCK_LEDGER");

            migrationBuilder.DropForeignKey(
                name: "FK_POS_TABLE_POS_ORDER_HEADER_ACTIVE_ORDER_ID",
                table: "POS_TABLE");

            migrationBuilder.DropTable(
                name: "POS_BATCH_PREP_LINE");

            migrationBuilder.DropTable(
                name: "POS_EXTERNAL_ORDER");

            migrationBuilder.DropTable(
                name: "POS_GIFT_CARD_TRANSACTION");

            migrationBuilder.DropTable(
                name: "POS_INVENTORY_CONFLICT");

            migrationBuilder.DropTable(
                name: "POS_ORDER_LINE_MODIFIER");

            migrationBuilder.DropTable(
                name: "POS_ORDER_PAYMENT");

            migrationBuilder.DropTable(
                name: "POS_ORDER_TAX");

            migrationBuilder.DropTable(
                name: "POS_PRICE_LIST_ITEM");

            migrationBuilder.DropTable(
                name: "POS_PRINT_TEMPLATE");

            migrationBuilder.DropTable(
                name: "POS_PRINTER_ROUTING");

            migrationBuilder.DropTable(
                name: "POS_PROMOTION_RULE");

            migrationBuilder.DropTable(
                name: "POS_RESERVATION");

            migrationBuilder.DropTable(
                name: "POS_SHIFT_CASH_MOVEMENT");

            migrationBuilder.DropTable(
                name: "POS_STAFF_ATTENDANCE");

            migrationBuilder.DropTable(
                name: "POS_Z_REPORT");

            migrationBuilder.DropTable(
                name: "SYS_ENTITY_TRANSLATION");

            migrationBuilder.DropTable(
                name: "POS_BATCH_PREP");

            migrationBuilder.DropTable(
                name: "POS_GIFT_CARD");

            migrationBuilder.DropTable(
                name: "POS_ORDER_LINE");

            migrationBuilder.DropTable(
                name: "POS_PRICE_LIST");

            migrationBuilder.DropTable(
                name: "POS_PROMOTION");

            migrationBuilder.DropTable(
                name: "POS_ORDER_HEADER");

            migrationBuilder.DropTable(
                name: "POS_SHIFT");

            migrationBuilder.DropTable(
                name: "POS_TABLE");

            migrationBuilder.DropTable(
                name: "POS_TILL");

            migrationBuilder.DropTable(
                name: "POS_FLOOR");

            migrationBuilder.DropPrimaryKey(
                name: "PK_INV_STOCK_LEDGER",
                table: "INV_STOCK_LEDGER");

            migrationBuilder.DropPrimaryKey(
                name: "PK_INV_ITEM_UOM",
                table: "INV_ITEM_UOM");

            migrationBuilder.DropColumn(
                name: "LINE_COST",
                table: "TRX_DOCUMENT_LINE");

            migrationBuilder.DropColumn(
                name: "LINE_PROFIT",
                table: "TRX_DOCUMENT_LINE");

            migrationBuilder.DropColumn(
                name: "PROFIT_MARGIN_PERCENT",
                table: "TRX_DOCUMENT_LINE");

            migrationBuilder.DropColumn(
                name: "PROFIT_MARGIN_PERCENT",
                table: "TRX_DOCUMENT_HEADER");

            migrationBuilder.DropColumn(
                name: "TOTAL_COST",
                table: "TRX_DOCUMENT_HEADER");

            migrationBuilder.DropColumn(
                name: "TOTAL_PROFIT",
                table: "TRX_DOCUMENT_HEADER");

            migrationBuilder.DropColumn(
                name: "IS_SHOW_IN_POS",
                table: "INV_ITEM_GROUP");

            migrationBuilder.RenameTable(
                name: "INV_STOCK_LEDGER",
                newName: "INV_STOCK_LEDGER_ENTRY");

            migrationBuilder.RenameTable(
                name: "INV_ITEM_UOM",
                newName: "INV_ITEM_UOM_CONVERSION");

            migrationBuilder.RenameIndex(
                name: "IX_INV_STOCK_LEDGER_WAREHOUSE_ID",
                table: "INV_STOCK_LEDGER_ENTRY",
                newName: "IX_INV_STOCK_LEDGER_ENTRY_WAREHOUSE_ID");

            migrationBuilder.RenameIndex(
                name: "IX_INV_STOCK_LEDGER_ITEM_ID",
                table: "INV_STOCK_LEDGER_ENTRY",
                newName: "IX_INV_STOCK_LEDGER_ENTRY_ITEM_ID");

            migrationBuilder.RenameIndex(
                name: "IX_INV_ITEM_UOM_ITEM_ID",
                table: "INV_ITEM_UOM_CONVERSION",
                newName: "IX_INV_ITEM_UOM_CONVERSION_ITEM_ID");

            migrationBuilder.AlterColumn<byte>(
                name: "STOCK_DIRECTION",
                table: "TRX_TRANSACTION_TYPE",
                type: "NUMBER(2)",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "UOM_CODE",
                table: "TRX_DOCUMENT_LINE",
                type: "NVARCHAR2(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(6)");

            migrationBuilder.AlterColumn<string>(
                name: "ZONE_CODE",
                table: "INV_ZONE",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(6)");

            migrationBuilder.AlterColumn<string>(
                name: "WAREHOUSE_CODE",
                table: "INV_WAREHOUSE",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(6)");

            migrationBuilder.AlterColumn<string>(
                name: "UOM_CODE",
                table: "INV_OPENING_LINE",
                type: "NVARCHAR2(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(6)");

            migrationBuilder.AlterColumn<string>(
                name: "GROUP_CODE",
                table: "INV_ITEM_GROUP",
                type: "NVARCHAR2(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)");

            migrationBuilder.AlterColumn<string>(
                name: "UOM_CODE",
                table: "INV_ITEM_BARCODE",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(6)");

            migrationBuilder.AlterColumn<string>(
                name: "WEIGHT_UNIT",
                table: "INV_ITEM",
                type: "NVARCHAR2(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "NUMBER(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UOM_BASE",
                table: "INV_ITEM",
                type: "NVARCHAR2(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(6)");

            migrationBuilder.AlterColumn<string>(
                name: "UOM_CODE",
                table: "INV_BOM_LINE",
                type: "NVARCHAR2(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(6)");

            migrationBuilder.AlterColumn<string>(
                name: "UOM_CODE",
                table: "INV_BOM_HEADER",
                type: "NVARCHAR2(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(6)");

            migrationBuilder.AlterColumn<string>(
                name: "BOM_CODE",
                table: "INV_BOM_HEADER",
                type: "NVARCHAR2(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)");

            migrationBuilder.AlterColumn<string>(
                name: "BIN_CODE",
                table: "INV_BIN",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(6)");

            migrationBuilder.AlterColumn<string>(
                name: "UOM_CODE",
                table: "INV_STOCK_LEDGER_ENTRY",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(6)");

            migrationBuilder.AlterColumn<string>(
                name: "TRANSACTION_TYPE",
                table: "INV_STOCK_LEDGER_ENTRY",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(6)");

            migrationBuilder.AlterColumn<string>(
                name: "DIRECTION",
                table: "INV_STOCK_LEDGER_ENTRY",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "UOM_CODE",
                table: "INV_ITEM_UOM_CONVERSION",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(6)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_INV_STOCK_LEDGER_ENTRY",
                table: "INV_STOCK_LEDGER_ENTRY",
                column: "ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_INV_ITEM_UOM_CONVERSION",
                table: "INV_ITEM_UOM_CONVERSION",
                column: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_INV_ITEM_UOM_CONVERSION_INV_ITEM_ITEM_ID",
                table: "INV_ITEM_UOM_CONVERSION",
                column: "ITEM_ID",
                principalTable: "INV_ITEM",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_LOT_MASTER_INV_ITEM_ITEM_ID",
                table: "INV_LOT_MASTER",
                column: "ITEM_ID",
                principalTable: "INV_ITEM",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_SERIAL_MASTER_INV_ITEM_ITEM_ID",
                table: "INV_SERIAL_MASTER",
                column: "ITEM_ID",
                principalTable: "INV_ITEM",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_STOCK_BALANCE_INV_ITEM_ITEM_ID",
                table: "INV_STOCK_BALANCE",
                column: "ITEM_ID",
                principalTable: "INV_ITEM",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_STOCK_BALANCE_INV_WAREHOUSE_WAREHOUSE_ID",
                table: "INV_STOCK_BALANCE",
                column: "WAREHOUSE_ID",
                principalTable: "INV_WAREHOUSE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_STOCK_LEDGER_ENTRY_INV_ITEM_ITEM_ID",
                table: "INV_STOCK_LEDGER_ENTRY",
                column: "ITEM_ID",
                principalTable: "INV_ITEM",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_STOCK_LEDGER_ENTRY_INV_WAREHOUSE_WAREHOUSE_ID",
                table: "INV_STOCK_LEDGER_ENTRY",
                column: "WAREHOUSE_ID",
                principalTable: "INV_WAREHOUSE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
