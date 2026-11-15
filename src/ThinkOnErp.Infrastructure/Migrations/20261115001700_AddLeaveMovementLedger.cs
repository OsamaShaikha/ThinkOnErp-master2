using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115001700_AddLeaveMovementLedger")]
public sealed class AddLeaveMovementLedger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                owner_name VARCHAR2(128) := SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA');
                table_count NUMBER;
                valid_columns NUMBER;
                identity_count NUMBER;
                valid_constraints NUMBER;
                signature_count NUMBER;
                parent_key_count NUMBER;
                collision_count NUMBER;
                trigger_count NUMBER;
                trigger_source VARCHAR2(4000);
                trigger_body VARCHAR2(4000);
                trigger_body_start NUMBER;
                trigger_body_end NUMBER;
                expected_trigger_body CONSTANT VARCHAR2(4000) := 'BEGINRAISE_APPLICATION_ERROR(-20071,''LEAVELEDGERISAPPEND-ONLY;USEALINKEDREVERSAL.'');END;';
            BEGIN
                SELECT COUNT(*) INTO parent_key_count
                FROM ALL_TAB_COLS c
                WHERE c.OWNER=owner_name AND c.TABLE_NAME='HR_LEAVE_BALANCE' AND c.COLUMN_NAME='ID'
                  AND c.DATA_TYPE='NUMBER' AND c.DATA_PRECISION=19 AND c.DATA_SCALE=0
                  AND c.NULLABLE='N' AND c.VIRTUAL_COLUMN='NO'
                  AND EXISTS (
                      SELECT 1 FROM ALL_CONSTRAINTS p
                      WHERE p.OWNER=c.OWNER AND p.TABLE_NAME=c.TABLE_NAME
                        AND p.CONSTRAINT_TYPE IN ('P','U') AND p.STATUS='ENABLED' AND p.VALIDATED='VALIDATED'
                        AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS pc WHERE pc.OWNER=p.OWNER AND pc.TABLE_NAME=p.TABLE_NAME AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME)=1
                        AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS pc WHERE pc.OWNER=p.OWNER AND pc.TABLE_NAME=p.TABLE_NAME AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME AND pc.COLUMN_NAME='ID' AND pc.POSITION=1)
                  );
                IF parent_key_count <> 1 THEN
                    RAISE_APPLICATION_ERROR(-20260, 'HR_LEAVE_BALANCE.ID must be NUMBER(19,0) NOT NULL with an enabled, validated single-column key before the leave movement ledger can be installed.');
                END IF;

                SELECT COUNT(*) INTO trigger_count FROM ALL_TRIGGERS
                WHERE OWNER=owner_name AND TRIGGER_NAME='TR_HR_LEAVE_MOVEMENT_IMM';
                IF trigger_count=1 THEN
                    SELECT COUNT(*) INTO trigger_count FROM ALL_TRIGGERS
                    WHERE OWNER=owner_name AND TRIGGER_NAME='TR_HR_LEAVE_MOVEMENT_IMM'
                      AND TABLE_OWNER=owner_name AND TABLE_NAME='HR_LEAVE_MOVEMENT'
                      AND TRIGGER_TYPE='BEFORE EACH ROW'
                      AND INSTR(TRIGGERING_EVENT,'UPDATE')>0 AND INSTR(TRIGGERING_EVENT,'DELETE')>0;
                    SELECT REGEXP_REPLACE(UPPER(LISTAGG(TEXT,'') WITHIN GROUP (ORDER BY LINE)), '[[:space:]]', '')
                    INTO trigger_source FROM ALL_SOURCE
                    WHERE OWNER=owner_name AND NAME='TR_HR_LEAVE_MOVEMENT_IMM' AND TYPE='TRIGGER';
                    trigger_body_start := INSTR(trigger_source, 'BEGIN');
                    trigger_body_end := INSTR(trigger_source, 'END;', -1);
                    IF trigger_body_start>0 AND trigger_body_end>=trigger_body_start THEN
                        trigger_body := SUBSTR(trigger_source, trigger_body_start, trigger_body_end + 4 - trigger_body_start);
                    END IF;
                    IF trigger_count<>1 OR trigger_source IS NULL OR trigger_body_start IS NULL OR trigger_body_start=0
                       OR trigger_body_end IS NULL OR trigger_body_end=0 OR trigger_body_end<trigger_body_start
                       OR trigger_body IS NULL OR INSTR(trigger_source, 'DECLARE')>0 OR trigger_body<>expected_trigger_body THEN
                        RAISE_APPLICATION_ERROR(-20266, 'Existing append-only trigger name has an unexpected target, events, or body; replacement was refused before DDL.');
                    END IF;
                ELSIF trigger_count>1 THEN
                    RAISE_APPLICATION_ERROR(-20266, 'Append-only trigger name is ambiguous; replacement was refused before DDL.');
                END IF;

                SELECT COUNT(*) INTO table_count FROM ALL_TABLES WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_MOVEMENT';
                IF table_count=0 THEN
                    SELECT COUNT(*) INTO collision_count
                    FROM ALL_CONSTRAINTS
                    WHERE OWNER=owner_name AND CONSTRAINT_NAME IN (
                        'PK_HR_LEAVE_MOVEMENT','FK_HR_LEAVE_MOVEMENT_BAL','FK_HR_LEAVE_MOVEMENT_REV',
                        'UX_HR_LEAVE_MOVEMENT_OP','UX_HR_LEAVE_MOVEMENT_SEQ','UX_HR_LEAVE_MOVEMENT_REV');
                    IF collision_count<>0 THEN
                        RAISE_APPLICATION_ERROR(-20261, 'HR_LEAVE_MOVEMENT constraint names are already in use.');
                    END IF;
                    SELECT COUNT(*) INTO collision_count FROM ALL_INDEXES
                    WHERE OWNER=owner_name AND INDEX_NAME IN (
                        'PK_HR_LEAVE_MOVEMENT','UX_HR_LEAVE_MOVEMENT_OP','UX_HR_LEAVE_MOVEMENT_SEQ','UX_HR_LEAVE_MOVEMENT_REV');
                    IF collision_count<>0 THEN
                        RAISE_APPLICATION_ERROR(-20262, 'HR_LEAVE_MOVEMENT index names are already in use.');
                    END IF;

                    EXECUTE IMMEDIATE q'~
                        CREATE TABLE "HR_LEAVE_MOVEMENT" (
                          "ID" NUMBER(19) GENERATED BY DEFAULT ON NULL AS IDENTITY CONSTRAINT "PK_HR_LEAVE_MOVEMENT" PRIMARY KEY,
                          "BALANCE_ID" NUMBER(19) NOT NULL,
                          "EMPLOYEE_CODE" NVARCHAR2(100) NOT NULL,
                          "LEAVE_TYPE_CODE" NVARCHAR2(100) NOT NULL,
                          "CYCLE_START_DATE" TIMESTAMP(7) NOT NULL,
                          "SEQUENCE_NO" NUMBER(19) NOT NULL,
                          "KIND" NVARCHAR2(40) NOT NULL,
                          "OPERATION_KEY" NVARCHAR2(64) NOT NULL,
                          "PAYLOAD_HASH" NVARCHAR2(64) NULL,
                          "SOURCE_REF" NVARCHAR2(300) NOT NULL,
                          "REASON" NVARCHAR2(1000) NOT NULL,
                          "ACTOR" NVARCHAR2(200) NOT NULL,
                          "CREATED_DATE" TIMESTAMP(7) NOT NULL,
                          "ACCRUED_DELTA" NUMBER(12,4) NOT NULL,
                          "USED_DELTA" NUMBER(12,4) NOT NULL,
                          "CARRIED_DELTA" NUMBER(12,4) NOT NULL,
                          "ACCRUED_AFTER" NUMBER(12,4) NOT NULL,
                          "USED_AFTER" NUMBER(12,4) NOT NULL,
                          "CARRIED_AFTER" NUMBER(12,4) NOT NULL,
                          "EXPIRED_DAYS" NUMBER(12,4) NOT NULL,
                          "TRANSFERRED_DAYS" NUMBER(12,4) NOT NULL,
                          "REVERSES_MOVEMENT_ID" NUMBER(19) NULL,
                          CONSTRAINT "FK_HR_LEAVE_MOVEMENT_BAL" FOREIGN KEY ("BALANCE_ID") REFERENCES "HR_LEAVE_BALANCE"("ID"),
                          CONSTRAINT "FK_HR_LEAVE_MOVEMENT_REV" FOREIGN KEY ("REVERSES_MOVEMENT_ID") REFERENCES "HR_LEAVE_MOVEMENT"("ID"),
                          CONSTRAINT "UX_HR_LEAVE_MOVEMENT_OP" UNIQUE ("OPERATION_KEY"),
                          CONSTRAINT "UX_HR_LEAVE_MOVEMENT_SEQ" UNIQUE ("BALANCE_ID","SEQUENCE_NO"),
                          CONSTRAINT "UX_HR_LEAVE_MOVEMENT_REV" UNIQUE ("REVERSES_MOVEMENT_ID")
                        )
                    ~';
                ELSE
                    SELECT COUNT(*) INTO valid_columns
                    FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_MOVEMENT'
                      AND ((COLUMN_NAME='ID' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND DATA_SCALE=0 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='BALANCE_ID' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND DATA_SCALE=0 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='EMPLOYEE_CODE' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH>=100 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='LEAVE_TYPE_CODE' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH>=100 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='CYCLE_START_DATE' AND DATA_TYPE LIKE 'TIMESTAMP%' AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='SEQUENCE_NO' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND DATA_SCALE=0 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='KIND' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH>=40 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='OPERATION_KEY' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH>=64 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='PAYLOAD_HASH' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH>=64 AND NULLABLE='Y' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='SOURCE_REF' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH>=300 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='REASON' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH>=1000 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='ACTOR' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH>=200 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='CREATED_DATE' AND DATA_TYPE LIKE 'TIMESTAMP%' AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME IN ('ACCRUED_DELTA','USED_DELTA','CARRIED_DELTA','ACCRUED_AFTER','USED_AFTER','CARRIED_AFTER','EXPIRED_DAYS','TRANSFERRED_DAYS') AND DATA_TYPE='NUMBER' AND DATA_PRECISION=12 AND DATA_SCALE=4 AND NULLABLE='N' AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='REVERSES_MOVEMENT_ID' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND DATA_SCALE=0 AND NULLABLE='Y' AND VIRTUAL_COLUMN='NO'));
                    IF valid_columns<>22 THEN
                        RAISE_APPLICATION_ERROR(-20263, 'Existing HR_LEAVE_MOVEMENT columns do not match the mapped ledger schema.');
                    END IF;
                    SELECT COUNT(*) INTO identity_count FROM ALL_TAB_IDENTITY_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_MOVEMENT' AND COLUMN_NAME='ID';
                    IF identity_count<>1 THEN
                        RAISE_APPLICATION_ERROR(-20264, 'Existing HR_LEAVE_MOVEMENT.ID must be an identity column.');
                    END IF;
                    SELECT COUNT(*) INTO valid_constraints
                    FROM ALL_CONSTRAINTS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_LEAVE_MOVEMENT' AND STATUS='ENABLED' AND VALIDATED='VALIDATED'
                      AND ((CONSTRAINT_TYPE='P' AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=ALL_CONSTRAINTS.OWNER AND cc.TABLE_NAME=ALL_CONSTRAINTS.TABLE_NAME AND cc.CONSTRAINT_NAME=ALL_CONSTRAINTS.CONSTRAINT_NAME)=1
                            AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=ALL_CONSTRAINTS.OWNER AND cc.TABLE_NAME=ALL_CONSTRAINTS.TABLE_NAME AND cc.CONSTRAINT_NAME=ALL_CONSTRAINTS.CONSTRAINT_NAME AND cc.COLUMN_NAME='ID' AND cc.POSITION=1))
                        OR (CONSTRAINT_NAME IN ('FK_HR_LEAVE_MOVEMENT_BAL','FK_HR_LEAVE_MOVEMENT_REV') AND CONSTRAINT_TYPE='R')
                        OR (CONSTRAINT_NAME IN ('UX_HR_LEAVE_MOVEMENT_OP','UX_HR_LEAVE_MOVEMENT_SEQ','UX_HR_LEAVE_MOVEMENT_REV') AND CONSTRAINT_TYPE='U'));
                    IF valid_constraints<>6 THEN
                        RAISE_APPLICATION_ERROR(-20265, 'Existing HR_LEAVE_MOVEMENT constraints are missing, disabled, or unvalidated.');
                    END IF;
                    SELECT COUNT(*) INTO signature_count FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER=owner_name AND c.TABLE_NAME='HR_LEAVE_MOVEMENT'
                      AND c.CONSTRAINT_TYPE='P'
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1
                      AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME AND cc.COLUMN_NAME='ID' AND cc.POSITION=1);
                    IF signature_count<>1 THEN RAISE_APPLICATION_ERROR(-20267, 'HR_LEAVE_MOVEMENT primary key must cover only ID.'); END IF;

                    SELECT COUNT(*) INTO signature_count FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER=owner_name AND c.TABLE_NAME='HR_LEAVE_MOVEMENT' AND c.CONSTRAINT_NAME='FK_HR_LEAVE_MOVEMENT_BAL'
                      AND c.CONSTRAINT_TYPE='R' AND c.R_OWNER=owner_name AND c.DELETE_RULE='NO ACTION'
                      AND EXISTS (SELECT 1 FROM ALL_CONSTRAINTS p JOIN ALL_CONS_COLUMNS pc ON pc.OWNER=p.OWNER AND pc.TABLE_NAME=p.TABLE_NAME AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME
                                  WHERE p.OWNER=c.R_OWNER AND p.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME AND p.TABLE_NAME='HR_LEAVE_BALANCE'
                                    AND p.CONSTRAINT_TYPE IN ('P','U') AND p.STATUS='ENABLED' AND p.VALIDATED='VALIDATED' AND pc.COLUMN_NAME='ID' AND pc.POSITION=1
                                    AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS px WHERE px.OWNER=p.OWNER AND px.TABLE_NAME=p.TABLE_NAME AND px.CONSTRAINT_NAME=p.CONSTRAINT_NAME)=1)
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1
                      AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME AND cc.COLUMN_NAME='BALANCE_ID' AND cc.POSITION=1);
                    IF signature_count<>1 THEN RAISE_APPLICATION_ERROR(-20268, 'FK_HR_LEAVE_MOVEMENT_BAL must reference HR_LEAVE_BALANCE.ID.'); END IF;

                    SELECT COUNT(*) INTO signature_count FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER=owner_name AND c.TABLE_NAME='HR_LEAVE_MOVEMENT' AND c.CONSTRAINT_NAME='FK_HR_LEAVE_MOVEMENT_REV'
                      AND c.CONSTRAINT_TYPE='R' AND c.R_OWNER=owner_name AND c.DELETE_RULE='NO ACTION'
                      AND EXISTS (SELECT 1 FROM ALL_CONSTRAINTS p
                                  WHERE p.OWNER=c.R_OWNER AND p.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME AND p.TABLE_NAME='HR_LEAVE_MOVEMENT'
                                    AND p.CONSTRAINT_TYPE='P' AND p.STATUS='ENABLED' AND p.VALIDATED='VALIDATED'
                                    AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS pc WHERE pc.OWNER=p.OWNER AND pc.TABLE_NAME=p.TABLE_NAME AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME)=1
                                    AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS pc WHERE pc.OWNER=p.OWNER AND pc.TABLE_NAME=p.TABLE_NAME AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME AND pc.COLUMN_NAME='ID' AND pc.POSITION=1))
                      AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1
                      AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME AND cc.COLUMN_NAME='REVERSES_MOVEMENT_ID' AND cc.POSITION=1);
                    IF signature_count<>1 THEN RAISE_APPLICATION_ERROR(-20269, 'FK_HR_LEAVE_MOVEMENT_REV must reference HR_LEAVE_MOVEMENT.ID.'); END IF;

                    SELECT COUNT(*) INTO signature_count FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER=owner_name AND c.TABLE_NAME='HR_LEAVE_MOVEMENT' AND c.CONSTRAINT_TYPE='U' AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
                      AND ((c.CONSTRAINT_NAME='UX_HR_LEAVE_MOVEMENT_OP' AND (SELECT LISTAGG(cc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='OPERATION_KEY')
                        OR (c.CONSTRAINT_NAME='UX_HR_LEAVE_MOVEMENT_SEQ' AND (SELECT LISTAGG(cc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='BALANCE_ID,SEQUENCE_NO')
                        OR (c.CONSTRAINT_NAME='UX_HR_LEAVE_MOVEMENT_REV' AND (SELECT LISTAGG(cc.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY cc.POSITION) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)='REVERSES_MOVEMENT_ID'));
                    IF signature_count<>3 THEN RAISE_APPLICATION_ERROR(-20270, 'HR_LEAVE_MOVEMENT unique constraint signatures do not match the ledger contract.'); END IF;
                END IF;

                EXECUTE IMMEDIATE q'~
                    CREATE OR REPLACE TRIGGER "TR_HR_LEAVE_MOVEMENT_IMM"
                    BEFORE UPDATE OR DELETE ON "HR_LEAVE_MOVEMENT" FOR EACH ROW
                    BEGIN RAISE_APPLICATION_ERROR(-20071, 'Leave ledger is append-only; use a linked reversal.'); END;
                ~';
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Leave movements are audit history; rollback never drops the ledger or its protection.
    }
}
