using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPosModifiersModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TYPE_NAME_LOCAL",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_TYPE",
                newName: "TYPE_NAME_AR");

            migrationBuilder.RenameColumn(
                name: "DESCRIPTION_LOCAL",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_TYPE",
                newName: "DESCRIPTION_AR");

            migrationBuilder.RenameColumn(
                name: "STATUS_NAME_LOCAL",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_STATUS",
                newName: "STATUS_NAME_AR");

            migrationBuilder.RenameColumn(
                name: "PRIORITY_NAME_LOCAL",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_PRIORITY",
                newName: "PRIORITY_NAME_AR");

            migrationBuilder.RenameColumn(
                name: "DESCRIPTION_LOCAL",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_CONFIG",
                newName: "DESCRIPTION_AR");

            migrationBuilder.RenameColumn(
                name: "DESCRIPTION_LOCAL",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_CATEGORY",
                newName: "DESCRIPTION_AR");

            migrationBuilder.RenameColumn(
                name: "CATEGORY_NAME_LOCAL",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_CATEGORY",
                newName: "CATEGORY_NAME_AR");

            migrationBuilder.CreateTable(
                name: "PAYMENT_METHOD",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    NAME_EN = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    METHOD_TYPE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    GL_ACCOUNT_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    BANK_ACCOUNT_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CASH_REGISTER_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    COMMISSION_PERCENT = table.Column<decimal>(type: "DECIMAL(8,4)", precision: 8, scale: 4, nullable: false, defaultValue: 0m),
                    COMMISSION_FIXED_AMOUNT = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    COMMISSION_GL_ACCOUNT_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    REQUIRES_REFERENCE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    REQUIRES_DUE_DATE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    AUTO_POST_GL = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    SHOW_IN_POS = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    SHOW_IN_INVOICES = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    SHOW_IN_VOUCHERS = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    DISPLAY_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 1),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYMENT_METHOD", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PAYMENT_METHOD_BANK_ACCOUNT_BANK_ACCOUNT_ID",
                        column: x => x.BANK_ACCOUNT_ID,
                        principalTable: "BANK_ACCOUNT",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYMENT_METHOD_CASH_REGISTER_CASH_REGISTER_ID",
                        column: x => x.CASH_REGISTER_ID,
                        principalTable: "CASH_REGISTER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYMENT_METHOD_GL_ACCOUNT_COMMISSION_GL_ACCOUNT_CODE",
                        column: x => x.COMMISSION_GL_ACCOUNT_CODE,
                        principalTable: "GL_ACCOUNT",
                        principalColumn: "ACCOUNT_CODE",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYMENT_METHOD_GL_ACCOUNT_GL_ACCOUNT_CODE",
                        column: x => x.GL_ACCOUNT_CODE,
                        principalTable: "GL_ACCOUNT",
                        principalColumn: "ACCOUNT_CODE",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYMENT_METHOD_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_MODIFIER_GROUP",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BRANCH_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    GROUP_CODE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    GROUP_NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    GROUP_NAME_EN = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    IS_REQUIRED = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    SELECTION_TYPE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MIN_SELECTIONS = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    MAX_SELECTIONS = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    SORT_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    CREATION_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATION_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UPDATE_USER = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    UPDATE_DATE = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_MODIFIER_GROUP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_MODIFIER_GROUP_SYS_BRANCH_BRANCH_ID",
                        column: x => x.BRANCH_ID,
                        principalTable: "SYS_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POS_ITEM_MODIFIER_GROUP",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    MODIFIER_GROUP_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SORT_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_ITEM_MODIFIER_GROUP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_ITEM_MODIFIER_GROUP_INV_ITEM_ITEM_ID",
                        column: x => x.ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_POS_ITEM_MODIFIER_GROUP_POS_MODIFIER_GROUP_MODIFIER_GROUP_ID",
                        column: x => x.MODIFIER_GROUP_ID,
                        principalTable: "POS_MODIFIER_GROUP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "POS_MODIFIER_OPTION",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    MODIFIER_GROUP_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    OPTION_NAME_LOCAL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    OPTION_NAME_EN = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    PRICE_ADJUSTMENT = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false, defaultValue: 0m),
                    RELATED_ITEM_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    IS_DEFAULT = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: false),
                    SORT_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POS_MODIFIER_OPTION", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_MODIFIER_OPTION_INV_ITEM_RELATED_ITEM_ID",
                        column: x => x.RELATED_ITEM_ID,
                        principalTable: "INV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_POS_MODIFIER_OPTION_POS_MODIFIER_GROUP_MODIFIER_GROUP_ID",
                        column: x => x.MODIFIER_GROUP_ID,
                        principalTable: "POS_MODIFIER_GROUP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_METHOD_BANK_ACCOUNT_ID",
                table: "PAYMENT_METHOD",
                column: "BANK_ACCOUNT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_METHOD_CASH_REGISTER_ID",
                table: "PAYMENT_METHOD",
                column: "CASH_REGISTER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_METHOD_COMMISSION_GL_ACCOUNT_CODE",
                table: "PAYMENT_METHOD",
                column: "COMMISSION_GL_ACCOUNT_CODE");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_METHOD_GL_ACCOUNT_CODE",
                table: "PAYMENT_METHOD",
                column: "GL_ACCOUNT_CODE");

            migrationBuilder.CreateIndex(
                name: "UX_PM_BRANCH_CODE",
                table: "PAYMENT_METHOD",
                columns: new[] { "BRANCH_ID", "CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_ITEM_MODIFIER_GROUP_ITEM_ID_MODIFIER_GROUP_ID",
                table: "POS_ITEM_MODIFIER_GROUP",
                columns: new[] { "ITEM_ID", "MODIFIER_GROUP_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_ITEM_MODIFIER_GROUP_MODIFIER_GROUP_ID",
                table: "POS_ITEM_MODIFIER_GROUP",
                column: "MODIFIER_GROUP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_MODIFIER_GROUP_BRANCH_ID_GROUP_CODE",
                table: "POS_MODIFIER_GROUP",
                columns: new[] { "BRANCH_ID", "GROUP_CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_MODIFIER_OPTION_MODIFIER_GROUP_ID",
                table: "POS_MODIFIER_OPTION",
                column: "MODIFIER_GROUP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_MODIFIER_OPTION_RELATED_ITEM_ID",
                table: "POS_MODIFIER_OPTION",
                column: "RELATED_ITEM_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PAYMENT_METHOD");

            migrationBuilder.DropTable(
                name: "POS_ITEM_MODIFIER_GROUP");

            migrationBuilder.DropTable(
                name: "POS_MODIFIER_OPTION");

            migrationBuilder.DropTable(
                name: "POS_MODIFIER_GROUP");

            migrationBuilder.RenameColumn(
                name: "TYPE_NAME_AR",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_TYPE",
                newName: "TYPE_NAME_LOCAL");

            migrationBuilder.RenameColumn(
                name: "DESCRIPTION_AR",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_TYPE",
                newName: "DESCRIPTION_LOCAL");

            migrationBuilder.RenameColumn(
                name: "STATUS_NAME_AR",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_STATUS",
                newName: "STATUS_NAME_LOCAL");

            migrationBuilder.RenameColumn(
                name: "PRIORITY_NAME_AR",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_PRIORITY",
                newName: "PRIORITY_NAME_LOCAL");

            migrationBuilder.RenameColumn(
                name: "DESCRIPTION_AR",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_CONFIG",
                newName: "DESCRIPTION_LOCAL");

            migrationBuilder.RenameColumn(
                name: "DESCRIPTION_AR",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_CATEGORY",
                newName: "DESCRIPTION_LOCAL");

            migrationBuilder.RenameColumn(
                name: "CATEGORY_NAME_AR",
                schema: "THINKON_SUPPORT",
                table: "SYS_TICKET_CATEGORY",
                newName: "CATEGORY_NAME_LOCAL");
        }
    }
}
