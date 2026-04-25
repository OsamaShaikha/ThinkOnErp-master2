-- =============================================
-- Company Request Tickets System - Stored Procedures
-- Description: CRUD stored procedures for ticket operations with SLA calculation and audit trail
-- Requirements: 14.9-14.14, 3.1-3.12
-- =============================================

-- =============================================
-- Procedure: SP_SYS_REQUEST_TICKET_INSERT
-- Description: Inserts a new ticket with SLA calculation logic
-- Parameters:
--   P_TITLE_AR: Ticket title in Arabic
--   P_TITLE_EN: Ticket title in English
--   P_DESCRIPTION: Detailed ticket description
--   P_COMPANY_ID: Company ID (foreign key)
--   P_BRANCH_ID: Branch ID (foreign key)
--   P_REQUESTER_ID: Requester user ID (foreign key)
--   P_ASSIGNEE_ID: Assignee user ID (optional, foreign key)
--   P_TICKET_TYPE_ID: Ticket type ID (foreign key)
--   P_TICKET_PRIORITY_ID: Priority ID (foreign key)
--   P_TICKET_CATEGORY_ID: Category ID (optional, foreign key)
--   P_CREATION_USER: User creating the ticket
--   P_NEW_ID: Output parameter returning the new ticket ID
-- Requirements: 1.1-1.15, 4.2-4.6
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_INSERT (
    P_TITLE_AR IN NVARCHAR2,
    P_TITLE_EN IN NVARCHAR2,
    P_DESCRIPTION IN NCLOB,
    P_COMPANY_ID IN NUMBER,
    P_BRANCH_ID IN NUMBER,
    P_REQUESTER_ID IN NUMBER,
    P_ASSIGNEE_ID IN NUMBER,
    P_TICKET_TYPE_ID IN NUMBER,
    P_TICKET_PRIORITY_ID IN NUMBER,
    P_TICKET_CATEGORY_ID IN NUMBER,
    P_CREATION_USER IN NVARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
    V_SLA_HOURS NUMBER(10,2);
    V_EXPECTED_DATE DATE;
    V_OPEN_STATUS_ID NUMBER;
BEGIN
    -- Generate new ID from sequence
    SELECT SEQ_SYS_REQUEST_TICKET.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
    -- Get SLA target hours from priority (use priority SLA as default)
    SELECT SLA_TARGET_HOURS INTO V_SLA_HOURS
    FROM SYS_TICKET_PRIORITY
    WHERE ROW_ID = P_TICKET_PRIORITY_ID AND IS_ACTIVE = 'Y';
    
    -- Check if ticket type has specific SLA override
    BEGIN
        SELECT SLA_TARGET_HOURS INTO V_SLA_HOURS
        FROM SYS_TICKET_TYPE
        WHERE ROW_ID = P_TICKET_TYPE_ID AND IS_ACTIVE = 'Y' AND SLA_TARGET_HOURS > 0;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            -- Use priority SLA (already set above)
            NULL;
    END;
    
    -- Calculate expected resolution date (business hours calculation)
    -- For now, simple calculation: current time + SLA hours
    -- TODO: Implement business hours calculation excluding weekends/holidays
    V_EXPECTED_DATE := SYSDATE + (V_SLA_HOURS / 24);
    
    -- Get Open status ID
    SELECT ROW_ID INTO V_OPEN_STATUS_ID
    FROM SYS_TICKET_STATUS
    WHERE STATUS_CODE = 'OPEN' AND IS_ACTIVE = 'Y';
    
    -- Insert the new ticket record
    INSERT INTO SYS_REQUEST_TICKET (
        ROW_ID,
        TITLE_AR,
        TITLE_EN,
        DESCRIPTION,
        COMPANY_ID,
        BRANCH_ID,
        REQUESTER_ID,
        ASSIGNEE_ID,
        TICKET_TYPE_ID,
        TICKET_STATUS_ID,
        TICKET_PRIORITY_ID,
        TICKET_CATEGORY_ID,
        EXPECTED_RESOLUTION_DATE,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_TITLE_AR,
        P_TITLE_EN,
        P_DESCRIPTION,
        P_COMPANY_ID,
        P_BRANCH_ID,
        P_REQUESTER_ID,
        P_ASSIGNEE_ID,
        P_TICKET_TYPE_ID,
        V_OPEN_STATUS_ID,
        P_TICKET_PRIORITY_ID,
        P_TICKET_CATEGORY_ID,
        V_EXPECTED_DATE,
        'Y',
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20401, 'Error inserting ticket: ' || SQLERRM);
END SP_SYS_REQUEST_TICKET_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_REQUEST_TICKET_UPDATE
-- Description: Updates an existing ticket with audit trail
-- Parameters:
--   P_ROW_ID: Ticket ID to update
--   P_TITLE_AR: Ticket title in Arabic
--   P_TITLE_EN: Ticket title in English
--   P_DESCRIPTION: Detailed ticket description
--   P_ASSIGNEE_ID: Assignee user ID (optional)
--   P_TICKET_TYPE_ID: Ticket type ID
--   P_TICKET_PRIORITY_ID: Priority ID
--   P_TICKET_CATEGORY_ID: Category ID (optional)
--   P_UPDATE_USER: User updating the ticket
-- Requirements: 1.12, 17.1-17.3
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_UPDATE (
    P_ROW_ID IN NUMBER,
    P_TITLE_AR IN NVARCHAR2,
    P_TITLE_EN IN NVARCHAR2,
    P_DESCRIPTION IN NCLOB,
    P_ASSIGNEE_ID IN NUMBER,
    P_TICKET_TYPE_ID IN NUMBER,
    P_TICKET_PRIORITY_ID IN NUMBER,
    P_TICKET_CATEGORY_ID IN NUMBER,
    P_UPDATE_USER IN NVARCHAR2
)
AS
    V_SLA_HOURS NUMBER(10,2);
    V_EXPECTED_DATE DATE;
    V_OLD_PRIORITY_ID NUMBER;
    V_OLD_ASSIGNEE_ID NUMBER;
BEGIN
    -- Get current values for audit trail
    SELECT TICKET_PRIORITY_ID, ASSIGNEE_ID 
    INTO V_OLD_PRIORITY_ID, V_OLD_ASSIGNEE_ID
    FROM SYS_REQUEST_TICKET
    WHERE ROW_ID = P_ROW_ID;
    
    -- Recalculate SLA if priority changed
    IF V_OLD_PRIORITY_ID != P_TICKET_PRIORITY_ID THEN
        -- Get SLA target hours from new priority
        SELECT SLA_TARGET_HOURS INTO V_SLA_HOURS
        FROM SYS_TICKET_PRIORITY
        WHERE ROW_ID = P_TICKET_PRIORITY_ID AND IS_ACTIVE = 'Y';
        
        -- Check if ticket type has specific SLA override
        BEGIN
            SELECT SLA_TARGET_HOURS INTO V_SLA_HOURS
            FROM SYS_TICKET_TYPE
            WHERE ROW_ID = P_TICKET_TYPE_ID AND IS_ACTIVE = 'Y' AND SLA_TARGET_HOURS > 0;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                -- Use priority SLA (already set above)
                NULL;
        END;
        
        -- Recalculate expected resolution date from current time
        V_EXPECTED_DATE := SYSDATE + (V_SLA_HOURS / 24);
    END IF;
    
    -- Update the ticket record
    UPDATE SYS_REQUEST_TICKET
    SET 
        TITLE_AR = P_TITLE_AR,
        TITLE_EN = P_TITLE_EN,
        DESCRIPTION = P_DESCRIPTION,
        ASSIGNEE_ID = P_ASSIGNEE_ID,
        TICKET_TYPE_ID = P_TICKET_TYPE_ID,
        TICKET_PRIORITY_ID = P_TICKET_PRIORITY_ID,
        TICKET_CATEGORY_ID = P_TICKET_CATEGORY_ID,
        EXPECTED_RESOLUTION_DATE = COALESCE(V_EXPECTED_DATE, EXPECTED_RESOLUTION_DATE),
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20402, 'No ticket found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20403, 'Error updating ticket: ' || SQLERRM);
END SP_SYS_REQUEST_TICKET_UPDATE;
/

-- =============================================
-- Procedure: SP_SYS_REQUEST_TICKET_SELECT_ALL
-- Description: Retrieves tickets with filtering and pagination
-- Parameters:
--   P_COMPANY_ID: Filter by company (optional, 0 = all)
--   P_BRANCH_ID: Filter by branch (optional, 0 = all)
--   P_ASSIGNEE_ID: Filter by assignee (optional, 0 = all)
--   P_STATUS_ID: Filter by status (optional, 0 = all)
--   P_PRIORITY_ID: Filter by priority (optional, 0 = all)
--   P_TYPE_ID: Filter by type (optional, 0 = all)
--   P_SEARCH_TERM: Search in titles and description (optional)
--   P_PAGE_NUMBER: Page number for pagination (1-based)
--   P_PAGE_SIZE: Number of records per page
--   P_SORT_BY: Sort column (CREATION_DATE, PRIORITY_LEVEL, STATUS, etc.)
--   P_SORT_DIRECTION: Sort direction (ASC or DESC)
-- Returns: SYS_REFCURSOR with filtered and paginated tickets
-- Requirements: 8.1-8.12, 16.1-16.2
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_SELECT_ALL (
    P_COMPANY_ID IN NUMBER DEFAULT 0,
    P_BRANCH_ID IN NUMBER DEFAULT 0,
    P_ASSIGNEE_ID IN NUMBER DEFAULT 0,
    P_STATUS_ID IN NUMBER DEFAULT 0,
    P_PRIORITY_ID IN NUMBER DEFAULT 0,
    P_TYPE_ID IN NUMBER DEFAULT 0,
    P_SEARCH_TERM IN NVARCHAR2 DEFAULT NULL,
    P_PAGE_NUMBER IN NUMBER DEFAULT 1,
    P_PAGE_SIZE IN NUMBER DEFAULT 20,
    P_SORT_BY IN VARCHAR2 DEFAULT 'CREATION_DATE',
    P_SORT_DIRECTION IN VARCHAR2 DEFAULT 'DESC',
    P_RESULT_CURSOR OUT SYS_REFCURSOR,
    P_TOTAL_COUNT OUT NUMBER
)
AS
    V_SQL NCLOB;
    V_WHERE_CLAUSE NCLOB := '';
    V_ORDER_CLAUSE NVARCHAR2(200);
    V_OFFSET NUMBER;
BEGIN
    -- Calculate offset for pagination
    V_OFFSET := (P_PAGE_NUMBER - 1) * P_PAGE_SIZE;
    
    -- Build WHERE clause based on filters
    V_WHERE_CLAUSE := 'WHERE t.IS_ACTIVE = ''Y''';
    
    IF P_COMPANY_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.COMPANY_ID = ' || P_COMPANY_ID;
    END IF;
    
    IF P_BRANCH_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.BRANCH_ID = ' || P_BRANCH_ID;
    END IF;
    
    IF P_ASSIGNEE_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.ASSIGNEE_ID = ' || P_ASSIGNEE_ID;
    END IF;
    
    IF P_STATUS_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.TICKET_STATUS_ID = ' || P_STATUS_ID;
    END IF;
    
    IF P_PRIORITY_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.TICKET_PRIORITY_ID = ' || P_PRIORITY_ID;
    END IF;
    
    IF P_TYPE_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.TICKET_TYPE_ID = ' || P_TYPE_ID;
    END IF;
    
    IF P_SEARCH_TERM IS NOT NULL THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND (UPPER(t.TITLE_AR) LIKE UPPER(''%' || P_SEARCH_TERM || '%'') OR UPPER(t.TITLE_EN) LIKE UPPER(''%' || P_SEARCH_TERM || '%'') OR UPPER(t.DESCRIPTION) LIKE UPPER(''%' || P_SEARCH_TERM || '%''))';
    END IF;
    
    -- Build ORDER BY clause
    V_ORDER_CLAUSE := 'ORDER BY ';
    CASE UPPER(P_SORT_BY)
        WHEN 'CREATION_DATE' THEN V_ORDER_CLAUSE := V_ORDER_CLAUSE || 't.CREATION_DATE';
        WHEN 'PRIORITY_LEVEL' THEN V_ORDER_CLAUSE := V_ORDER_CLAUSE || 'pr.PRIORITY_LEVEL';
        WHEN 'STATUS' THEN V_ORDER_CLAUSE := V_ORDER_CLAUSE || 'st.DISPLAY_ORDER';
        WHEN 'TITLE' THEN V_ORDER_CLAUSE := V_ORDER_CLAUSE || 't.TITLE_EN';
        WHEN 'EXPECTED_DATE' THEN V_ORDER_CLAUSE := V_ORDER_CLAUSE || 't.EXPECTED_RESOLUTION_DATE';
        ELSE V_ORDER_CLAUSE := V_ORDER_CLAUSE || 't.CREATION_DATE';
    END CASE;
    
    V_ORDER_CLAUSE := V_ORDER_CLAUSE || ' ' || UPPER(P_SORT_DIRECTION);
    
    -- Get total count for pagination
    V_SQL := 'SELECT COUNT(*) FROM SYS_REQUEST_TICKET t ' || V_WHERE_CLAUSE;
    EXECUTE IMMEDIATE V_SQL INTO P_TOTAL_COUNT;
    
    -- Build main query with pagination
    V_SQL := 'SELECT * FROM (
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
            t.ASSIGNEE_ID,
            ass.ROW_DESC_E AS ASSIGNEE_NAME,
            t.TICKET_TYPE_ID,
            tt.TYPE_NAME_EN AS TYPE_NAME,
            t.TICKET_STATUS_ID,
            st.STATUS_NAME_EN AS STATUS_NAME,
            st.STATUS_CODE,
            t.TICKET_PRIORITY_ID,
            pr.PRIORITY_NAME_EN AS PRIORITY_NAME,
            pr.PRIORITY_LEVEL,
            t.TICKET_CATEGORY_ID,
            cat.CATEGORY_NAME_EN AS CATEGORY_NAME,
            t.EXPECTED_RESOLUTION_DATE,
            t.ACTUAL_RESOLUTION_DATE,
            t.IS_ACTIVE,
            t.CREATION_USER,
            t.CREATION_DATE,
            t.UPDATE_USER,
            t.UPDATE_DATE,
            CASE 
                WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL THEN ''Resolved''
                WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE THEN ''Overdue''
                WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE + (pr.ESCALATION_THRESHOLD_HOURS / 24) THEN ''At Risk''
                ELSE ''On Time''
            END AS SLA_STATUS,
            ROW_NUMBER() OVER (' || V_ORDER_CLAUSE || ') AS RN
        FROM SYS_REQUEST_TICKET t
        LEFT JOIN SYS_COMPANY c ON t.COMPANY_ID = c.ROW_ID
        LEFT JOIN SYS_BRANCH b ON t.BRANCH_ID = b.ROW_ID
        LEFT JOIN SYS_USERS req ON t.REQUESTER_ID = req.ROW_ID
        LEFT JOIN SYS_USERS ass ON t.ASSIGNEE_ID = ass.ROW_ID
        LEFT JOIN SYS_TICKET_TYPE tt ON t.TICKET_TYPE_ID = tt.ROW_ID
        LEFT JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
        LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
        LEFT JOIN SYS_TICKET_CATEGORY cat ON t.TICKET_CATEGORY_ID = cat.ROW_ID
        ' || V_WHERE_CLAUSE || '
    ) WHERE RN > ' || V_OFFSET || ' AND RN <= ' || (V_OFFSET + P_PAGE_SIZE);
    
    OPEN P_RESULT_CURSOR FOR V_SQL;
    
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20404, 'Error retrieving tickets: ' || SQLERRM);
END SP_SYS_REQUEST_TICKET_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_REQUEST_TICKET_SELECT_BY_ID
-- Description: Retrieves a specific ticket by ID with joins
-- Parameters:
--   P_ROW_ID: The ticket ID to retrieve
-- Returns: SYS_REFCURSOR with the matching ticket and related data
-- Requirements: 11.2, 13.2
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
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
        CASE 
            WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL THEN 'Resolved'
            WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE THEN 'Overdue'
            WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE + (pr.ESCALATION_THRESHOLD_HOURS / 24) THEN 'At Risk'
            ELSE 'On Time'
        END AS SLA_STATUS,
        CASE 
            WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL THEN 
                ROUND((t.ACTUAL_RESOLUTION_DATE - t.CREATION_DATE) * 24, 2)
            ELSE 
                ROUND((SYSDATE - t.CREATION_DATE) * 24, 2)
        END AS ELAPSED_HOURS
    FROM SYS_REQUEST_TICKET t
    LEFT JOIN SYS_COMPANY c ON t.COMPANY_ID = c.ROW_ID
    LEFT JOIN SYS_BRANCH b ON t.BRANCH_ID = b.ROW_ID
    LEFT JOIN SYS_USERS req ON t.REQUESTER_ID = req.ROW_ID
    LEFT JOIN SYS_USERS ass ON t.ASSIGNEE_ID = ass.ROW_ID
    LEFT JOIN SYS_TICKET_TYPE tt ON t.TICKET_TYPE_ID = tt.ROW_ID
    LEFT JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
    LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
    LEFT JOIN SYS_TICKET_CATEGORY cat ON t.TICKET_CATEGORY_ID = cat.ROW_ID
    WHERE t.ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20405, 'Error retrieving ticket by ID: ' || SQLERRM);
END SP_SYS_REQUEST_TICKET_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_REQUEST_TICKET_UPDATE_STATUS
-- Description: Updates ticket status with workflow validation and audit trail
-- Parameters:
--   P_ROW_ID: Ticket ID to update
--   P_NEW_STATUS_ID: New status ID
--   P_STATUS_CHANGE_REASON: Reason for status change (optional)
--   P_UPDATE_USER: User updating the status
-- Requirements: 3.1-3.12, 17.2
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_UPDATE_STATUS (
    P_ROW_ID IN NUMBER,
    P_NEW_STATUS_ID IN NUMBER,
    P_STATUS_CHANGE_REASON IN NVARCHAR2,
    P_UPDATE_USER IN NVARCHAR2
)
AS
    V_CURRENT_STATUS_ID NUMBER;
    V_CURRENT_STATUS_CODE VARCHAR2(20);
    V_NEW_STATUS_CODE VARCHAR2(20);
    V_IS_FINAL_STATUS CHAR(1);
    V_RESOLVED_STATUS_ID NUMBER;
BEGIN
    -- Get current status
    SELECT TICKET_STATUS_ID INTO V_CURRENT_STATUS_ID
    FROM SYS_REQUEST_TICKET
    WHERE ROW_ID = P_ROW_ID;
    
    -- Get status codes for validation
    SELECT STATUS_CODE INTO V_CURRENT_STATUS_CODE
    FROM SYS_TICKET_STATUS
    WHERE ROW_ID = V_CURRENT_STATUS_ID;
    
    SELECT STATUS_CODE, IS_FINAL_STATUS INTO V_NEW_STATUS_CODE, V_IS_FINAL_STATUS
    FROM SYS_TICKET_STATUS
    WHERE ROW_ID = P_NEW_STATUS_ID;
    
    -- Validate status transition rules
    -- Cannot reopen closed or cancelled tickets
    IF V_CURRENT_STATUS_CODE IN ('CLOSED', 'CANCELLED') THEN
        RAISE_APPLICATION_ERROR(-20406, 'Cannot change status of closed or cancelled tickets');
    END IF;
    
    -- Get Resolved status ID for resolution date logic
    SELECT ROW_ID INTO V_RESOLVED_STATUS_ID
    FROM SYS_TICKET_STATUS
    WHERE STATUS_CODE = 'RESOLVED' AND IS_ACTIVE = 'Y';
    
    -- Update the ticket status
    UPDATE SYS_REQUEST_TICKET
    SET 
        TICKET_STATUS_ID = P_NEW_STATUS_ID,
        ACTUAL_RESOLUTION_DATE = CASE 
            WHEN P_NEW_STATUS_ID = V_RESOLVED_STATUS_ID THEN SYSDATE
            WHEN V_IS_FINAL_STATUS = 'Y' THEN COALESCE(ACTUAL_RESOLUTION_DATE, SYSDATE)
            ELSE ACTUAL_RESOLUTION_DATE
        END,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20407, 'No ticket found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20408, 'Error updating ticket status: ' || SQLERRM);
END SP_SYS_REQUEST_TICKET_UPDATE_STATUS;
/

-- =============================================
-- Procedure: SP_SYS_REQUEST_TICKET_ASSIGN
-- Description: Assigns or reassigns a ticket to a user
-- Parameters:
--   P_ROW_ID: Ticket ID to assign
--   P_ASSIGNEE_ID: User ID to assign to (must be admin user)
--   P_ASSIGNMENT_REASON: Reason for assignment (optional)
--   P_UPDATE_USER: User performing the assignment
-- Requirements: 5.1-5.10, 13.6
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_ASSIGN (
    P_ROW_ID IN NUMBER,
    P_ASSIGNEE_ID IN NUMBER,
    P_ASSIGNMENT_REASON IN NVARCHAR2,
    P_UPDATE_USER IN NVARCHAR2
)
AS
    V_IS_ADMIN CHAR(1);
    V_IS_ACTIVE CHAR(1);
    V_IN_PROGRESS_STATUS_ID NUMBER;
BEGIN
    -- Validate assignee is an active admin user
    IF P_ASSIGNEE_ID IS NOT NULL THEN
        SELECT IS_ADMIN, IS_ACTIVE INTO V_IS_ADMIN, V_IS_ACTIVE
        FROM SYS_USERS
        WHERE ROW_ID = P_ASSIGNEE_ID;
        
        IF V_IS_ACTIVE != '1' THEN
            RAISE_APPLICATION_ERROR(-20409, 'Cannot assign ticket to inactive user');
        END IF;
        
        IF V_IS_ADMIN != '1' THEN
            RAISE_APPLICATION_ERROR(-20410, 'Cannot assign ticket to non-admin user');
        END IF;
    END IF;
    
    -- Get In Progress status ID for auto status update
    SELECT ROW_ID INTO V_IN_PROGRESS_STATUS_ID
    FROM SYS_TICKET_STATUS
    WHERE STATUS_CODE = 'IN_PROGRESS' AND IS_ACTIVE = 'Y';
    
    -- Update the ticket assignment
    UPDATE SYS_REQUEST_TICKET
    SET 
        ASSIGNEE_ID = P_ASSIGNEE_ID,
        TICKET_STATUS_ID = CASE 
            WHEN P_ASSIGNEE_ID IS NOT NULL AND TICKET_STATUS_ID = (SELECT ROW_ID FROM SYS_TICKET_STATUS WHERE STATUS_CODE = 'OPEN')
            THEN V_IN_PROGRESS_STATUS_ID
            ELSE TICKET_STATUS_ID
        END,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20411, 'No ticket found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20412, 'Error assigning ticket: ' || SQLERRM);
END SP_SYS_REQUEST_TICKET_ASSIGN;
/

-- =============================================
-- Procedure: SP_SYS_REQUEST_TICKET_DELETE
-- Description: Soft deletes a ticket by setting IS_ACTIVE to 'N'
-- Parameters:
--   P_ROW_ID: The ticket ID to delete
--   P_DELETE_USER: User performing the deletion
-- Requirements: 11.5, 13.1
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_DELETE (
    P_ROW_ID IN NUMBER,
    P_DELETE_USER IN NVARCHAR2
)
AS
BEGIN
    -- Soft delete by setting IS_ACTIVE to 'N'
    UPDATE SYS_REQUEST_TICKET
    SET 
        IS_ACTIVE = 'N',
        UPDATE_USER = P_DELETE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20413, 'No ticket found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20414, 'Error deleting ticket: ' || SQLERRM);
END SP_SYS_REQUEST_TICKET_DELETE;
/

-- =============================================
-- Verification: Display all created procedures
-- =============================================
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_REQUEST_TICKET_INSERT',
    'SP_SYS_REQUEST_TICKET_UPDATE',
    'SP_SYS_REQUEST_TICKET_SELECT_ALL',
    'SP_SYS_REQUEST_TICKET_SELECT_BY_ID',
    'SP_SYS_REQUEST_TICKET_UPDATE_STATUS',
    'SP_SYS_REQUEST_TICKET_ASSIGN',
    'SP_SYS_REQUEST_TICKET_DELETE'
)
ORDER BY object_name;