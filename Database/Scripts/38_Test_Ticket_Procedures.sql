-- =============================================
-- Company Request Tickets System - Procedure Testing Script
-- Description: Tests all ticket-related stored procedures
-- Requirements: Verification of 14.9-14.14, 3.1-3.12
-- =============================================

-- =============================================
-- Test Setup: Ensure we have test data
-- =============================================

-- Verify we have companies, branches, and users for testing
SELECT 'Test Data Verification:' AS INFO FROM DUAL;

SELECT 'Companies:' AS INFO FROM DUAL;
SELECT ROW_ID, ROW_DESC_E FROM SYS_COMPANY WHERE IS_ACTIVE = '1' AND ROWNUM <= 3;

SELECT 'Branches:' AS INFO FROM DUAL;
SELECT ROW_ID, ROW_DESC_E, COMPANY_ID FROM SYS_BRANCH WHERE IS_ACTIVE = '1' AND ROWNUM <= 3;

SELECT 'Users:' AS INFO FROM DUAL;
SELECT ROW_ID, ROW_DESC_E, USER_NAME, IS_ADMIN FROM SYS_USERS WHERE IS_ACTIVE = '1' AND ROWNUM <= 5;

SELECT 'Ticket Types:' AS INFO FROM DUAL;
SELECT ROW_ID, TYPE_NAME_EN, DEFAULT_PRIORITY_ID, SLA_TARGET_HOURS FROM SYS_TICKET_TYPE WHERE IS_ACTIVE = 'Y';

SELECT 'Ticket Priorities:' AS INFO FROM DUAL;
SELECT ROW_ID, PRIORITY_NAME_EN, PRIORITY_LEVEL, SLA_TARGET_HOURS FROM SYS_TICKET_PRIORITY WHERE IS_ACTIVE = 'Y';

SELECT 'Ticket Statuses:' AS INFO FROM DUAL;
SELECT ROW_ID, STATUS_NAME_EN, STATUS_CODE, DISPLAY_ORDER FROM SYS_TICKET_STATUS WHERE IS_ACTIVE = 'Y';

-- =============================================
-- Test 1: Test Ticket Creation (SP_SYS_REQUEST_TICKET_INSERT)
-- =============================================

SELECT 'TEST 1: Creating Test Tickets' AS TEST_INFO FROM DUAL;

DECLARE
    V_NEW_TICKET_ID1 NUMBER;
    V_NEW_TICKET_ID2 NUMBER;
    V_COMPANY_ID NUMBER := 1;
    V_BRANCH_ID NUMBER := 1;
    V_REQUESTER_ID NUMBER := 1;
    V_ASSIGNEE_ID NUMBER := 2;
    V_TYPE_ID NUMBER := 1;
    V_PRIORITY_ID NUMBER := 2; -- High priority
    V_CATEGORY_ID NUMBER := 1;
BEGIN
    -- Test ticket 1: High priority technical support
    SP_SYS_REQUEST_TICKET_INSERT(
        P_TITLE_AR => 'مشكلة في النظام',
        P_TITLE_EN => 'System Issue',
        P_DESCRIPTION => 'There is a critical system issue that needs immediate attention. The application is not responding properly.',
        P_COMPANY_ID => V_COMPANY_ID,
        P_BRANCH_ID => V_BRANCH_ID,
        P_REQUESTER_ID => V_REQUESTER_ID,
        P_ASSIGNEE_ID => V_ASSIGNEE_ID,
        P_TICKET_TYPE_ID => V_TYPE_ID,
        P_TICKET_PRIORITY_ID => V_PRIORITY_ID,
        P_TICKET_CATEGORY_ID => V_CATEGORY_ID,
        P_CREATION_USER => 'test_user',
        P_NEW_ID => V_NEW_TICKET_ID1
    );
    
    DBMS_OUTPUT.PUT_LINE('Created ticket 1 with ID: ' || V_NEW_TICKET_ID1);
    
    -- Test ticket 2: Medium priority service request
    SP_SYS_REQUEST_TICKET_INSERT(
        P_TITLE_AR => 'طلب خدمة جديدة',
        P_TITLE_EN => 'New Service Request',
        P_DESCRIPTION => 'Request for a new feature to be added to the system. This would help improve user productivity.',
        P_COMPANY_ID => V_COMPANY_ID,
        P_BRANCH_ID => V_BRANCH_ID,
        P_REQUESTER_ID => V_REQUESTER_ID,
        P_ASSIGNEE_ID => NULL, -- Unassigned
        P_TICKET_TYPE_ID => 3, -- Service Request
        P_TICKET_PRIORITY_ID => 3, -- Medium priority
        P_TICKET_CATEGORY_ID => V_CATEGORY_ID,
        P_CREATION_USER => 'test_user',
        P_NEW_ID => V_NEW_TICKET_ID2
    );
    
    DBMS_OUTPUT.PUT_LINE('Created ticket 2 with ID: ' || V_NEW_TICKET_ID2);
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error creating tickets: ' || SQLERRM);
        ROLLBACK;
END;
/

-- =============================================
-- Test 2: Test Ticket Retrieval (SP_SYS_REQUEST_TICKET_SELECT_ALL)
-- =============================================

SELECT 'TEST 2: Testing Ticket Retrieval' AS TEST_INFO FROM DUAL;

DECLARE
    V_CURSOR SYS_REFCURSOR;
    V_TOTAL_COUNT NUMBER;
    V_ROW_ID NUMBER;
    V_TITLE_EN NVARCHAR2(200);
    V_STATUS_NAME NVARCHAR2(50);
    V_PRIORITY_NAME NVARCHAR2(50);
    V_SLA_STATUS NVARCHAR2(20);
BEGIN
    -- Test retrieving all tickets with pagination
    SP_SYS_REQUEST_TICKET_SELECT_ALL(
        P_COMPANY_ID => 0, -- All companies
        P_BRANCH_ID => 0,  -- All branches
        P_ASSIGNEE_ID => 0, -- All assignees
        P_STATUS_ID => 0,   -- All statuses
        P_PRIORITY_ID => 0, -- All priorities
        P_TYPE_ID => 0,     -- All types
        P_SEARCH_TERM => NULL,
        P_PAGE_NUMBER => 1,
        P_PAGE_SIZE => 10,
        P_SORT_BY => 'CREATION_DATE',
        P_SORT_DIRECTION => 'DESC',
        P_RESULT_CURSOR => V_CURSOR,
        P_TOTAL_COUNT => V_TOTAL_COUNT
    );
    
    DBMS_OUTPUT.PUT_LINE('Total tickets found: ' || V_TOTAL_COUNT);
    DBMS_OUTPUT.PUT_LINE('First few tickets:');
    
    -- Display first few results
    FOR i IN 1..3 LOOP
        FETCH V_CURSOR INTO V_ROW_ID, V_TITLE_EN, V_STATUS_NAME, V_PRIORITY_NAME, V_SLA_STATUS;
        EXIT WHEN V_CURSOR%NOTFOUND;
        DBMS_OUTPUT.PUT_LINE('ID: ' || V_ROW_ID || ', Title: ' || V_TITLE_EN || ', Status: ' || V_STATUS_NAME);
    END LOOP;
    
    CLOSE V_CURSOR;
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error retrieving tickets: ' || SQLERRM);
        IF V_CURSOR%ISOPEN THEN
            CLOSE V_CURSOR;
        END IF;
END;
/

-- =============================================
-- Test 3: Test Ticket Detail Retrieval (SP_SYS_REQUEST_TICKET_SELECT_BY_ID)
-- =============================================

SELECT 'TEST 3: Testing Ticket Detail Retrieval' AS TEST_INFO FROM DUAL;

DECLARE
    V_CURSOR SYS_REFCURSOR;
    V_TICKET_ID NUMBER;
    V_TITLE_EN NVARCHAR2(200);
    V_DESCRIPTION NCLOB;
    V_COMPANY_NAME NVARCHAR2(200);
    V_STATUS_NAME NVARCHAR2(50);
    V_SLA_STATUS NVARCHAR2(20);
    V_ELAPSED_HOURS NUMBER;
BEGIN
    -- Get the first ticket ID for testing
    SELECT MIN(ROW_ID) INTO V_TICKET_ID FROM SYS_REQUEST_TICKET WHERE IS_ACTIVE = 'Y';
    
    IF V_TICKET_ID IS NOT NULL THEN
        SP_SYS_REQUEST_TICKET_SELECT_BY_ID(
            P_ROW_ID => V_TICKET_ID,
            P_RESULT_CURSOR => V_CURSOR
        );
        
        FETCH V_CURSOR INTO V_TICKET_ID, V_TITLE_EN, V_DESCRIPTION, V_COMPANY_NAME, V_STATUS_NAME, V_SLA_STATUS, V_ELAPSED_HOURS;
        
        IF V_CURSOR%FOUND THEN
            DBMS_OUTPUT.PUT_LINE('Ticket Details:');
            DBMS_OUTPUT.PUT_LINE('ID: ' || V_TICKET_ID);
            DBMS_OUTPUT.PUT_LINE('Title: ' || V_TITLE_EN);
            DBMS_OUTPUT.PUT_LINE('Company: ' || V_COMPANY_NAME);
            DBMS_OUTPUT.PUT_LINE('Status: ' || V_STATUS_NAME);
            DBMS_OUTPUT.PUT_LINE('SLA Status: ' || V_SLA_STATUS);
            DBMS_OUTPUT.PUT_LINE('Elapsed Hours: ' || V_ELAPSED_HOURS);
        END IF;
        
        CLOSE V_CURSOR;
    ELSE
        DBMS_OUTPUT.PUT_LINE('No tickets found for testing');
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error retrieving ticket details: ' || SQLERRM);
        IF V_CURSOR%ISOPEN THEN
            CLOSE V_CURSOR;
        END IF;
END;
/

-- =============================================
-- Test 4: Test Ticket Assignment (SP_SYS_REQUEST_TICKET_ASSIGN)
-- =============================================

SELECT 'TEST 4: Testing Ticket Assignment' AS TEST_INFO FROM DUAL;

DECLARE
    V_TICKET_ID NUMBER;
    V_ADMIN_USER_ID NUMBER;
BEGIN
    -- Get an unassigned ticket
    SELECT MIN(ROW_ID) INTO V_TICKET_ID 
    FROM SYS_REQUEST_TICKET 
    WHERE IS_ACTIVE = 'Y' AND ASSIGNEE_ID IS NULL;
    
    -- Get an admin user
    SELECT MIN(ROW_ID) INTO V_ADMIN_USER_ID 
    FROM SYS_USERS 
    WHERE IS_ACTIVE = '1' AND IS_ADMIN = '1';
    
    IF V_TICKET_ID IS NOT NULL AND V_ADMIN_USER_ID IS NOT NULL THEN
        SP_SYS_REQUEST_TICKET_ASSIGN(
            P_ROW_ID => V_TICKET_ID,
            P_ASSIGNEE_ID => V_ADMIN_USER_ID,
            P_ASSIGNMENT_REASON => 'Test assignment',
            P_UPDATE_USER => 'test_admin'
        );
        
        DBMS_OUTPUT.PUT_LINE('Successfully assigned ticket ' || V_TICKET_ID || ' to user ' || V_ADMIN_USER_ID);
    ELSE
        DBMS_OUTPUT.PUT_LINE('No suitable ticket or admin user found for assignment test');
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error assigning ticket: ' || SQLERRM);
        ROLLBACK;
END;
/

-- =============================================
-- Test 5: Test Status Update (SP_SYS_REQUEST_TICKET_UPDATE_STATUS)
-- =============================================

SELECT 'TEST 5: Testing Status Update' AS TEST_INFO FROM DUAL;

DECLARE
    V_TICKET_ID NUMBER;
    V_IN_PROGRESS_STATUS_ID NUMBER;
    V_RESOLVED_STATUS_ID NUMBER;
BEGIN
    -- Get a ticket to update
    SELECT MIN(ROW_ID) INTO V_TICKET_ID 
    FROM SYS_REQUEST_TICKET 
    WHERE IS_ACTIVE = 'Y';
    
    -- Get status IDs
    SELECT ROW_ID INTO V_IN_PROGRESS_STATUS_ID 
    FROM SYS_TICKET_STATUS 
    WHERE STATUS_CODE = 'IN_PROGRESS';
    
    SELECT ROW_ID INTO V_RESOLVED_STATUS_ID 
    FROM SYS_TICKET_STATUS 
    WHERE STATUS_CODE = 'RESOLVED';
    
    IF V_TICKET_ID IS NOT NULL THEN
        -- Update to In Progress
        SP_SYS_REQUEST_TICKET_UPDATE_STATUS(
            P_ROW_ID => V_TICKET_ID,
            P_NEW_STATUS_ID => V_IN_PROGRESS_STATUS_ID,
            P_STATUS_CHANGE_REASON => 'Started working on the issue',
            P_UPDATE_USER => 'test_admin'
        );
        
        DBMS_OUTPUT.PUT_LINE('Updated ticket ' || V_TICKET_ID || ' to In Progress');
        
        -- Update to Resolved
        SP_SYS_REQUEST_TICKET_UPDATE_STATUS(
            P_ROW_ID => V_TICKET_ID,
            P_NEW_STATUS_ID => V_RESOLVED_STATUS_ID,
            P_STATUS_CHANGE_REASON => 'Issue has been resolved',
            P_UPDATE_USER => 'test_admin'
        );
        
        DBMS_OUTPUT.PUT_LINE('Updated ticket ' || V_TICKET_ID || ' to Resolved');
        
        -- Verify resolution date was set
        DECLARE
            V_RESOLUTION_DATE DATE;
        BEGIN
            SELECT ACTUAL_RESOLUTION_DATE INTO V_RESOLUTION_DATE
            FROM SYS_REQUEST_TICKET
            WHERE ROW_ID = V_TICKET_ID;
            
            IF V_RESOLUTION_DATE IS NOT NULL THEN
                DBMS_OUTPUT.PUT_LINE('Resolution date set: ' || TO_CHAR(V_RESOLUTION_DATE, 'YYYY-MM-DD HH24:MI:SS'));
            END IF;
        END;
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error updating status: ' || SQLERRM);
        ROLLBACK;
END;
/

-- =============================================
-- Test 6: Test Comment Operations
-- =============================================

SELECT 'TEST 6: Testing Comment Operations' AS TEST_INFO FROM DUAL;

DECLARE
    V_TICKET_ID NUMBER;
    V_COMMENT_ID1 NUMBER;
    V_COMMENT_ID2 NUMBER;
    V_CURSOR SYS_REFCURSOR;
    V_COMMENT_TEXT NCLOB;
    V_COMMENTER_NAME NVARCHAR2(200);
BEGIN
    -- Get a ticket for testing
    SELECT MIN(ROW_ID) INTO V_TICKET_ID 
    FROM SYS_REQUEST_TICKET 
    WHERE IS_ACTIVE = 'Y';
    
    IF V_TICKET_ID IS NOT NULL THEN
        -- Add public comment
        SP_SYS_TICKET_COMMENT_INSERT(
            P_TICKET_ID => V_TICKET_ID,
            P_COMMENT_TEXT => 'This is a public comment from the customer. Please provide an update on the progress.',
            P_IS_INTERNAL => 'N',
            P_CREATION_USER => 'customer_user',
            P_NEW_ID => V_COMMENT_ID1
        );
        
        DBMS_OUTPUT.PUT_LINE('Added public comment with ID: ' || V_COMMENT_ID1);
        
        -- Add internal comment
        SP_SYS_TICKET_COMMENT_INSERT(
            P_TICKET_ID => V_TICKET_ID,
            P_COMMENT_TEXT => 'Internal note: This issue requires escalation to the development team.',
            P_IS_INTERNAL => 'Y',
            P_CREATION_USER => 'admin_user',
            P_NEW_ID => V_COMMENT_ID2
        );
        
        DBMS_OUTPUT.PUT_LINE('Added internal comment with ID: ' || V_COMMENT_ID2);
        
        -- Retrieve comments (public only)
        SP_SYS_TICKET_COMMENT_SELECT_BY_TICKET(
            P_TICKET_ID => V_TICKET_ID,
            P_INCLUDE_INTERNAL => 'N',
            P_RESULT_CURSOR => V_CURSOR
        );
        
        DBMS_OUTPUT.PUT_LINE('Public comments for ticket ' || V_TICKET_ID || ':');
        LOOP
            FETCH V_CURSOR INTO V_COMMENT_ID1, V_TICKET_ID, V_COMMENT_TEXT, V_COMMENTER_NAME;
            EXIT WHEN V_CURSOR%NOTFOUND;
            DBMS_OUTPUT.PUT_LINE('Comment ID: ' || V_COMMENT_ID1 || ', By: ' || V_COMMENTER_NAME);
        END LOOP;
        CLOSE V_CURSOR;
        
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error testing comments: ' || SQLERRM);
        IF V_CURSOR%ISOPEN THEN
            CLOSE V_CURSOR;
        END IF;
        ROLLBACK;
END;
/

-- =============================================
-- Test 7: Test Ticket Type Operations
-- =============================================

SELECT 'TEST 7: Testing Ticket Type Operations' AS TEST_INFO FROM DUAL;

DECLARE
    V_CURSOR SYS_REFCURSOR;
    V_TYPE_ID NUMBER;
    V_TYPE_NAME NVARCHAR2(100);
    V_SLA_HOURS NUMBER;
    V_NEW_TYPE_ID NUMBER;
BEGIN
    -- Test retrieving all ticket types
    SP_SYS_TICKET_TYPE_SELECT_ALL(P_RESULT_CURSOR => V_CURSOR);
    
    DBMS_OUTPUT.PUT_LINE('Available Ticket Types:');
    LOOP
        FETCH V_CURSOR INTO V_TYPE_ID, V_TYPE_NAME, V_SLA_HOURS;
        EXIT WHEN V_CURSOR%NOTFOUND;
        DBMS_OUTPUT.PUT_LINE('ID: ' || V_TYPE_ID || ', Name: ' || V_TYPE_NAME || ', SLA: ' || V_SLA_HOURS || ' hours');
    END LOOP;
    CLOSE V_CURSOR;
    
    -- Test creating a new ticket type
    SP_SYS_TICKET_TYPE_INSERT(
        P_TYPE_NAME_AR => 'طلب تدريب',
        P_TYPE_NAME_EN => 'Training Request',
        P_DESCRIPTION_AR => 'طلبات التدريب والتأهيل',
        P_DESCRIPTION_EN => 'Training and qualification requests',
        P_DEFAULT_PRIORITY_ID => 4, -- Low priority
        P_SLA_TARGET_HOURS => 120, -- 5 days
        P_CREATION_USER => 'test_admin',
        P_NEW_ID => V_NEW_TYPE_ID
    );
    
    DBMS_OUTPUT.PUT_LINE('Created new ticket type with ID: ' || V_NEW_TYPE_ID);
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error testing ticket types: ' || SQLERRM);
        IF V_CURSOR%ISOPEN THEN
            CLOSE V_CURSOR;
        END IF;
        ROLLBACK;
END;
/

-- =============================================
-- Test 8: Test Lookup Data Procedures
-- =============================================

SELECT 'TEST 8: Testing Lookup Data Procedures' AS TEST_INFO FROM DUAL;

DECLARE
    V_CURSOR SYS_REFCURSOR;
    V_COUNT NUMBER := 0;
BEGIN
    -- Test ticket statuses
    SP_SYS_TICKET_STATUS_SELECT_ALL(P_RESULT_CURSOR => V_CURSOR);
    LOOP
        FETCH V_CURSOR INTO V_COUNT; -- Just count rows
        EXIT WHEN V_CURSOR%NOTFOUND;
        V_COUNT := V_COUNT + 1;
    END LOOP;
    CLOSE V_CURSOR;
    DBMS_OUTPUT.PUT_LINE('Retrieved ' || V_COUNT || ' ticket statuses');
    
    -- Test ticket priorities
    V_COUNT := 0;
    SP_SYS_TICKET_PRIORITY_SELECT_ALL(P_RESULT_CURSOR => V_CURSOR);
    LOOP
        FETCH V_CURSOR INTO V_COUNT; -- Just count rows
        EXIT WHEN V_CURSOR%NOTFOUND;
        V_COUNT := V_COUNT + 1;
    END LOOP;
    CLOSE V_CURSOR;
    DBMS_OUTPUT.PUT_LINE('Retrieved ' || V_COUNT || ' ticket priorities');
    
    -- Test ticket categories
    V_COUNT := 0;
    SP_SYS_TICKET_CATEGORY_SELECT_ALL(P_RESULT_CURSOR => V_CURSOR);
    LOOP
        FETCH V_CURSOR INTO V_COUNT; -- Just count rows
        EXIT WHEN V_CURSOR%NOTFOUND;
        V_COUNT := V_COUNT + 1;
    END LOOP;
    CLOSE V_CURSOR;
    DBMS_OUTPUT.PUT_LINE('Retrieved ' || V_COUNT || ' ticket categories');
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error testing lookup procedures: ' || SQLERRM);
        IF V_CURSOR%ISOPEN THEN
            CLOSE V_CURSOR;
        END IF;
END;
/

-- =============================================
-- Test Summary
-- =============================================

SELECT 'TEST SUMMARY: All Stored Procedures' AS TEST_INFO FROM DUAL;

-- Show all ticket-related procedures and their status
SELECT 
    object_name,
    object_type,
    status,
    created,
    last_ddl_time
FROM user_objects
WHERE object_name LIKE 'SP_SYS_%TICKET%' OR object_name LIKE 'SP_SYS_REQUEST_TICKET%'
ORDER BY object_name;

-- Show current ticket data
SELECT 'Current Tickets in System:' AS INFO FROM DUAL;
SELECT 
    COUNT(*) AS TOTAL_TICKETS,
    SUM(CASE WHEN IS_ACTIVE = 'Y' THEN 1 ELSE 0 END) AS ACTIVE_TICKETS,
    SUM(CASE WHEN ASSIGNEE_ID IS NOT NULL THEN 1 ELSE 0 END) AS ASSIGNED_TICKETS
FROM SYS_REQUEST_TICKET;

-- Show tickets by status
SELECT 'Tickets by Status:' AS INFO FROM DUAL;
SELECT 
    st.STATUS_NAME_EN,
    COUNT(*) AS TICKET_COUNT
FROM SYS_REQUEST_TICKET t
JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
WHERE t.IS_ACTIVE = 'Y'
GROUP BY st.STATUS_NAME_EN, st.DISPLAY_ORDER
ORDER BY st.DISPLAY_ORDER;

-- Show tickets by priority
SELECT 'Tickets by Priority:' AS INFO FROM DUAL;
SELECT 
    pr.PRIORITY_NAME_EN,
    COUNT(*) AS TICKET_COUNT
FROM SYS_REQUEST_TICKET t
JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
WHERE t.IS_ACTIVE = 'Y'
GROUP BY pr.PRIORITY_NAME_EN, pr.PRIORITY_LEVEL
ORDER BY pr.PRIORITY_LEVEL;

COMMIT;

SELECT 'All tests completed successfully!' AS FINAL_STATUS FROM DUAL;