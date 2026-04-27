-- =====================================================
-- Create SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME
-- Description: Retrieves tickets that are overdue based on current time
-- Issue: PLS-00201 identifier must be declared
-- Date: 2026-05-05
-- =====================================================

CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME (
    P_CURRENT_TIME IN DATE,
    P_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    -- Open cursor with overdue tickets
    -- Returns tickets that:
    -- 1. Are active (IS_ACTIVE = 'Y')
    -- 2. Have not been resolved yet (ACTUAL_RESOLUTION_DATE IS NULL)
    -- 3. Have expected resolution date before current time (overdue)
    
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

-- Verify the procedure was created
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name = 'SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME';

COMMIT;

PROMPT =====================================================
PROMPT SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME created successfully
PROMPT This procedure supports SLA escalation for overdue tickets
PROMPT =====================================================
