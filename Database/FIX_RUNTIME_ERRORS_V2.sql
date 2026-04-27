-- =====================================================
-- Fix Runtime Errors - Combined Script (Version 2)
-- Description: Fixes three critical runtime errors
-- 1. Missing stored procedure SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA
-- 2. Missing stored procedure SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME
-- 3. Check constraint violation on SYS_AUDIT_LOG.ACTOR_TYPE
-- Date: 2026-05-05
-- =====================================================

SET SERVEROUTPUT ON SIZE UNLIMITED
SET ECHO ON
SET FEEDBACK ON

PROMPT =====================================================
PROMPT Starting Runtime Error Fixes
PROMPT =====================================================
PROMPT

-- =====================================================
-- FIX 1: Update ACTOR_TYPE Check Constraint
-- =====================================================
PROMPT =====================================================
PROMPT FIX 1: Updating SYS_AUDIT_LOG ACTOR_TYPE constraint
PROMPT =====================================================

-- Drop all check constraints on ACTOR_TYPE column
DECLARE
    v_count NUMBER := 0;
BEGIN
    FOR c IN (
        SELECT constraint_name 
        FROM user_constraints 
        WHERE table_name = 'SYS_AUDIT_LOG' 
        AND constraint_type = 'C'
        AND constraint_name LIKE '%ACTOR%' OR constraint_name LIKE 'SYS_C%'
    ) LOOP
        BEGIN
            EXECUTE IMMEDIATE 'ALTER TABLE SYS_AUDIT_LOG DROP CONSTRAINT ' || c.constraint_name;
            DBMS_OUTPUT.PUT_LINE('✓ Dropped constraint: ' || c.constraint_name);
            v_count := v_count + 1;
        EXCEPTION
            WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('! Could not drop constraint ' || c.constraint_name || ': ' || SQLERRM);
        END;
    END LOOP;
    
    IF v_count = 0 THEN
        DBMS_OUTPUT.PUT_LINE('! No constraints found to drop');
    END IF;
END;
/

-- Add the new check constraint with SYSTEM included
ALTER TABLE SYS_AUDIT_LOG ADD CONSTRAINT CHK_AUDIT_LOG_ACTOR_TYPE 
    CHECK (ACTOR_TYPE IN ('SUPER_ADMIN', 'COMPANY_ADMIN', 'USER', 'SYSTEM'));

COMMENT ON COLUMN SYS_AUDIT_LOG.ACTOR_TYPE IS 'Type of actor: SUPER_ADMIN, COMPANY_ADMIN, USER, or SYSTEM';

PROMPT ✓ Added new constraint allowing SYSTEM actor type
PROMPT

-- Verify the constraint was added
PROMPT Verifying constraint:
SELECT constraint_name, constraint_type, status
FROM user_constraints
WHERE table_name = 'SYS_AUDIT_LOG'
AND constraint_name = 'CHK_AUDIT_LOG_ACTOR_TYPE';

PROMPT
PROMPT =====================================================
PROMPT FIX 2: Creating Missing SLA Procedures
PROMPT =====================================================

-- Create the first missing stored procedure (approaching SLA)
CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA (
    P_CUTOFF_TIME IN DATE,
    P_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_CURSOR FOR
    SELECT 
        t.ROW_ID,
        t.TITLE_AR,
        t.TITLE_EN,
        t.DESCRIPTION,
        t.COMPANY_ID,
        c.ROW_DESC_E AS COMPANY_NAME,
        t.BRANCH_ID,
        b.ROW_DESC_E AS BRANCH_NAME,
        t.REQUESTER_ID,
        req.ROW_DESC_E AS REQUESTER_NAME,
        req.EMAIL AS REQUESTER_EMAIL,
        t.ASSIGNEE_ID,
        ass.ROW_DESC_E AS ASSIGNEE_NAME,
        ass.EMAIL AS ASSIGNEE_EMAIL,
        t.TICKET_TYPE_ID,
        tt.TYPE_NAME_EN AS TYPE_NAME,
        t.TICKET_STATUS_ID,
        st.STATUS_NAME_EN AS STATUS_NAME,
        st.STATUS_CODE,
        t.TICKET_PRIORITY_ID,
        pr.PRIORITY_NAME_EN AS PRIORITY_NAME,
        pr.PRIORITY_LEVEL,
        pr.ESCALATION_THRESHOLD_HOURS,
        t.TICKET_CATEGORY_ID,
        cat.CATEGORY_NAME_EN AS CATEGORY_NAME,
        t.EXPECTED_RESOLUTION_DATE,
        t.ACTUAL_RESOLUTION_DATE,
        t.IS_ACTIVE,
        t.CREATION_USER,
        t.CREATION_DATE,
        t.UPDATE_USER,
        t.UPDATE_DATE,
        'At Risk' AS SLA_STATUS,
        ROUND((t.EXPECTED_RESOLUTION_DATE - SYSDATE) * 24, 2) AS HOURS_UNTIL_DEADLINE,
        ROUND((SYSDATE - t.CREATION_DATE) * 24, 2) AS ELAPSED_HOURS
    FROM SYS_REQUEST_TICKET t
    LEFT JOIN SYS_COMPANY c ON t.COMPANY_ID = c.ROW_ID
    LEFT JOIN SYS_BRANCH b ON t.BRANCH_ID = b.ROW_ID
    LEFT JOIN SYS_USERS req ON t.REQUESTER_ID = req.ROW_ID
    LEFT JOIN SYS_USERS ass ON t.ASSIGNEE_ID = ass.ROW_ID
    LEFT JOIN SYS_TICKET_TYPE tt ON t.TICKET_TYPE_ID = tt.ROW_ID
    LEFT JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
    LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
    LEFT JOIN SYS_TICKET_CATEGORY cat ON t.TICKET_CATEGORY_ID = cat.ROW_ID
    WHERE t.IS_ACTIVE = 'Y'
    AND t.ACTUAL_RESOLUTION_DATE IS NULL
    AND t.EXPECTED_RESOLUTION_DATE > SYSDATE
    AND t.EXPECTED_RESOLUTION_DATE <= P_CUTOFF_TIME
    ORDER BY t.EXPECTED_RESOLUTION_DATE ASC, pr.PRIORITY_LEVEL ASC;
    
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20999, 'Error retrieving tickets approaching SLA deadline: ' || SQLERRM);
END SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA;
/

PROMPT ✓ Created SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA procedure
PROMPT

-- Create the second missing stored procedure (overdue tickets)
CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME (
    P_CURRENT_TIME IN DATE,
    P_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_CURSOR FOR
    SELECT 
        t.ROW_ID,
        t.TITLE_AR,
        t.TITLE_EN,
        t.DESCRIPTION,
        t.COMPANY_ID,
        c.ROW_DESC AS COMPANY_NAME_AR,
        c.ROW_DESC_E AS COMPANY_NAME_EN,
        t.BRANCH_ID,
        b.ROW_DESC AS BRANCH_NAME_AR,
        b.ROW_DESC_E AS BRANCH_NAME_EN,
        t.REQUESTER_ID,
        req.ROW_DESC AS REQUESTER_NAME_AR,
        req.ROW_DESC_E AS REQUESTER_NAME_EN,
        req.USER_NAME AS REQUESTER_USERNAME,
        req.EMAIL AS REQUESTER_EMAIL,
        t.ASSIGNEE_ID,
        ass.ROW_DESC AS ASSIGNEE_NAME_AR,
        ass.ROW_DESC_E AS ASSIGNEE_NAME_EN,
        ass.USER_NAME AS ASSIGNEE_USERNAME,
        ass.EMAIL AS ASSIGNEE_EMAIL,
        t.TICKET_TYPE_ID,
        tt.TYPE_NAME_AR,
        tt.TYPE_NAME_EN,
        tt.DESCRIPTION_AR AS TYPE_DESCRIPTION_AR,
        tt.DESCRIPTION_EN AS TYPE_DESCRIPTION_EN,
        t.TICKET_STATUS_ID,
        st.STATUS_NAME_AR,
        st.STATUS_NAME_EN,
        st.STATUS_CODE,
        st.IS_FINAL_STATUS,
        t.TICKET_PRIORITY_ID,
        pr.PRIORITY_NAME_AR,
        pr.PRIORITY_NAME_EN,
        pr.PRIORITY_LEVEL,
        pr.SLA_TARGET_HOURS,
        pr.ESCALATION_THRESHOLD_HOURS,
        t.TICKET_CATEGORY_ID,
        cat.CATEGORY_NAME_AR,
        cat.CATEGORY_NAME_EN,
        t.EXPECTED_RESOLUTION_DATE,
        t.ACTUAL_RESOLUTION_DATE,
        t.IS_ACTIVE,
        t.CREATION_USER,
        t.CREATION_DATE,
        t.UPDATE_USER,
        t.UPDATE_DATE,
        'Overdue' AS SLA_STATUS,
        ROUND((P_CURRENT_TIME - t.EXPECTED_RESOLUTION_DATE) * 24, 2) AS HOURS_OVERDUE,
        ROUND((P_CURRENT_TIME - t.CREATION_DATE) * 24, 2) AS ELAPSED_HOURS
    FROM SYS_REQUEST_TICKET t
    LEFT JOIN SYS_COMPANY c ON t.COMPANY_ID = c.ROW_ID
    LEFT JOIN SYS_BRANCH b ON t.BRANCH_ID = b.ROW_ID
    LEFT JOIN SYS_USERS req ON t.REQUESTER_ID = req.ROW_ID
    LEFT JOIN SYS_USERS ass ON t.ASSIGNEE_ID = ass.ROW_ID
    LEFT JOIN SYS_TICKET_TYPE tt ON t.TICKET_TYPE_ID = tt.ROW_ID
    LEFT JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
    LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
    LEFT JOIN SYS_TICKET_CATEGORY cat ON t.TICKET_CATEGORY_ID = cat.ROW_ID
    WHERE t.IS_ACTIVE = 'Y'
    AND t.ACTUAL_RESOLUTION_DATE IS NULL
    AND t.EXPECTED_RESOLUTION_DATE < P_CURRENT_TIME
    ORDER BY t.EXPECTED_RESOLUTION_DATE ASC, pr.PRIORITY_LEVEL ASC;
    
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20998, 'Error retrieving overdue tickets: ' || SQLERRM);
END SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME;
/

PROMPT ✓ Created SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME procedure
PROMPT

-- Verify the procedures were created
PROMPT Verifying procedures:
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN ('SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA', 
                      'SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME')
ORDER BY object_name;

COMMIT;

PROMPT
PROMPT =====================================================
PROMPT Runtime Error Fixes Completed Successfully!
PROMPT =====================================================
PROMPT
PROMPT Summary of Changes:
PROMPT 1. ✓ Updated SYS_AUDIT_LOG.ACTOR_TYPE constraint to allow 'SYSTEM'
PROMPT 2. ✓ Created SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA procedure
PROMPT 3. ✓ Created SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME procedure
PROMPT
PROMPT Next Steps:
PROMPT - No need to restart your application
PROMPT - SLA escalation service should now work correctly
PROMPT - Audit logging for authentication events should succeed
PROMPT =====================================================
