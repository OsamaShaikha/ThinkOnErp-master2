-- =====================================================
-- Create SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA
-- Description: Retrieves tickets approaching SLA deadline for escalation
-- Issue: PLS-00201 identifier must be declared
-- Date: 2026-05-05
-- =====================================================

CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA (
    P_CUTOFF_TIME IN DATE,
    P_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    -- Open cursor with tickets approaching SLA deadline
    -- Returns tickets that:
    -- 1. Are active (IS_ACTIVE = 'Y')
    -- 2. Have not been resolved yet (ACTUAL_RESOLUTION_DATE IS NULL)
    -- 3. Have expected resolution date before the cutoff time
    -- 4. Have expected resolution date in the future (not already overdue)
    
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

-- Verify the procedure was created
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name = 'SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA';

COMMIT;

PROMPT =====================================================
PROMPT SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA created successfully
PROMPT This procedure supports SLA escalation monitoring
PROMPT =====================================================
