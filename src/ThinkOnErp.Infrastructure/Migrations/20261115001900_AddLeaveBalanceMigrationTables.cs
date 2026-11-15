using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115001900_AddLeaveBalanceMigrationTables")]
public sealed class AddLeaveBalanceMigrationTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                owner_name VARCHAR2(128) := SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA');
                header_count NUMBER;
                line_count NUMBER;
                valid_columns NUMBER;
                column_total NUMBER;
                identity_count NUMBER;
                constraint_count NUMBER;
                signature_count NUMBER;
                collision_count NUMBER;
                parent_key_count NUMBER;
                v_child_column VARCHAR2(128);
                v_parent_table VARCHAR2(128);
                v_constraint_name VARCHAR2(128);
            BEGIN
                FOR parent IN (
                    SELECT 'HR_LEAVE_BALANCE' AS TABLE_NAME FROM DUAL
                    UNION ALL SELECT 'HR_LEAVE_MOVEMENT' FROM DUAL
                ) LOOP
                    SELECT COUNT(*) INTO parent_key_count
                    FROM ALL_TAB_COLS c
                    WHERE c.OWNER=owner_name AND c.TABLE_NAME=parent.TABLE_NAME AND c.COLUMN_NAME='ID'
                      AND c.DATA_TYPE='NUMBER' AND c.DATA_PRECISION=19 AND c.DATA_SCALE=0
                      AND c.NULLABLE='N' AND c.VIRTUAL_COLUMN='NO'
                      AND EXISTS (SELECT 1 FROM ALL_CONSTRAINTS p
                                  WHERE p.OWNER=c.OWNER AND p.TABLE_NAME=c.TABLE_NAME
                                    AND p.CONSTRAINT_TYPE IN ('P','U') AND p.STATUS='ENABLED' AND p.VALIDATED='VALIDATED'
                                    AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS pc WHERE pc.OWNER=p.OWNER AND pc.TABLE_NAME=p.TABLE_NAME AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME)=1
                                    AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS pc WHERE pc.OWNER=p.OWNER AND pc.TABLE_NAME=p.TABLE_NAME AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME AND pc.COLUMN_NAME='ID' AND pc.POSITION=1));
                    IF parent_key_count<>1 THEN
                        RAISE_APPLICATION_ERROR(-20280, parent.TABLE_NAME || '.ID must be NUMBER(19,0) NOT NULL with an enabled validated single-column key before leave migration DDL.');
                    END IF;
                END LOOP;

                SELECT COUNT(*) INTO header_count FROM ALL_TABLES WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION';
                SELECT COUNT(*) INTO line_count FROM ALL_TABLES WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION_LINE';
                IF line_count=1 AND header_count=0 THEN
                    RAISE_APPLICATION_ERROR(-20281, 'HR_LEAVE_BALANCE_MIGRATION_LINE exists without its parent; no DDL was applied.');
                END IF;

                IF header_count=0 OR line_count=0 THEN
                    SELECT COUNT(*) INTO collision_count FROM ALL_CONSTRAINTS
                    WHERE OWNER=owner_name AND ((header_count=0 AND CONSTRAINT_NAME IN (
                      'PK_HR_BAL_MIGRATION','UX_HR_BAL_MIGRATION_KEY','CK_HR_BAL_MIGRATION_STATUS','CK_HR_BAL_MIGRATION_MONTHS','CK_HR_BAL_MIGRATION_DAYS'))
                      OR (line_count=0 AND CONSTRAINT_NAME IN (
                      'PK_HR_BAL_MIGRATION_LINE','UX_HR_BAL_MIGRATION_LINE','FK_HR_BAL_MIG_LINE_BATCH','FK_HR_BAL_MIG_LINE_SRC','FK_HR_BAL_MIG_LINE_TGT',
                      'FK_HR_BAL_MIG_LINE_SRCMOV','FK_HR_BAL_MIG_LINE_TGTMOV','CK_HR_BAL_MIG_LINE_DATES','CK_HR_BAL_MIG_LINE_SOURCE_NULLS')));
                    IF collision_count<>0 THEN
                        RAISE_APPLICATION_ERROR(-20282, 'A leave balance migration constraint name is already used by another table.');
                    END IF;
                    SELECT COUNT(*) INTO collision_count FROM ALL_INDEXES
                    WHERE OWNER=owner_name AND ((header_count=0 AND INDEX_NAME IN ('PK_HR_BAL_MIGRATION','UX_HR_BAL_MIGRATION_KEY'))
                      OR (line_count=0 AND INDEX_NAME IN ('PK_HR_BAL_MIGRATION_LINE','UX_HR_BAL_MIGRATION_LINE')));
                    IF collision_count<>0 THEN
                        RAISE_APPLICATION_ERROR(-20283, 'A leave balance migration index name is already used by another table.');
                    END IF;
                END IF;

                IF header_count=1 THEN
                    SELECT COUNT(*) INTO valid_columns
                    FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION'
                      AND ((COLUMN_NAME='ID' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND DATA_SCALE=0 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='MIGRATION_KEY' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=200 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='EFFECTIVE_DATE' AND DATA_TYPE='DATE' AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME IN ('SOURCE_YEAR_START_MONTH','SOURCE_YEAR_START_DAY','TARGET_YEAR_START_MONTH','TARGET_YEAR_START_DAY') AND DATA_TYPE='NUMBER' AND DATA_PRECISION=2 AND DATA_SCALE=0 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='PAYLOAD_HASH' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=64 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='STATUS' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=20 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='CREATION_USER' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=200 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='CREATION_DATE' AND DATA_TYPE='TIMESTAMP(7)' AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME IN ('POSTED_BY','REVERSED_BY') AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=200 AND NULLABLE='Y' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME IN ('POSTED_DATE','REVERSED_DATE') AND DATA_TYPE='TIMESTAMP(7)' AND NULLABLE='Y' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO'));
                    SELECT COUNT(*) INTO column_total FROM ALL_TAB_COLS WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION';
                    IF valid_columns<>15 OR column_total<>15 THEN
                        RAISE_APPLICATION_ERROR(-20284, 'Existing HR_LEAVE_BALANCE_MIGRATION columns do not match EF.');
                    END IF;
                    SELECT COUNT(*) INTO identity_count FROM ALL_TAB_IDENTITY_COLS WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION' AND COLUMN_NAME='ID';
                    IF identity_count<>1 THEN RAISE_APPLICATION_ERROR(-20285, 'HR_LEAVE_BALANCE_MIGRATION.ID must be an identity column.'); END IF;
                    SELECT COUNT(*) INTO constraint_count FROM ALL_CONSTRAINTS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION' AND STATUS='ENABLED' AND VALIDATED='VALIDATED'
                      AND ((CONSTRAINT_NAME='PK_HR_BAL_MIGRATION' AND CONSTRAINT_TYPE='P')
                        OR (CONSTRAINT_NAME='UX_HR_BAL_MIGRATION_KEY' AND CONSTRAINT_TYPE='U')
                        OR (CONSTRAINT_NAME IN ('CK_HR_BAL_MIGRATION_STATUS','CK_HR_BAL_MIGRATION_MONTHS','CK_HR_BAL_MIGRATION_DAYS') AND CONSTRAINT_TYPE='C'));
                    IF constraint_count<>5 THEN RAISE_APPLICATION_ERROR(-20286, 'HR_LEAVE_BALANCE_MIGRATION constraints are missing, disabled or unvalidated.'); END IF;
                    SELECT COUNT(*) INTO signature_count FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER=owner_name AND c.TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION'
                      AND ((c.CONSTRAINT_NAME='PK_HR_BAL_MIGRATION' AND c.CONSTRAINT_TYPE='P' AND (SELECT LISTAGG(cc.COLUMN_NAME,',') WITHIN GROUP (ORDER BY cc.POSITION) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='ID')
                        OR (c.CONSTRAINT_NAME='UX_HR_BAL_MIGRATION_KEY' AND c.CONSTRAINT_TYPE='U' AND (SELECT LISTAGG(cc.COLUMN_NAME,',') WITHIN GROUP (ORDER BY cc.POSITION) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='MIGRATION_KEY'));
                    IF signature_count<>2 THEN RAISE_APPLICATION_ERROR(-20287, 'HR_LEAVE_BALANCE_MIGRATION primary/unique key signatures are incompatible.'); END IF;
                    FOR expected_check IN (
                        SELECT 'CK_HR_BAL_MIGRATION_STATUS' AS CONSTRAINT_NAME, 'STATUS IN (''DRAFT'',''POSTED'',''REVERSED'')' AS CONDITION_TEXT FROM DUAL
                        UNION ALL SELECT 'CK_HR_BAL_MIGRATION_MONTHS', 'SOURCE_YEAR_START_MONTH BETWEEN 1 AND 12 AND TARGET_YEAR_START_MONTH BETWEEN 1 AND 12' FROM DUAL
                        UNION ALL SELECT 'CK_HR_BAL_MIGRATION_DAYS', 'SOURCE_YEAR_START_DAY BETWEEN 1 AND 31 AND TARGET_YEAR_START_DAY BETWEEN 1 AND 31' FROM DUAL
                    ) LOOP
                        SELECT COUNT(*) INTO signature_count FROM ALL_CONSTRAINTS c
                        WHERE c.OWNER=owner_name AND c.TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION'
                          AND c.CONSTRAINT_NAME=expected_check.CONSTRAINT_NAME AND c.CONSTRAINT_TYPE='C'
                          AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
                          AND REPLACE(REPLACE(REPLACE(REGEXP_REPLACE(UPPER(c.SEARCH_CONDITION_VC),'[[:space:]]',''), '"',''), '(',''), ')','')
                              = REPLACE(REPLACE(REPLACE(REGEXP_REPLACE(UPPER(expected_check.CONDITION_TEXT),'[[:space:]]',''), '"',''), '(',''), ')','');
                        IF signature_count<>1 THEN RAISE_APPLICATION_ERROR(-20293, expected_check.CONSTRAINT_NAME || ' has an incompatible CHECK definition.'); END IF;
                    END LOOP;
                END IF;

                IF line_count=1 THEN
                    SELECT COUNT(*) INTO valid_columns
                    FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION_LINE'
                      AND ((COLUMN_NAME='ID' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND DATA_SCALE=0 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME IN ('MIGRATION_ID','SOURCE_BALANCE_ID') AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND DATA_SCALE=0 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME IN ('TARGET_BALANCE_ID','SOURCE_MOVEMENT_ID','TARGET_MOVEMENT_ID') AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND DATA_SCALE=0 AND NULLABLE='Y' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME IN ('EMPLOYEE_CODE','LEAVE_TYPE_CODE') AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH=100 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME IN ('SOURCE_CYCLE_START_DATE','SOURCE_CYCLE_END_DATE','TARGET_CYCLE_START_DATE','TARGET_CYCLE_END_DATE') AND DATA_TYPE='DATE' AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='SOURCE_YEAR_NO' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=4 AND DATA_SCALE=0 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME IN ('SOURCE_START_WAS_NULL','SOURCE_END_WAS_NULL') AND DATA_TYPE='NUMBER' AND DATA_PRECISION=1 AND DATA_SCALE=0 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME IN ('SOURCE_ACCRUED_DAYS','SOURCE_USED_DAYS','SOURCE_CARRIED_DAYS','ALLOCATED_ACCRUED_DAYS','ALLOCATED_USED_DAYS','ALLOCATED_CARRIED_DAYS','TARGET_ACCRUED_BEFORE','TARGET_USED_BEFORE','TARGET_CARRIED_BEFORE') AND DATA_TYPE='NUMBER' AND DATA_PRECISION=8 AND DATA_SCALE=4 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO'));
                    SELECT COUNT(*) INTO column_total FROM ALL_TAB_COLS WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION_LINE';
                    IF valid_columns<>24 OR column_total<>24 THEN RAISE_APPLICATION_ERROR(-20288, 'Existing HR_LEAVE_BALANCE_MIGRATION_LINE columns do not match EF.'); END IF;
                    SELECT COUNT(*) INTO identity_count FROM ALL_TAB_IDENTITY_COLS WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION_LINE' AND COLUMN_NAME='ID';
                    IF identity_count<>1 THEN RAISE_APPLICATION_ERROR(-20289, 'HR_LEAVE_BALANCE_MIGRATION_LINE.ID must be an identity column.'); END IF;
                    SELECT COUNT(*) INTO constraint_count FROM ALL_CONSTRAINTS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION_LINE' AND STATUS='ENABLED' AND VALIDATED='VALIDATED'
                      AND ((CONSTRAINT_NAME='PK_HR_BAL_MIGRATION_LINE' AND CONSTRAINT_TYPE='P')
                        OR (CONSTRAINT_NAME='UX_HR_BAL_MIGRATION_LINE' AND CONSTRAINT_TYPE='U')
                        OR (CONSTRAINT_NAME IN ('FK_HR_BAL_MIG_LINE_BATCH','FK_HR_BAL_MIG_LINE_SRC','FK_HR_BAL_MIG_LINE_TGT','FK_HR_BAL_MIG_LINE_SRCMOV','FK_HR_BAL_MIG_LINE_TGTMOV') AND CONSTRAINT_TYPE='R')
                        OR (CONSTRAINT_NAME IN ('CK_HR_BAL_MIG_LINE_DATES','CK_HR_BAL_MIG_LINE_SOURCE_NULLS') AND CONSTRAINT_TYPE='C'));
                    IF constraint_count<>9 THEN RAISE_APPLICATION_ERROR(-20290, 'HR_LEAVE_BALANCE_MIGRATION_LINE constraints are missing, disabled or unvalidated.'); END IF;
                    SELECT COUNT(*) INTO signature_count FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER=owner_name AND c.TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION_LINE'
                      AND ((c.CONSTRAINT_NAME='PK_HR_BAL_MIGRATION_LINE' AND c.CONSTRAINT_TYPE='P' AND (SELECT LISTAGG(cc.COLUMN_NAME,',') WITHIN GROUP (ORDER BY cc.POSITION) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='ID')
                        OR (c.CONSTRAINT_NAME='UX_HR_BAL_MIGRATION_LINE' AND c.CONSTRAINT_TYPE='U' AND (SELECT LISTAGG(cc.COLUMN_NAME,',') WITHIN GROUP (ORDER BY cc.POSITION) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='MIGRATION_ID,SOURCE_BALANCE_ID,TARGET_CYCLE_START_DATE'));
                    IF signature_count<>2 THEN RAISE_APPLICATION_ERROR(-20291, 'HR_LEAVE_BALANCE_MIGRATION_LINE primary/unique key signatures are incompatible.'); END IF;
                    FOR expected_check IN (
                        SELECT 'CK_HR_BAL_MIG_LINE_DATES' AS CONSTRAINT_NAME, 'SOURCE_CYCLE_END_DATE>=SOURCE_CYCLE_START_DATE AND TARGET_CYCLE_END_DATE>=TARGET_CYCLE_START_DATE' AS CONDITION_TEXT FROM DUAL
                        UNION ALL SELECT 'CK_HR_BAL_MIG_LINE_SOURCE_NULLS', 'SOURCE_START_WAS_NULL IN (0,1) AND SOURCE_END_WAS_NULL IN (0,1)' FROM DUAL
                    ) LOOP
                        SELECT COUNT(*) INTO signature_count FROM ALL_CONSTRAINTS c
                        WHERE c.OWNER=owner_name AND c.TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION_LINE'
                          AND c.CONSTRAINT_NAME=expected_check.CONSTRAINT_NAME AND c.CONSTRAINT_TYPE='C'
                          AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
                          AND REPLACE(REPLACE(REPLACE(REGEXP_REPLACE(UPPER(c.SEARCH_CONDITION_VC),'[[:space:]]',''), '"',''), '(',''), ')','')
                              = REPLACE(REPLACE(REPLACE(REGEXP_REPLACE(UPPER(expected_check.CONDITION_TEXT),'[[:space:]]',''), '"',''), '(',''), ')','');
                        IF signature_count<>1 THEN RAISE_APPLICATION_ERROR(-20294, expected_check.CONSTRAINT_NAME || ' has an incompatible CHECK definition.'); END IF;
                    END LOOP;

                    FOR expected_fk IN (
                        SELECT 'FK_HR_BAL_MIG_LINE_BATCH' AS CONSTRAINT_NAME, 'MIGRATION_ID' AS CHILD_COLUMN, 'HR_LEAVE_BALANCE_MIGRATION' AS PARENT_TABLE FROM DUAL
                        UNION ALL SELECT 'FK_HR_BAL_MIG_LINE_SRC','SOURCE_BALANCE_ID','HR_LEAVE_BALANCE' FROM DUAL
                        UNION ALL SELECT 'FK_HR_BAL_MIG_LINE_TGT','TARGET_BALANCE_ID','HR_LEAVE_BALANCE' FROM DUAL
                        UNION ALL SELECT 'FK_HR_BAL_MIG_LINE_SRCMOV','SOURCE_MOVEMENT_ID','HR_LEAVE_MOVEMENT' FROM DUAL
                        UNION ALL SELECT 'FK_HR_BAL_MIG_LINE_TGTMOV','TARGET_MOVEMENT_ID','HR_LEAVE_MOVEMENT' FROM DUAL
                    ) LOOP
                        v_constraint_name:=expected_fk.CONSTRAINT_NAME;
                        v_child_column:=expected_fk.CHILD_COLUMN;
                        v_parent_table:=expected_fk.PARENT_TABLE;
                        SELECT COUNT(*) INTO signature_count FROM ALL_CONSTRAINTS c
                        WHERE c.OWNER=owner_name AND c.TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION_LINE' AND c.CONSTRAINT_NAME=v_constraint_name
                          AND c.CONSTRAINT_TYPE='R' AND c.R_OWNER=owner_name AND c.DELETE_RULE='NO ACTION'
                          AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1
                          AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME AND cc.COLUMN_NAME=v_child_column AND cc.POSITION=1)
                          AND EXISTS (SELECT 1 FROM ALL_CONSTRAINTS p JOIN ALL_CONS_COLUMNS pc ON pc.OWNER=p.OWNER AND pc.TABLE_NAME=p.TABLE_NAME AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME
                                      WHERE p.OWNER=c.R_OWNER AND p.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME AND p.TABLE_NAME=v_parent_table
                                        AND p.CONSTRAINT_TYPE IN ('P','U') AND p.STATUS='ENABLED' AND p.VALIDATED='VALIDATED'
                                        AND pc.COLUMN_NAME='ID' AND pc.POSITION=1
                                        AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS px WHERE px.OWNER=p.OWNER AND px.TABLE_NAME=p.TABLE_NAME AND px.CONSTRAINT_NAME=p.CONSTRAINT_NAME)=1);
                        IF signature_count<>1 THEN RAISE_APPLICATION_ERROR(-20292, v_constraint_name || ' references an unexpected parent key.'); END IF;
                    END LOOP;
                END IF;
            END;
            """);

        migrationBuilder.Sql("""
            DECLARE
                owner_name VARCHAR2(128) := SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA');
                table_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO table_count FROM ALL_TABLES WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION';
                IF table_count=0 THEN
                    EXECUTE IMMEDIATE q'~
                        CREATE TABLE "HR_LEAVE_BALANCE_MIGRATION" (
                          "ID" NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY CONSTRAINT "PK_HR_BAL_MIGRATION" PRIMARY KEY,
                          "MIGRATION_KEY" NVARCHAR2(200) NOT NULL,
                          "EFFECTIVE_DATE" DATE NOT NULL,
                          "SOURCE_YEAR_START_MONTH" NUMBER(2) NOT NULL,
                          "SOURCE_YEAR_START_DAY" NUMBER(2) NOT NULL,
                          "TARGET_YEAR_START_MONTH" NUMBER(2) NOT NULL,
                          "TARGET_YEAR_START_DAY" NUMBER(2) NOT NULL,
                          "PAYLOAD_HASH" NVARCHAR2(64) NOT NULL,
                          "STATUS" NVARCHAR2(20) NOT NULL,
                          "CREATION_USER" NVARCHAR2(200) NOT NULL,
                          "CREATION_DATE" TIMESTAMP(7) NOT NULL,
                          "POSTED_BY" NVARCHAR2(200) NULL,
                          "POSTED_DATE" TIMESTAMP(7) NULL,
                          "REVERSED_BY" NVARCHAR2(200) NULL,
                          "REVERSED_DATE" TIMESTAMP(7) NULL,
                          CONSTRAINT "UX_HR_BAL_MIGRATION_KEY" UNIQUE ("MIGRATION_KEY"),
                          CONSTRAINT "CK_HR_BAL_MIGRATION_STATUS" CHECK ("STATUS" IN ('DRAFT','POSTED','REVERSED')),
                          CONSTRAINT "CK_HR_BAL_MIGRATION_MONTHS" CHECK ("SOURCE_YEAR_START_MONTH" BETWEEN 1 AND 12 AND "TARGET_YEAR_START_MONTH" BETWEEN 1 AND 12),
                          CONSTRAINT "CK_HR_BAL_MIGRATION_DAYS" CHECK ("SOURCE_YEAR_START_DAY" BETWEEN 1 AND 31 AND "TARGET_YEAR_START_DAY" BETWEEN 1 AND 31)
                        )
                    ~';
                END IF;

                SELECT COUNT(*) INTO table_count FROM ALL_TABLES WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_BALANCE_MIGRATION_LINE';
                IF table_count=0 THEN
                    EXECUTE IMMEDIATE q'~
                        CREATE TABLE "HR_LEAVE_BALANCE_MIGRATION_LINE" (
                          "ID" NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY CONSTRAINT "PK_HR_BAL_MIGRATION_LINE" PRIMARY KEY,
                          "MIGRATION_ID" NUMBER(19) NOT NULL,
                          "SOURCE_BALANCE_ID" NUMBER(19) NOT NULL,
                          "TARGET_BALANCE_ID" NUMBER(19) NULL,
                          "EMPLOYEE_CODE" NVARCHAR2(100) NOT NULL,
                          "LEAVE_TYPE_CODE" NVARCHAR2(100) NOT NULL,
                          "SOURCE_CYCLE_START_DATE" DATE NOT NULL,
                          "SOURCE_CYCLE_END_DATE" DATE NOT NULL,
                          "SOURCE_YEAR_NO" NUMBER(4) NOT NULL,
                          "SOURCE_START_WAS_NULL" NUMBER(1) NOT NULL,
                          "SOURCE_END_WAS_NULL" NUMBER(1) NOT NULL,
                          "SOURCE_ACCRUED_DAYS" NUMBER(8,4) NOT NULL,
                          "SOURCE_USED_DAYS" NUMBER(8,4) NOT NULL,
                          "SOURCE_CARRIED_DAYS" NUMBER(8,4) NOT NULL,
                          "TARGET_CYCLE_START_DATE" DATE NOT NULL,
                          "TARGET_CYCLE_END_DATE" DATE NOT NULL,
                          "ALLOCATED_ACCRUED_DAYS" NUMBER(8,4) NOT NULL,
                          "ALLOCATED_USED_DAYS" NUMBER(8,4) NOT NULL,
                          "ALLOCATED_CARRIED_DAYS" NUMBER(8,4) NOT NULL,
                          "TARGET_ACCRUED_BEFORE" NUMBER(8,4) NOT NULL,
                          "TARGET_USED_BEFORE" NUMBER(8,4) NOT NULL,
                          "TARGET_CARRIED_BEFORE" NUMBER(8,4) NOT NULL,
                          "SOURCE_MOVEMENT_ID" NUMBER(19) NULL,
                          "TARGET_MOVEMENT_ID" NUMBER(19) NULL,
                          CONSTRAINT "UX_HR_BAL_MIGRATION_LINE" UNIQUE ("MIGRATION_ID","SOURCE_BALANCE_ID","TARGET_CYCLE_START_DATE"),
                          CONSTRAINT "FK_HR_BAL_MIG_LINE_BATCH" FOREIGN KEY ("MIGRATION_ID") REFERENCES "HR_LEAVE_BALANCE_MIGRATION"("ID"),
                          CONSTRAINT "FK_HR_BAL_MIG_LINE_SRC" FOREIGN KEY ("SOURCE_BALANCE_ID") REFERENCES "HR_LEAVE_BALANCE"("ID"),
                          CONSTRAINT "FK_HR_BAL_MIG_LINE_TGT" FOREIGN KEY ("TARGET_BALANCE_ID") REFERENCES "HR_LEAVE_BALANCE"("ID"),
                          CONSTRAINT "FK_HR_BAL_MIG_LINE_SRCMOV" FOREIGN KEY ("SOURCE_MOVEMENT_ID") REFERENCES "HR_LEAVE_MOVEMENT"("ID"),
                          CONSTRAINT "FK_HR_BAL_MIG_LINE_TGTMOV" FOREIGN KEY ("TARGET_MOVEMENT_ID") REFERENCES "HR_LEAVE_MOVEMENT"("ID"),
                          CONSTRAINT "CK_HR_BAL_MIG_LINE_DATES" CHECK ("SOURCE_CYCLE_END_DATE">="SOURCE_CYCLE_START_DATE" AND "TARGET_CYCLE_END_DATE">="TARGET_CYCLE_START_DATE"),
                          CONSTRAINT "CK_HR_BAL_MIG_LINE_SOURCE_NULLS" CHECK ("SOURCE_START_WAS_NULL" IN (0,1) AND "SOURCE_END_WAS_NULL" IN (0,1))
                        )
                    ~';
                END IF;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve migration audit batches and allocation snapshots during rollback.
    }
}
