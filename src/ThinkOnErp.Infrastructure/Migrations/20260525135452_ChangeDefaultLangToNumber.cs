using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDefaultLangToNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                BEGIN
                    EXECUTE IMMEDIATE 'ALTER TABLE ""SYS_BRANCH"" ADD ""DEFAULT_LANG_NEW"" NUMBER(10)';
                    EXECUTE IMMEDIATE 'UPDATE ""SYS_BRANCH"" SET ""DEFAULT_LANG_NEW"" = 1';
                    EXECUTE IMMEDIATE 'ALTER TABLE ""SYS_BRANCH"" DROP COLUMN ""DEFAULT_LANG""';
                    EXECUTE IMMEDIATE 'ALTER TABLE ""SYS_BRANCH"" RENAME COLUMN ""DEFAULT_LANG_NEW"" TO ""DEFAULT_LANG""';
                EXCEPTION WHEN OTHERS THEN
                    IF SQLCODE = -1430 THEN NULL;
                    ELSE RAISE;
                    END IF;
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                BEGIN
                    EXECUTE IMMEDIATE 'ALTER TABLE ""SYS_BRANCH"" ADD ""DEFAULT_LANG_NEW"" NVARCHAR2(10)';
                    EXECUTE IMMEDIATE 'UPDATE ""SYS_BRANCH"" SET ""DEFAULT_LANG_NEW"" = TO_CHAR(""DEFAULT_LANG"")';
                    EXECUTE IMMEDIATE 'ALTER TABLE ""SYS_BRANCH"" DROP COLUMN ""DEFAULT_LANG""';
                    EXECUTE IMMEDIATE 'ALTER TABLE ""SYS_BRANCH"" RENAME COLUMN ""DEFAULT_LANG_NEW"" TO ""DEFAULT_LANG""';
                EXCEPTION WHEN OTHERS THEN
                    IF SQLCODE = -1430 THEN NULL;
                    ELSE RAISE;
                    END IF;
                END;
            ");
        }
    }
}
