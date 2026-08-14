using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorGlAccountToCompositeCodeKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop obsolete ACCOUNT_CATEGORY table if exists
            migrationBuilder.Sql("BEGIN EXECUTE IMMEDIATE 'DROP TABLE ACCOUNT_CATEGORY CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN NULL; ELSE RAISE; END IF; END;");

            // 2. Drop old GL_ACCOUNT_BRANCH if exists
            migrationBuilder.Sql("BEGIN EXECUTE IMMEDIATE 'DROP TABLE GL_ACCOUNT_BRANCH CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN NULL; ELSE RAISE; END IF; END;");

            // 3. Drop old GL_ACCOUNT if exists
            migrationBuilder.Sql("BEGIN EXECUTE IMMEDIATE 'DROP TABLE GL_ACCOUNT CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN NULL; ELSE RAISE; END IF; END;");

            // 4. Create GL_ACCOUNT table with single PK (ACCOUNT_CODE)
            migrationBuilder.Sql(@"
                CREATE TABLE GL_ACCOUNT (
                    ACCOUNT_CODE NVARCHAR2(50) NOT NULL,
                    ACCOUNT_NAME_AR NVARCHAR2(200) NOT NULL,
                    ACCOUNT_NAME_EN NVARCHAR2(200) NOT NULL,
                    PARENT_ACCOUNT_CODE NVARCHAR2(50) NULL,
                    ACCOUNT_LEVEL NUMBER(2) NOT NULL,
                    ACCOUNT_TYPE NVARCHAR2(10) NOT NULL,
                    NORMAL_BALANCE NVARCHAR2(1) NOT NULL,
                    IS_CONTRA NUMBER(1) DEFAULT 0 NOT NULL,
                    IS_CONTROL_ACCOUNT NUMBER(1) DEFAULT 0 NOT NULL,
                    CONTROL_ACCOUNT_TYPE NVARCHAR2(20) NULL,
                    IS_BRANCH_SPECIFIC NUMBER(1) DEFAULT 0 NOT NULL,
                    IS_CLEARING NUMBER(1) DEFAULT 0 NOT NULL,
                    IS_ACTIVE NUMBER(1) DEFAULT 1 NOT NULL,
                    DESCRIPTION NVARCHAR2(1000) NULL,
                    NOTES NVARCHAR2(2000) NULL,
                    CONSTRAINT PK_GL_ACC PRIMARY KEY (ACCOUNT_CODE),
                    CONSTRAINT FK_GL_ACC_PARENT FOREIGN KEY (PARENT_ACCOUNT_CODE) REFERENCES GL_ACCOUNT (ACCOUNT_CODE),
                    CONSTRAINT CK_GL_ACCOUNT_LEVEL CHECK (ACCOUNT_LEVEL BETWEEN 1 AND 5),
                    CONSTRAINT CK_GL_ACCOUNT_TYPE CHECK (ACCOUNT_TYPE IN ('HEADER', 'DETAIL')),
                    CONSTRAINT CK_GL_ACCOUNT_BALANCE CHECK (NORMAL_BALANCE IN ('D', 'C')),
                    CONSTRAINT CK_GL_ACCOUNT_FLAGS CHECK (IS_CONTRA IN (0, 1) AND IS_CONTROL_ACCOUNT IN (0, 1) AND IS_BRANCH_SPECIFIC IN (0, 1) AND IS_CLEARING IN (0, 1) AND IS_ACTIVE IN (0, 1)),
                    CONSTRAINT CK_GL_ACCOUNT_CONTROL_TYPE CHECK ((IS_CONTROL_ACCOUNT = 0 AND CONTROL_ACCOUNT_TYPE IS NULL) OR (IS_CONTROL_ACCOUNT = 1 AND CONTROL_ACCOUNT_TYPE IN ('AR', 'AP', 'INVENTORY')))
                )
            ");

            // 5. Create GL_ACCOUNT_BRANCH table with PK (ACCOUNT_CODE, BRANCH_ID)
            migrationBuilder.Sql(@"
                CREATE TABLE GL_ACCOUNT_BRANCH (
                    ACCOUNT_CODE NVARCHAR2(50) NOT NULL,
                    BRANCH_ID NUMBER(19) NOT NULL,
                    IS_ACTIVE NUMBER(1) DEFAULT 1 NOT NULL,
                    CONSTRAINT PK_GL_ACC_BRANCH PRIMARY KEY (ACCOUNT_CODE, BRANCH_ID),
                    CONSTRAINT FK_GLAB_ACC FOREIGN KEY (ACCOUNT_CODE) REFERENCES GL_ACCOUNT (ACCOUNT_CODE),
                    CONSTRAINT CK_GL_ACC_BRANCH_ACTIVE CHECK (IS_ACTIVE IN (0, 1))
                )
            ");

            // 6. Create Indexes
            migrationBuilder.Sql("CREATE INDEX IX_GL_ACCOUNT_PARENT ON GL_ACCOUNT (PARENT_ACCOUNT_CODE)");
            migrationBuilder.Sql("CREATE INDEX IX_GL_ACCOUNT_CTRL ON GL_ACCOUNT (IS_CONTROL_ACCOUNT)");
            migrationBuilder.Sql("CREATE INDEX IX_GL_ACC_BRANCH_BRANCH ON GL_ACCOUNT_BRANCH (BRANCH_ID)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("BEGIN EXECUTE IMMEDIATE 'DROP TABLE GL_ACCOUNT_BRANCH CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN NULL; ELSE RAISE; END IF; END;");
            migrationBuilder.Sql("BEGIN EXECUTE IMMEDIATE 'DROP TABLE GL_ACCOUNT CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN NULL; ELSE RAISE; END IF; END;");
        }
    }
}

