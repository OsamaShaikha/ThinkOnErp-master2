-- DRAFT ONLY — OPS-12
-- Review this change with all three trigger owners before execution.
-- This script is deliberately disabled until every owner approval is entered.
-- It replaces only the malformed trigger bodies already confirmed on
-- DEV_TEMPLATE.HR_LEAVE_MOVEMENT. The business rule remains append-only.

SET DEFINE ON
WHENEVER SQLERROR EXIT SQL.SQLCODE

DEFINE APPROVAL_THINKONERP_1122 = 'NOT_APPROVED'
DEFINE APPROVAL_THINKONERP_SALONDEMO = 'NOT_APPROVED'
DEFINE APPROVAL_THINKONERP_THINKONCO = 'NOT_APPROVED'

DECLARE
    owner_count NUMBER;
    trigger_count NUMBER;
    privilege_count NUMBER;
    source_text VARCHAR2(4000);
    expected_source VARCHAR2(4000);
    owner_name VARCHAR2(128);
BEGIN
    IF UPPER('&&APPROVAL_THINKONERP_1122') <> 'APPROVED'
       OR UPPER('&&APPROVAL_THINKONERP_SALONDEMO') <> 'APPROVED'
       OR UPPER('&&APPROVAL_THINKONERP_THINKONCO') <> 'APPROVED' THEN
        RAISE_APPLICATION_ERROR(-20410, 'OPS-12 owner review is not recorded for all three triggers.');
    END IF;

    SELECT COUNT(*) INTO privilege_count
    FROM SESSION_PRIVS
    WHERE PRIVILEGE = 'CREATE ANY TRIGGER';
    IF privilege_count <> 1 THEN
        RAISE_APPLICATION_ERROR(-20411, 'The session lacks CREATE ANY TRIGGER; no trigger was changed.');
    END IF;

    SELECT COUNT(DISTINCT OWNER) INTO owner_count
    FROM ALL_TRIGGERS
    WHERE OWNER IN ('THINKONERP_1122','THINKONERP_SALONDEMO','THINKONERP_THINKONCO')
      AND TRIGGER_NAME='TR_HR_LEAVE_MOVEMENT_IMM'
      AND TABLE_OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_MOVEMENT';
    IF owner_count <> 3 THEN
        RAISE_APPLICATION_ERROR(-20412, 'The expected three owner triggers are not all attached to DEV_TEMPLATE.HR_LEAVE_MOVEMENT.');
    END IF;

    FOR expected_owner IN (
        SELECT 'THINKONERP_1122' AS OWNER FROM DUAL
        UNION ALL SELECT 'THINKONERP_SALONDEMO' FROM DUAL
        UNION ALL SELECT 'THINKONERP_THINKONCO' FROM DUAL
    ) LOOP
        owner_name := expected_owner.OWNER;
        SELECT COUNT(*) INTO trigger_count
        FROM ALL_TRIGGERS
        WHERE OWNER=owner_name AND TRIGGER_NAME='TR_HR_LEAVE_MOVEMENT_IMM'
          AND TABLE_OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_MOVEMENT'
          AND STATUS='ENABLED' AND TRIGGER_TYPE='BEFORE EACH ROW'
          AND UPPER(TRIGGERING_EVENT)='UPDATE OR DELETE';
        IF trigger_count <> 1 THEN
            RAISE_APPLICATION_ERROR(-20413, owner_name || ' trigger metadata differs from the reviewed snapshot.');
        END IF;

        SELECT REGEXP_REPLACE(UPPER(LISTAGG(TEXT,'') WITHIN GROUP (ORDER BY LINE)), '[[:space:]]', '')
        INTO source_text
        FROM ALL_SOURCE
        WHERE OWNER=owner_name AND NAME='TR_HR_LEAVE_MOVEMENT_IMM' AND TYPE='TRIGGER';

        expected_source := 'TRIGGER"' || owner_name || '"."TR_HR_LEAVE_MOVEMENT_IMM"'
            || 'BEFOREUPDATEORDELETEONDEV_TEMPLATE.HR_LEAVE_MOVEMENTFOREACHROW'
            || 'BEGINRAISE_APPLICATION_ERROR(-20071,''LEAVELEDGERISAPPEND-ONLY;USEALINKEDREVERSAL.'');END;'
            || 'ALTERTRIGGER"' || owner_name || '"."TR_HR_LEAVE_MOVEMENT_IMM"ENABLE';
        IF source_text <> expected_source THEN
            RAISE_APPLICATION_ERROR(-20414, owner_name || ' trigger source differs from the reviewed malformed body.');
        END IF;
    END LOOP;
END;
/

CREATE OR REPLACE TRIGGER "THINKONERP_1122"."TR_HR_LEAVE_MOVEMENT_IMM"
BEFORE UPDATE OR DELETE ON "DEV_TEMPLATE"."HR_LEAVE_MOVEMENT"
FOR EACH ROW
BEGIN
    RAISE_APPLICATION_ERROR(-20071, 'Leave ledger is append-only; use a linked reversal.');
END;
/

CREATE OR REPLACE TRIGGER "THINKONERP_SALONDEMO"."TR_HR_LEAVE_MOVEMENT_IMM"
BEFORE UPDATE OR DELETE ON "DEV_TEMPLATE"."HR_LEAVE_MOVEMENT"
FOR EACH ROW
BEGIN
    RAISE_APPLICATION_ERROR(-20071, 'Leave ledger is append-only; use a linked reversal.');
END;
/

CREATE OR REPLACE TRIGGER "THINKONERP_THINKONCO"."TR_HR_LEAVE_MOVEMENT_IMM"
BEFORE UPDATE OR DELETE ON "DEV_TEMPLATE"."HR_LEAVE_MOVEMENT"
FOR EACH ROW
BEGIN
    RAISE_APPLICATION_ERROR(-20071, 'Leave ledger is append-only; use a linked reversal.');
END;
/

DECLARE
    invalid_count NUMBER;
    disabled_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO invalid_count
    FROM ALL_ERRORS
    WHERE OWNER IN ('THINKONERP_1122','THINKONERP_SALONDEMO','THINKONERP_THINKONCO')
      AND NAME='TR_HR_LEAVE_MOVEMENT_IMM' AND TYPE='TRIGGER';
    SELECT COUNT(*) INTO disabled_count
    FROM ALL_TRIGGERS
    WHERE OWNER IN ('THINKONERP_1122','THINKONERP_SALONDEMO','THINKONERP_THINKONCO')
      AND TRIGGER_NAME='TR_HR_LEAVE_MOVEMENT_IMM'
      AND TABLE_OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_MOVEMENT'
      AND STATUS <> 'ENABLED';
    IF invalid_count <> 0 OR disabled_count <> 0 THEN
        RAISE_APPLICATION_ERROR(-20415, 'Trigger repair did not leave all three owners enabled and valid.');
    END IF;
END;
/

PROMPT OPS-12 trigger source corrected; no table rows or other schema objects were changed.
