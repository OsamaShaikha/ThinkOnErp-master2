using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261115001800_AddAttendanceTimeSettings")]
public sealed class AddAttendanceTimeSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE
                owner_name VARCHAR2(128) := SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA');
                table_count NUMBER;
                compatible_columns NUMBER;
                pk_count NUMBER;
                pk_signature NUMBER;
                invalid_keys NUMBER;
                name_collision NUMBER;
                pk_name VARCHAR2(128);
                trigger_count NUMBER;
                trigger_source VARCHAR2(4000);
                trigger_body VARCHAR2(4000);
                trigger_body_start NUMBER;
                trigger_body_end NUMBER;
                expected_trigger_body CONSTANT VARCHAR2(4000) := 'BEGINRAISE_APPLICATION_ERROR(-20072,''ATTENDANCETIMESETTINGSREQUIREADATEDMIGRATION;UPDATEANDDELETEAREDISABLED.'');END;';
            BEGIN
                SELECT COUNT(*) INTO trigger_count FROM ALL_TRIGGERS
                WHERE OWNER=owner_name AND TRIGGER_NAME='TRG_HR_TIME_IMMUTABLE';
                IF trigger_count=1 THEN
                    SELECT COUNT(*) INTO trigger_count FROM ALL_TRIGGERS
                    WHERE OWNER=owner_name AND TRIGGER_NAME='TRG_HR_TIME_IMMUTABLE'
                      AND TABLE_OWNER=owner_name AND TABLE_NAME='HR_ATTENDANCE_TIME_SETTINGS'
                      AND TRIGGER_TYPE='BEFORE STATEMENT'
                      AND INSTR(TRIGGERING_EVENT,'UPDATE')>0 AND INSTR(TRIGGERING_EVENT,'DELETE')>0;
                    SELECT REGEXP_REPLACE(UPPER(LISTAGG(TEXT,'') WITHIN GROUP (ORDER BY LINE)), '[[:space:]]', '')
                    INTO trigger_source FROM ALL_SOURCE
                    WHERE OWNER=owner_name AND NAME='TRG_HR_TIME_IMMUTABLE' AND TYPE='TRIGGER';
                    trigger_body_start := INSTR(trigger_source, 'BEGIN');
                    trigger_body_end := INSTR(trigger_source, 'END;', -1);
                    IF trigger_body_start>0 AND trigger_body_end>=trigger_body_start THEN
                        trigger_body := SUBSTR(trigger_source, trigger_body_start, trigger_body_end + 4 - trigger_body_start);
                    END IF;
                    IF trigger_count<>1 OR trigger_source IS NULL OR trigger_body_start IS NULL OR trigger_body_start=0
                       OR trigger_body_end IS NULL OR trigger_body_end=0 OR trigger_body_end<trigger_body_start
                       OR trigger_body IS NULL OR INSTR(trigger_source, 'DECLARE')>0 OR trigger_body<>expected_trigger_body THEN
                        RAISE_APPLICATION_ERROR(-20271, 'Existing TRG_HR_TIME_IMMUTABLE has an unexpected target, events, or body; migration was refused before DDL.');
                    END IF;
                ELSIF trigger_count>1 THEN
                    RAISE_APPLICATION_ERROR(-20271, 'TRG_HR_TIME_IMMUTABLE metadata is ambiguous; migration was refused before DDL.');
                END IF;

                SELECT COUNT(*) INTO table_count FROM ALL_TABLES
                WHERE OWNER=owner_name AND TABLE_NAME='HR_ATTENDANCE_TIME_SETTINGS';
                IF table_count=0 THEN
                    SELECT (SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER=owner_name AND CONSTRAINT_NAME='PK_HR_ATT_TIME_SETTINGS')
                         + (SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER=owner_name AND INDEX_NAME='PK_HR_ATT_TIME_SETTINGS')
                    INTO name_collision FROM DUAL;
                    IF name_collision<>0 THEN
                        RAISE_APPLICATION_ERROR(-20272, 'PK_HR_ATT_TIME_SETTINGS is already used by another object.');
                    END IF;
                ELSE
                    SELECT COUNT(*) INTO compatible_columns
                    FROM ALL_TAB_COLS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_ATTENDANCE_TIME_SETTINGS'
                      AND ((COLUMN_NAME='COMPANY_ID' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND DATA_SCALE=0 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='TIME_ZONE_ID' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH>=100 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='CREATION_USER' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH>=200 AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO')
                        OR (COLUMN_NAME='CREATION_DATE' AND DATA_TYPE='TIMESTAMP(7)' AND NULLABLE='N' AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN='NO'));
                    IF compatible_columns<>4 THEN
                        RAISE_APPLICATION_ERROR(-20273, 'Existing HR_ATTENDANCE_TIME_SETTINGS columns do not match the EF mapping.');
                    END IF;

                    EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM (SELECT "COMPANY_ID" FROM "HR_ATTENDANCE_TIME_SETTINGS" GROUP BY "COMPANY_ID" HAVING "COMPANY_ID" IS NULL OR COUNT(*)>1)' INTO invalid_keys;
                    IF invalid_keys<>0 THEN
                        RAISE_APPLICATION_ERROR(-20277, 'HR_ATTENDANCE_TIME_SETTINGS.COMPANY_ID contains NULL or duplicate values.');
                    END IF;

                    SELECT COUNT(*) INTO pk_count FROM ALL_CONSTRAINTS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_ATTENDANCE_TIME_SETTINGS' AND CONSTRAINT_TYPE='P';
                    IF pk_count>1 THEN
                        RAISE_APPLICATION_ERROR(-20274, 'HR_ATTENDANCE_TIME_SETTINGS has ambiguous primary-key metadata.');
                    ELSIF pk_count=1 THEN
                        SELECT COUNT(*) INTO pk_signature FROM ALL_CONSTRAINTS c
                        WHERE c.OWNER=owner_name AND c.TABLE_NAME='HR_ATTENDANCE_TIME_SETTINGS' AND c.CONSTRAINT_TYPE='P'
                          AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME)=1
                          AND EXISTS (SELECT 1 FROM ALL_CONS_COLUMNS cc WHERE cc.OWNER=c.OWNER AND cc.TABLE_NAME=c.TABLE_NAME AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME AND cc.COLUMN_NAME='COMPANY_ID' AND cc.POSITION=1);
                        IF pk_signature<>1 THEN
                            RAISE_APPLICATION_ERROR(-20275, 'HR_ATTENDANCE_TIME_SETTINGS primary key must contain only COMPANY_ID.');
                        END IF;
                    ELSE
                        SELECT (SELECT COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER=owner_name AND CONSTRAINT_NAME='PK_HR_ATT_TIME_SETTINGS')
                             + (SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER=owner_name AND INDEX_NAME='PK_HR_ATT_TIME_SETTINGS')
                        INTO name_collision FROM DUAL;
                        IF name_collision<>0 THEN
                            RAISE_APPLICATION_ERROR(-20272, 'PK_HR_ATT_TIME_SETTINGS is already used by another object.');
                        END IF;
                        EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "HR_ATTENDANCE_TIME_SETTINGS" WHERE "COMPANY_ID" IS NULL' INTO invalid_keys;
                        IF invalid_keys<>0 THEN
                            RAISE_APPLICATION_ERROR(-20276, 'HR_ATTENDANCE_TIME_SETTINGS.COMPANY_ID contains NULL values.');
                        END IF;
                        EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM (SELECT "COMPANY_ID" FROM "HR_ATTENDANCE_TIME_SETTINGS" GROUP BY "COMPANY_ID" HAVING COUNT(*)>1)' INTO invalid_keys;
                        IF invalid_keys<>0 THEN
                            RAISE_APPLICATION_ERROR(-20277, 'HR_ATTENDANCE_TIME_SETTINGS.COMPANY_ID contains duplicates.');
                        END IF;
                    END IF;
                END IF;

                IF table_count=0 THEN
                    EXECUTE IMMEDIATE q'~
                        CREATE TABLE "HR_ATTENDANCE_TIME_SETTINGS" (
                          "COMPANY_ID" NUMBER(19) NOT NULL CONSTRAINT "PK_HR_ATT_TIME_SETTINGS" PRIMARY KEY,
                          "TIME_ZONE_ID" NVARCHAR2(100) NOT NULL,
                          "CREATION_USER" NVARCHAR2(200) NOT NULL,
                          "CREATION_DATE" TIMESTAMP(7) NOT NULL
                        )
                    ~';
                ELSIF pk_count=0 THEN
                    EXECUTE IMMEDIATE 'ALTER TABLE "HR_ATTENDANCE_TIME_SETTINGS" ADD CONSTRAINT "PK_HR_ATT_TIME_SETTINGS" PRIMARY KEY ("COMPANY_ID")';
                ELSE
                    SELECT COUNT(*) INTO pk_signature FROM ALL_CONSTRAINTS
                    WHERE OWNER=owner_name AND TABLE_NAME='HR_ATTENDANCE_TIME_SETTINGS'
                      AND CONSTRAINT_TYPE='P' AND STATUS='ENABLED' AND VALIDATED='VALIDATED';
                    IF pk_signature<>1 THEN
                        SELECT CONSTRAINT_NAME INTO pk_name FROM ALL_CONSTRAINTS
                        WHERE OWNER=owner_name AND TABLE_NAME='HR_ATTENDANCE_TIME_SETTINGS' AND CONSTRAINT_TYPE='P';
                        EXECUTE IMMEDIATE 'ALTER TABLE "HR_ATTENDANCE_TIME_SETTINGS" ENABLE VALIDATE CONSTRAINT "' || REPLACE(pk_name,'"','""') || '"';
                    END IF;
                END IF;

                EXECUTE IMMEDIATE q'~
                    CREATE OR REPLACE TRIGGER "TRG_HR_TIME_IMMUTABLE"
                    BEFORE UPDATE OR DELETE ON "HR_ATTENDANCE_TIME_SETTINGS"
                    BEGIN
                      RAISE_APPLICATION_ERROR(-20072, 'Attendance time settings require a dated migration; UPDATE and DELETE are disabled.');
                    END;
                ~';
                END;
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve company time-zone configuration and historical interpretation on rollback.
    }
}
