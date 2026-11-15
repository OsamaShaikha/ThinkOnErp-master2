using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114225000_BootstrapLeavePolicyTable")]
public sealed class BootstrapLeavePolicyTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                owner_name VARCHAR2(128) := SYS_CONTEXT('USERENV','CURRENT_SCHEMA');
                table_count NUMBER;
                object_count NUMBER;
                column_count NUMBER;
                compatible_count NUMBER;
                invalid_rows NUMBER;
                index_count NUMBER;
                index_match NUMBER;

                PROCEDURE inspect_date_column(p_column VARCHAR2, p_nullable VARCHAR2) IS
                BEGIN
                    SELECT COUNT(*) INTO column_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_POLICY' AND COLUMN_NAME=p_column;
                    IF column_count=0 THEN RETURN; END IF;
                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_POLICY' AND COLUMN_NAME=p_column
                      AND DATA_TYPE='TIMESTAMP(7)' AND (p_nullable='*' OR NULLABLE=p_nullable)
                      AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO';
                    IF compatible_count<>1 THEN
                        RAISE_APPLICATION_ERROR(-20421, 'HR_LEAVE_POLICY.'||p_column||' has an incompatible definition.');
                    END IF;
                END;

                PROCEDURE inspect_leave_type_parent IS
                BEGIN
                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TABLES WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_TYPE';
                    IF compatible_count<>1 THEN
                        RAISE_APPLICATION_ERROR(-20422, 'HR_LEAVE_TYPE is required before creating HR_LEAVE_POLICY.');
                    END IF;
                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_TYPE' AND COLUMN_NAME='LEAVE_TYPE_CODE'
                      AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH>=50 AND CHAR_USED='C'
                      AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO';
                    IF compatible_count<>1 THEN
                        RAISE_APPLICATION_ERROR(-20423, 'HR_LEAVE_TYPE.LEAVE_TYPE_CODE does not match the expected NVARCHAR2(50) parent key.');
                    END IF;
                    SELECT COUNT(*) INTO compatible_count
                    FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER=owner_name AND c.TABLE_NAME='HR_LEAVE_TYPE'
                      AND c.CONSTRAINT_TYPE IN ('P','U') AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1
                      AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME AND cc.COLUMN_NAME='LEAVE_TYPE_CODE' AND cc.POSITION=1);
                    IF compatible_count<>1 THEN
                        RAISE_APPLICATION_ERROR(-20424, 'HR_LEAVE_TYPE.LEAVE_TYPE_CODE has no enabled, validated single-column key.');
                    END IF;
                END;

                PROCEDURE inspect_index IS
                BEGIN
                    SELECT COUNT(*) INTO index_count FROM ALL_INDEXES
                    WHERE OWNER=owner_name AND INDEX_NAME='IX_HR_LEAVE_POLICY_EFFECTIVE';
                    IF index_count=0 THEN
                        SELECT COUNT(*) INTO object_count FROM ALL_OBJECTS
                        WHERE OWNER=owner_name AND OBJECT_NAME='IX_HR_LEAVE_POLICY_EFFECTIVE';
                        IF object_count<>0 THEN
                            RAISE_APPLICATION_ERROR(-20425, 'IX_HR_LEAVE_POLICY_EFFECTIVE is reserved by another object.');
                        END IF;
                        RETURN;
                    END IF;
                    SELECT COUNT(*) INTO index_match FROM ALL_INDEXES i
                    WHERE i.OWNER=owner_name AND i.INDEX_NAME='IX_HR_LEAVE_POLICY_EFFECTIVE'
                      AND i.TABLE_NAME='HR_LEAVE_POLICY' AND i.UNIQUENESS='NONUNIQUE' AND i.STATUS='VALID'
                      AND (SELECT LISTAGG(c.COLUMN_NAME,',') WITHIN GROUP (ORDER BY c.COLUMN_POSITION)
                           FROM ALL_IND_COLUMNS c WHERE c.INDEX_OWNER=i.OWNER AND c.INDEX_NAME=i.INDEX_NAME)='LEAVE_TYPE_CODE,EFFECTIVE_FROM';
                    IF index_match<>1 THEN
                        RAISE_APPLICATION_ERROR(-20426, 'IX_HR_LEAVE_POLICY_EFFECTIVE exists with an incompatible signature.');
                    END IF;
                END;
            BEGIN
                SELECT COUNT(*) INTO table_count FROM ALL_TABLES
                WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_POLICY';
                IF table_count>1 THEN
                    RAISE_APPLICATION_ERROR(-20420, 'HR_LEAVE_POLICY metadata is ambiguous.');
                END IF;

                IF table_count=0 THEN
                    SELECT COUNT(*) INTO object_count FROM ALL_OBJECTS
                    WHERE OWNER=owner_name AND OBJECT_NAME='HR_LEAVE_POLICY';
                    IF object_count<>0 THEN
                        RAISE_APPLICATION_ERROR(-20427, 'HR_LEAVE_POLICY is reserved by a non-table object.');
                    END IF;
                    inspect_leave_type_parent();
                    FOR object_name IN (
                        SELECT 'PK_HR_LEAVE_POLICY' name FROM DUAL UNION ALL
                        SELECT 'FK_HR_LEAVE_POLICY_TYPE' FROM DUAL UNION ALL
                        SELECT 'IX_HR_LEAVE_POLICY_EFFECTIVE' FROM DUAL
                    ) LOOP
                        SELECT (SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER=owner_name AND CONSTRAINT_NAME=object_name.name)
                             + (SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER=owner_name AND INDEX_NAME=object_name.name)
                        INTO object_count FROM DUAL;
                        IF object_count<>0 THEN
                            RAISE_APPLICATION_ERROR(-20428, object_name.name||' is already reserved by another object.');
                        END IF;
                    END LOOP;
                ELSE
                    SELECT COUNT(*) INTO compatible_count FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_POLICY' AND COLUMN_NAME='CREATION_DATE'
                      AND DATA_TYPE='TIMESTAMP(7)' AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO';
                    IF compatible_count<>1 THEN
                        RAISE_APPLICATION_ERROR(-20429, 'HR_LEAVE_POLICY.CREATION_DATE is missing or incompatible.');
                    END IF;
                    inspect_date_column('EFFECTIVE_FROM','*');
                    inspect_date_column('EFFECTIVE_TO','Y');
                    inspect_index();

                    SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_POLICY' AND COLUMN_NAME='EFFECTIVE_FROM';
                    IF column_count=0 THEN
                        EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "HR_LEAVE_POLICY" WHERE "CREATION_DATE" IS NULL' INTO invalid_rows;
                    ELSE
                        EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "HR_LEAVE_POLICY" WHERE "EFFECTIVE_FROM" IS NULL AND "CREATION_DATE" IS NULL' INTO invalid_rows;
                    END IF;
                    IF invalid_rows<>0 THEN
                        RAISE_APPLICATION_ERROR(-20430, 'HR_LEAVE_POLICY has rows without a creation date for effective-date backfill.');
                    END IF;
                END IF;

                IF table_count=0 THEN
                    EXECUTE IMMEDIATE q'[
                        CREATE TABLE "HR_LEAVE_POLICY" (
                            "ID" NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY,
                            "POLICY_NAME" NVARCHAR2(200) NOT NULL,
                            "LEAVE_TYPE_CODE" NVARCHAR2(50) NOT NULL,
                            "EFFECTIVE_FROM" TIMESTAMP(7) NOT NULL,
                            "EFFECTIVE_TO" TIMESTAMP(7) NULL,
                            "ACCRUAL_METHOD" NVARCHAR2(50) NOT NULL,
                            "ACCRUAL_RATE" NUMBER(8,4) NOT NULL,
                            "ANNUAL_ENTITLEMENT_DAYS" NUMBER(8,4) NULL,
                            "CYCLE_BASIS" NVARCHAR2(40) NOT NULL,
                            "PRORATE_FIRST_CYCLE" NUMBER(10) NOT NULL,
                            "APPLICABLE_TO" NVARCHAR2(50) NOT NULL,
                            "MIN_SERVICE_MONTHS" NUMBER(10) NOT NULL,
                            "TIER1_YEARS_THRESHOLD" NUMBER(10) NOT NULL,
                            "TIER1_DAYS" NUMBER(6,2) NOT NULL,
                            "TIER2_DAYS" NUMBER(6,2) NOT NULL,
                            "IS_ACTIVE" NUMBER(10) NOT NULL,
                            "CREATION_USER" NVARCHAR2(100) NOT NULL,
                            "CREATION_DATE" TIMESTAMP(7) NOT NULL,
                            "UPDATE_USER" NVARCHAR2(100) NULL,
                            "UPDATE_DATE" TIMESTAMP(7) NULL,
                            CONSTRAINT "PK_HR_LEAVE_POLICY" PRIMARY KEY ("ID"),
                            CONSTRAINT "FK_HR_LEAVE_POLICY_TYPE" FOREIGN KEY ("LEAVE_TYPE_CODE") REFERENCES "HR_LEAVE_TYPE" ("LEAVE_TYPE_CODE")
                        )
                    ]';
                ELSE
                    SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_POLICY' AND COLUMN_NAME='EFFECTIVE_FROM';
                    IF column_count=0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_POLICY" ADD ("EFFECTIVE_FROM" TIMESTAMP(7) NULL)';
                    END IF;
                    SELECT COUNT(*) INTO column_count FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_POLICY' AND COLUMN_NAME='EFFECTIVE_TO';
                    IF column_count=0 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_POLICY" ADD ("EFFECTIVE_TO" TIMESTAMP(7) NULL)';
                    END IF;
                    EXECUTE IMMEDIATE 'UPDATE "HR_LEAVE_POLICY" SET "EFFECTIVE_FROM"=TRUNC("CREATION_DATE") WHERE "EFFECTIVE_FROM" IS NULL';
                    SELECT COUNT(*) INTO compatible_count FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_POLICY' AND COLUMN_NAME='EFFECTIVE_FROM' AND NULLABLE='Y';
                    IF compatible_count=1 THEN
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_LEAVE_POLICY" MODIFY ("EFFECTIVE_FROM" NOT NULL)';
                    END IF;
                END IF;

                SELECT COUNT(*) INTO index_count FROM ALL_INDEXES
                WHERE OWNER=owner_name AND INDEX_NAME='IX_HR_LEAVE_POLICY_EFFECTIVE';
                IF index_count=0 THEN
                    EXECUTE IMMEDIATE 'CREATE INDEX "IX_HR_LEAVE_POLICY_EFFECTIVE" ON "HR_LEAVE_POLICY" ("LEAVE_TYPE_CODE","EFFECTIVE_FROM")';
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve leave policy definitions and the table during rollback.
    }
}
