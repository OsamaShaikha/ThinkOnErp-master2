using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAndTradeModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SYS_AUDIT_LOG_SYSTEM",
                table: "SYS_AUDIT_LOG");

            migrationBuilder.DropIndex(
                name: "IX_SYS_AUDIT_LOG_BUSINESS_MODULE",
                table: "SYS_AUDIT_LOG");

            migrationBuilder.EnsureSchema(
                name: "THINKON_AUDIT");

            migrationBuilder.EnsureSchema(
                name: "THINKON_SUPPORT");

            migrationBuilder.RenameTable(
                name: "SYS_AUDIT_LOG_ARCHIVE",
                newName: "SYS_AUDIT_LOG_ARCHIVE",
                newSchema: "THINKON_AUDIT");

            migrationBuilder.RenameTable(
                name: "SYS_AUDIT_LOG",
                newName: "SYS_AUDIT_LOG",
                newSchema: "THINKON_AUDIT");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "THINKON_AUDIT",
                table: "SYS_AUDIT_LOG_ARCHIVE",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "THINKON_AUDIT",
                table: "SYS_AUDIT_LOG",
                newName: "ID");

            migrationBuilder.CreateTable(
                name: "INV_ITEM_GROUP",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    PARENT_GROUP_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    GROUP_CODE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    GROUP_NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    GROUP_NAME_EN = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    GROUP_LEVEL = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 1),
                    GL_CONTROL_ACCOUNT = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    GL_COGS_ACCOUNT = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    GL_REVENUE_ACCOUNT = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    GL_ADJUSTMENT_ACCOUNT = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ITEM_GROUP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_ITEM_GROUP_INV_ITEM_GROUP_PARENT_GROUP_ID",
                        column: x => x.PARENT_GROUP_ID,
                        principalTable: "INV_ITEM_GROUP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_OPENING_BATCH",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BATCH_NO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    BATCH_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    FISCAL_YEAR_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    TOTAL_QUANTITY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 0m),
                    TOTAL_VALUATION_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    STATUS_CODE = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 1),
                    JOURNAL_ENTRY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    POSTED_AT = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    POSTED_BY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_OPENING_BATCH", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "INV_WAREHOUSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    WAREHOUSE_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    WAREHOUSE_NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    WAREHOUSE_NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    WAREHOUSE_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ADDRESS = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    ENABLE_BIN_TRACKING = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_WAREHOUSE", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TRX_DOC_TYPE",
                columns: table => new
                {
                    TYPE_CODE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TYPE_KEY = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    TYPE_NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    TYPE_NAME_EN = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    MODULE_CODE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    DOC_PREFIX = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false),
                    RESET_POLICY = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "YEARLY"),
                    IS_SYSTEM_RESERVED = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRX_DOC_TYPE", x => x.TYPE_CODE);
                });

            migrationBuilder.CreateTable(
                name: "INV_ITEM",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ITEM_CODE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    ITEM_NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ITEM_NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    MAIN_GROUP_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SUB_GROUP_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    ITEM_TYPE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    UOM_BASE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    COSTING_METHOD = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "WeightedAverage"),
                    STANDARD_COST = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    SERIAL_TRACKING = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    LOT_TRACKING = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    EXPIRY_TRACKING = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    SHELF_LIFE_DAYS = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ALLOW_NEGATIVE_STOCK = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    REORDER_POINT = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 0m),
                    SAFETY_STOCK = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 0m),
                    MIN_ORDER_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 0m),
                    LEAD_TIME_DAYS = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    WEIGHT = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 0m),
                    WEIGHT_UNIT = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: true),
                    GL_CONTROL_ACCOUNT = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    GL_REVENUE_ACCOUNT = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    GL_COGS_ACCOUNT = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    COUNTRY_OF_ORIGIN = table.Column<string>(type: "NVARCHAR2(3)", maxLength: 3, nullable: true),
                    HS_CODE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ITEM", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_ITEM_INV_ITEM_GROUP_MAIN_GROUP_ID",
                        column: x => x.MAIN_GROUP_ID,
                        principalTable: "INV_ITEM_GROUP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_ITEM_INV_ITEM_GROUP_SUB_GROUP_ID",
                        column: x => x.SUB_GROUP_ID,
                        principalTable: "INV_ITEM_GROUP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_COUNT_SESSION",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    SESSION_NO = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    WAREHOUSE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    COUNT_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    IS_BLIND_COUNT = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    FREEZE_STOCK = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    START_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    END_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_COUNT_SESSION", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_COUNT_SESSION_INV_WAREHOUSE_WAREHOUSE_ID",
                        column: x => x.WAREHOUSE_ID,
                        principalTable: "INV_WAREHOUSE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "INV_ZONE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    WAREHOUSE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ZONE_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ZONE_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ZONE_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ZONE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_ZONE_INV_WAREHOUSE_WAREHOUSE_ID",
                        column: x => x.WAREHOUSE_ID,
                        principalTable: "INV_WAREHOUSE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRX_TRANSACTION_TYPE",
                columns: table => new
                {
                    TRX_CODE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DOC_TYPE_CODE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TRX_KEY = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    TRX_NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    TRX_NAME_EN = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    AFFECTS_STOCK = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    STOCK_DIRECTION = table.Column<byte>(type: "NUMBER(2)", nullable: false, defaultValue: (byte)0),
                    REQUIRES_WAREHOUSE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    AFFECTS_GL = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    AFFECTS_PARTY_BALANCE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    POSTING_RULE_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    REQUIRES_PARTY = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    REQUIRES_PRICE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    REQUIRES_COST = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRX_TRANSACTION_TYPE", x => x.TRX_CODE);
                    table.ForeignKey(
                        name: "FK_TRX_TRANSACTION_TYPE_TRX_DOC_TYPE_DOC_TYPE_CODE",
                        column: x => x.DOC_TYPE_CODE,
                        principalTable: "TRX_DOC_TYPE",
                        principalColumn: "TYPE_CODE",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "INV_BOM_HEADER",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BOM_CODE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    BOM_NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    BOM_NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    PARENT_ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    OUTPUT_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 1m),
                    UOM_CODE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    BOM_TYPE_CODE = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 1),
                    LABOR_COST = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    OVERHEAD_COST = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    IS_DEFAULT = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_BOM_HEADER", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_BOM_HEADER_INV_ITEM_PARENT_ITEM_ID",
                        column: x => x.PARENT_ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_COST_LAYER",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    WAREHOUSE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    RECEIVED_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    REMAINING_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    UNIT_COST = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    RECEIVED_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    LOT_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    SOURCE_LEDGER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_COST_LAYER", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_COST_LAYER_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_INV_COST_LAYER_INV_WAREHOUSE_WAREHOUSE_ID",
                        column: x => x.WAREHOUSE_ID,
                        principalTable: "INV_WAREHOUSE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "INV_ITEM_BARCODE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BARCODE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    BARCODE_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    UOM_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ITEM_BARCODE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_ITEM_BARCODE_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "INV_ITEM_UOM_CONVERSION",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    UOM_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    CONVERSION_FACTOR = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    IS_DEFAULT_PURCHASE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    IS_DEFAULT_SALES = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ITEM_UOM_CONVERSION", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_ITEM_UOM_CONVERSION_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "INV_LOT_MASTER",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    LOT_NUMBER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    SUPPLIER_LOT = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    MANUFACTURING_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    EXPIRY_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_LOT_MASTER", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_LOT_MASTER_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "INV_OPENING_LINE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BATCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    WAREHOUSE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BIN_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    UOM_CODE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    UOM_FACTOR = table.Column<decimal>(type: "NUMBER(18,6)", nullable: false, defaultValue: 1m),
                    QUANTITY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    BASE_QUANTITY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    UNIT_COST = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    TOTAL_COST = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    LOT_NUMBER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    SERIAL_NUMBER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    EXPIRY_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_OPENING_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_OPENING_LINE_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_OPENING_LINE_INV_OPENING_BATCH_BATCH_ID",
                        column: x => x.BATCH_ID,
                        principalTable: "INV_OPENING_BATCH",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_INV_OPENING_LINE_INV_WAREHOUSE_WAREHOUSE_ID",
                        column: x => x.WAREHOUSE_ID,
                        principalTable: "INV_WAREHOUSE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_RESERVATION",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    WAREHOUSE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    LOT_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    SERIAL_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    RESERVED_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    SOURCE_DOC_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    SOURCE_DOC_ID = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    SOURCE_LINE_ID = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    EXPIRY_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_RESERVATION", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_RESERVATION_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_INV_RESERVATION_INV_WAREHOUSE_WAREHOUSE_ID",
                        column: x => x.WAREHOUSE_ID,
                        principalTable: "INV_WAREHOUSE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "INV_STOCK_BALANCE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    WAREHOUSE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BIN_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    ON_HAND_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    RESERVED_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    ON_ORDER_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    AVG_COST = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    LAST_RECEIPT_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    LAST_ISSUE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    UPDATED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_STOCK_BALANCE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_STOCK_BALANCE_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_INV_STOCK_BALANCE_INV_WAREHOUSE_WAREHOUSE_ID",
                        column: x => x.WAREHOUSE_ID,
                        principalTable: "INV_WAREHOUSE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "INV_STOCK_LEDGER_ENTRY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    WAREHOUSE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BIN_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    TRANSACTION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    TRANSACTION_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    DIRECTION = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    QUANTITY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    UOM_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    UNIT_COST = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    RUNNING_BALANCE_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    RUNNING_BALANCE_VALUE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    LOT_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    SERIAL_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    LPN_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    COST_LAYER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    SOURCE_MODULE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    SOURCE_DOC_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    SOURCE_DOC_ID = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    SOURCE_LINE_ID = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    JOURNAL_ENTRY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    POSTING_RULE_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_STOCK_LEDGER_ENTRY", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_STOCK_LEDGER_ENTRY_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_INV_STOCK_LEDGER_ENTRY_INV_WAREHOUSE_WAREHOUSE_ID",
                        column: x => x.WAREHOUSE_ID,
                        principalTable: "INV_WAREHOUSE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "INV_BIN",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ZONE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BIN_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    MAX_WEIGHT = table.Column<decimal>(type: "NUMBER(14,4)", nullable: true),
                    MAX_VOLUME = table.Column<decimal>(type: "NUMBER(18,4)", nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_BIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_BIN_INV_ZONE_ZONE_ID",
                        column: x => x.ZONE_ID,
                        principalTable: "INV_ZONE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRX_DOCUMENT_HEADER",
                columns: table => new
                {
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    DOC_YEAR = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DOC_TYPE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TRX_TYPE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DOC_NO = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    DOC_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    DUE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    PARTY_TYPE_CODE = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    PARTY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    PARTY_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    FROM_WAREHOUSE_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    TO_WAREHOUSE_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CURRENCY_CODE = table.Column<string>(type: "NVARCHAR2(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    EXCHANGE_RATE = table.Column<decimal>(type: "NUMBER(14,6)", nullable: false, defaultValue: 1m),
                    PAYMENT_METHOD_CODE = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 1),
                    TOTAL_GROSS = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    DISCOUNT_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_NET_BEFORE_TAX = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TAX_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TOTAL_NET = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    PAID_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    REMAINING_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    BASE_BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    BASE_DOC_YEAR = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    BASE_DOC_TYPE = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    BASE_DOC_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    DOC_STATUS_CODE = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 1),
                    IS_POSTED_GL = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    IS_POSTED_STOCK = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    JOURNAL_ENTRY_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    POSTED_BY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    POSTED_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRX_DOCUMENT_HEADER", x => new { x.BRANCH_ID, x.DOC_YEAR, x.DOC_TYPE, x.ID });
                    table.ForeignKey(
                        name: "FK_TRX_DOCUMENT_HEADER_INV_WAREHOUSE_FROM_WAREHOUSE_ID",
                        column: x => x.FROM_WAREHOUSE_ID,
                        principalTable: "INV_WAREHOUSE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRX_DOCUMENT_HEADER_INV_WAREHOUSE_TO_WAREHOUSE_ID",
                        column: x => x.TO_WAREHOUSE_ID,
                        principalTable: "INV_WAREHOUSE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRX_DOCUMENT_HEADER_TRX_DOC_TYPE_DOC_TYPE",
                        column: x => x.DOC_TYPE,
                        principalTable: "TRX_DOC_TYPE",
                        principalColumn: "TYPE_CODE",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRX_DOCUMENT_HEADER_TRX_TRANSACTION_TYPE_TRX_TYPE",
                        column: x => x.TRX_TYPE,
                        principalTable: "TRX_TRANSACTION_TYPE",
                        principalColumn: "TRX_CODE",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_BOM_LINE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BOM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    LINE_NO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    COMPONENT_ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    UOM_CODE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    UOM_FACTOR = table.Column<decimal>(type: "NUMBER(18,6)", nullable: false, defaultValue: 1m),
                    QUANTITY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    SCRAP_PERCENT = table.Column<decimal>(type: "NUMBER(7,4)", nullable: false, defaultValue: 0m),
                    COST_SHARE_PERCENT = table.Column<decimal>(type: "NUMBER(7,4)", nullable: false, defaultValue: 0m),
                    ALLOW_SUBSTITUTE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    SUBSTITUTE_ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_BOM_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_BOM_LINE_INV_BOM_HEADER_BOM_ID",
                        column: x => x.BOM_ID,
                        principalTable: "INV_BOM_HEADER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_INV_BOM_LINE_INV_ITEM_COMPONENT_ITEM_ID",
                        column: x => x.COMPONENT_ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_BOM_LINE_INV_ITEM_SUBSTITUTE_ITEM_ID",
                        column: x => x.SUBSTITUTE_ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_COUNT_LINE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COUNT_SESSION_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    BIN_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    LOT_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    SYSTEM_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    COUNTED_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    VARIANCE_QTY = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    COUNTED_BY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    COUNTED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_COUNT_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_COUNT_LINE_INV_BIN_BIN_ID",
                        column: x => x.BIN_ID,
                        principalTable: "INV_BIN",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_INV_COUNT_LINE_INV_COUNT_SESSION_COUNT_SESSION_ID",
                        column: x => x.COUNT_SESSION_ID,
                        principalTable: "INV_COUNT_SESSION",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_INV_COUNT_LINE_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "INV_SERIAL_MASTER",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SERIAL_NUMBER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    LOT_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    CURRENT_WAREHOUSE_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CURRENT_BIN_ID = table.Column<long>(type: "NUMBER(19)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_SERIAL_MASTER", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INV_SERIAL_MASTER_INV_BIN_CURRENT_BIN_ID",
                        column: x => x.CURRENT_BIN_ID,
                        principalTable: "INV_BIN",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_INV_SERIAL_MASTER_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_INV_SERIAL_MASTER_INV_LOT_MASTER_LOT_ID",
                        column: x => x.LOT_ID,
                        principalTable: "INV_LOT_MASTER",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_INV_SERIAL_MASTER_INV_WAREHOUSE_CURRENT_WAREHOUSE_ID",
                        column: x => x.CURRENT_WAREHOUSE_ID,
                        principalTable: "INV_WAREHOUSE",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "TRX_DOCUMENT_LINE",
                columns: table => new
                {
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    DOC_YEAR = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DOC_TYPE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DOC_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    LINE_NO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TRX_TYPE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ITEM_DESCRIPTION = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    UOM_CODE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    UOM_FACTOR = table.Column<decimal>(type: "NUMBER(18,6)", nullable: false, defaultValue: 1m),
                    QUANTITY_IN = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 0m),
                    QUANTITY_OUT = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 0m),
                    BASE_QUANTITY_IN = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 0m),
                    BASE_QUANTITY_OUT = table.Column<decimal>(type: "NUMBER(14,4)", nullable: false, defaultValue: 0m),
                    UNIT_PRICE = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    UNIT_COST = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    DISCOUNT_PERCENT = table.Column<decimal>(type: "NUMBER(7,4)", nullable: false, defaultValue: 0m),
                    DISCOUNT_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    TAX_RATE = table.Column<decimal>(type: "NUMBER(7,4)", nullable: false, defaultValue: 0m),
                    TAX_AMOUNT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    LINE_TOTAL = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    FROM_BIN_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    TO_BIN_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    LOT_NUMBER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    SERIAL_NUMBER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    EXPIRY_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    GL_ACCOUNT_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    BASE_LINE_NO = table.Column<int>(type: "NUMBER(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRX_DOCUMENT_LINE", x => new { x.BRANCH_ID, x.DOC_YEAR, x.DOC_TYPE, x.DOC_ID, x.LINE_NO });
                    table.ForeignKey(
                        name: "FK_TRX_DOCUMENT_LINE_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRX_DOCUMENT_LINE_TRX_DOCUMENT_HEADER_BRANCH_ID_DOC_YEAR_DOC_TYPE_DOC_ID",
                        columns: x => new { x.BRANCH_ID, x.DOC_YEAR, x.DOC_TYPE, x.DOC_ID },
                        principalTable: "TRX_DOCUMENT_HEADER",
                        principalColumns: new[] { "BRANCH_ID", "DOC_YEAR", "DOC_TYPE", "ID" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TRX_DOCUMENT_LINE_TRX_TRANSACTION_TYPE_TRX_TYPE",
                        column: x => x.TRX_TYPE,
                        principalTable: "TRX_TRANSACTION_TYPE",
                        principalColumn: "TRX_CODE",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INV_BIN_ZONE_ID",
                table: "INV_BIN",
                column: "ZONE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_BOM_HEADER_BOM_CODE",
                table: "INV_BOM_HEADER",
                column: "BOM_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_BOM_HEADER_PARENT_ITEM_ID",
                table: "INV_BOM_HEADER",
                column: "PARENT_ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_BOM_LINE_BOM_ID",
                table: "INV_BOM_LINE",
                column: "BOM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_BOM_LINE_COMPONENT_ITEM_ID",
                table: "INV_BOM_LINE",
                column: "COMPONENT_ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_BOM_LINE_SUBSTITUTE_ITEM_ID",
                table: "INV_BOM_LINE",
                column: "SUBSTITUTE_ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_COST_LAYER_ITEM_ID",
                table: "INV_COST_LAYER",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_COST_LAYER_WAREHOUSE_ID",
                table: "INV_COST_LAYER",
                column: "WAREHOUSE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_COUNT_LINE_BIN_ID",
                table: "INV_COUNT_LINE",
                column: "BIN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_COUNT_LINE_COUNT_SESSION_ID",
                table: "INV_COUNT_LINE",
                column: "COUNT_SESSION_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_COUNT_LINE_ITEM_ID",
                table: "INV_COUNT_LINE",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_COUNT_SESSION_WAREHOUSE_ID",
                table: "INV_COUNT_SESSION",
                column: "WAREHOUSE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_ITEM_CODE",
                table: "INV_ITEM",
                column: "ITEM_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_MAIN_GROUP_ID_SUB_GROUP_ID",
                table: "INV_ITEM",
                columns: new[] { "MAIN_GROUP_ID", "SUB_GROUP_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_SUB_GROUP_ID",
                table: "INV_ITEM",
                column: "SUB_GROUP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_BARCODE_ITEM_ID",
                table: "INV_ITEM_BARCODE",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_GROUP_GROUP_CODE",
                table: "INV_ITEM_GROUP",
                column: "GROUP_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_GROUP_PARENT_GROUP_ID",
                table: "INV_ITEM_GROUP",
                column: "PARENT_GROUP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ITEM_UOM_CONVERSION_ITEM_ID",
                table: "INV_ITEM_UOM_CONVERSION",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_LOT_MASTER_ITEM_ID",
                table: "INV_LOT_MASTER",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_OPENING_BATCH_BATCH_NO",
                table: "INV_OPENING_BATCH",
                column: "BATCH_NO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_OPENING_LINE_BATCH_ID",
                table: "INV_OPENING_LINE",
                column: "BATCH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_OPENING_LINE_ITEM_ID",
                table: "INV_OPENING_LINE",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_OPENING_LINE_WAREHOUSE_ID",
                table: "INV_OPENING_LINE",
                column: "WAREHOUSE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_RESERVATION_ITEM_ID",
                table: "INV_RESERVATION",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_RESERVATION_WAREHOUSE_ID",
                table: "INV_RESERVATION",
                column: "WAREHOUSE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_SERIAL_MASTER_CURRENT_BIN_ID",
                table: "INV_SERIAL_MASTER",
                column: "CURRENT_BIN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_SERIAL_MASTER_CURRENT_WAREHOUSE_ID",
                table: "INV_SERIAL_MASTER",
                column: "CURRENT_WAREHOUSE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_SERIAL_MASTER_ITEM_ID",
                table: "INV_SERIAL_MASTER",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_SERIAL_MASTER_LOT_ID",
                table: "INV_SERIAL_MASTER",
                column: "LOT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_STOCK_BALANCE_ITEM_ID",
                table: "INV_STOCK_BALANCE",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_STOCK_BALANCE_WAREHOUSE_ID",
                table: "INV_STOCK_BALANCE",
                column: "WAREHOUSE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_STOCK_LEDGER_ENTRY_ITEM_ID",
                table: "INV_STOCK_LEDGER_ENTRY",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_STOCK_LEDGER_ENTRY_WAREHOUSE_ID",
                table: "INV_STOCK_LEDGER_ENTRY",
                column: "WAREHOUSE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ZONE_WAREHOUSE_ID",
                table: "INV_ZONE",
                column: "WAREHOUSE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_TRX_DOC_TYPE_TYPE_KEY",
                table: "TRX_DOC_TYPE",
                column: "TYPE_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRX_DOCUMENT_HEADER_BRANCH_ID_DOC_YEAR_DOC_TYPE_DOC_NO",
                table: "TRX_DOCUMENT_HEADER",
                columns: new[] { "BRANCH_ID", "DOC_YEAR", "DOC_TYPE", "DOC_NO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRX_DOCUMENT_HEADER_DOC_TYPE",
                table: "TRX_DOCUMENT_HEADER",
                column: "DOC_TYPE");

            migrationBuilder.CreateIndex(
                name: "IX_TRX_DOCUMENT_HEADER_FROM_WAREHOUSE_ID",
                table: "TRX_DOCUMENT_HEADER",
                column: "FROM_WAREHOUSE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_TRX_DOCUMENT_HEADER_TO_WAREHOUSE_ID",
                table: "TRX_DOCUMENT_HEADER",
                column: "TO_WAREHOUSE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_TRX_DOCUMENT_HEADER_TRX_TYPE",
                table: "TRX_DOCUMENT_HEADER",
                column: "TRX_TYPE");

            migrationBuilder.CreateIndex(
                name: "IX_TRX_DOCUMENT_LINE_ITEM_ID",
                table: "TRX_DOCUMENT_LINE",
                column: "ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_TRX_DOCUMENT_LINE_TRX_TYPE",
                table: "TRX_DOCUMENT_LINE",
                column: "TRX_TYPE");

            migrationBuilder.CreateIndex(
                name: "IX_TRX_TRANSACTION_TYPE_DOC_TYPE_CODE",
                table: "TRX_TRANSACTION_TYPE",
                column: "DOC_TYPE_CODE");

            migrationBuilder.CreateIndex(
                name: "IX_TRX_TRANSACTION_TYPE_TRX_KEY",
                table: "TRX_TRANSACTION_TYPE",
                column: "TRX_KEY",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INV_BOM_LINE");

            migrationBuilder.DropTable(
                name: "INV_COST_LAYER");

            migrationBuilder.DropTable(
                name: "INV_COUNT_LINE");

            migrationBuilder.DropTable(
                name: "INV_ITEM_BARCODE");

            migrationBuilder.DropTable(
                name: "INV_ITEM_UOM_CONVERSION");

            migrationBuilder.DropTable(
                name: "INV_OPENING_LINE");

            migrationBuilder.DropTable(
                name: "INV_RESERVATION");

            migrationBuilder.DropTable(
                name: "INV_SERIAL_MASTER");

            migrationBuilder.DropTable(
                name: "INV_STOCK_BALANCE");

            migrationBuilder.DropTable(
                name: "INV_STOCK_LEDGER_ENTRY");

            migrationBuilder.DropTable(
                name: "TRX_DOCUMENT_LINE");

            migrationBuilder.DropTable(
                name: "INV_BOM_HEADER");

            migrationBuilder.DropTable(
                name: "INV_COUNT_SESSION");

            migrationBuilder.DropTable(
                name: "INV_OPENING_BATCH");

            migrationBuilder.DropTable(
                name: "INV_BIN");

            migrationBuilder.DropTable(
                name: "INV_LOT_MASTER");

            migrationBuilder.DropTable(
                name: "TRX_DOCUMENT_HEADER");

            migrationBuilder.DropTable(
                name: "INV_ZONE");

            migrationBuilder.DropTable(
                name: "INV_ITEM");

            migrationBuilder.DropTable(
                name: "TRX_TRANSACTION_TYPE");

            migrationBuilder.DropTable(
                name: "INV_WAREHOUSE");

            migrationBuilder.DropTable(
                name: "INV_ITEM_GROUP");

            migrationBuilder.DropTable(
                name: "TRX_DOC_TYPE");

            migrationBuilder.RenameTable(
                name: "SYS_AUDIT_LOG_ARCHIVE",
                schema: "THINKON_AUDIT",
                newName: "SYS_AUDIT_LOG_ARCHIVE");

            migrationBuilder.RenameTable(
                name: "SYS_AUDIT_LOG",
                schema: "THINKON_AUDIT",
                newName: "SYS_AUDIT_LOG");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "SYS_AUDIT_LOG_ARCHIVE",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "SYS_AUDIT_LOG",
                newName: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_SYS_AUDIT_LOG_BUSINESS_MODULE",
                table: "SYS_AUDIT_LOG",
                column: "BUSINESS_MODULE");

            migrationBuilder.AddForeignKey(
                name: "FK_SYS_AUDIT_LOG_SYSTEM",
                table: "SYS_AUDIT_LOG",
                column: "BUSINESS_MODULE",
                principalTable: "SYS_SYSTEM",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
