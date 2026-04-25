-- =============================================
-- Company Request Tickets System - Support Procedures Test Script
-- Description: Test script to verify all ticket support procedures work correctly
-- Requirements: Task 1.3 verification
-- =============================================

-- =============================================
-- Test 1: Verify all procedures were created successfully
-- =============================================
PROMPT Testing procedure creation...

SELECT 
    object_name,
    object_type,
    status,
    CASE 
        WHEN status = 'VALID' THEN 'PASS'
        ELSE 'FAIL'
    END AS test_result
FROM user_objects
WHERE object_name IN (
    'SP_SYS_TICKET_COMMENT_INSERT',
    'SP_SYS_TICKET_COMMENT_SELECT_BY_TICKET',
    'SP_SYS_TICKET_ATTACHMENT_INSERT',
    'SP_SYS_TICKET_ATTACHMENT_SELECT_BY_TICKET',
    'SP_SYS_TICKET_ATTACHMENT_SELECT_BY_ID',
    'SP_SYS_TICKET_ATTACHMENT_DELETE',
    'SP_SYS_TICKET_TYPE_SELECT_ALL',
    'SP_SYS_TICKET_TYPE_SELECT_BY_ID',
    'SP_SYS_TICKET_TYPE_INSERT',
    'SP_SYS_TICKET_TYPE_UPDATE',
    'SP_SYS_TICKET_TYPE_DELETE',
    'SP_SYS_TICKET_STATUS_SELECT_ALL',
    'SP_SYS_TICKET_PRIORITY_SELECT_ALL',
    'SP_SYS_TICKET_CATEGORY_SELECT_ALL',
    'SP_SYS_TICKET_REPORTS_VOLUME',
    'SP_SYS_TICKET_REPORTS_SLA_COMPLIANCE',
    'SP_SYS_TICKET_REPORTS_WORKLOAD',
    'SP_SYS_TICKET_REPORTS_AGING',
    'SP_SYS_TICKET_REPORTS_TRENDS',
    'SP_SYS_TICKET_SEED_DATA_INSERT',
    'SP_SYS_TICKET_SYSTEM_STATS'
)
ORDER BY object_name;

-- =============================================
-- Test 2: Test lookup data procedures
-- =============================================
PROMPT Testing lookup data procedures...

-- Test ticket types selection
DECLARE
    v_cursor SYS_REFCURSOR;
    v_count NUMBER := 0;
BEGIN
    SP_SYS_TICKET_TYPE_SELECT_ALL(v_cursor);
    
    LOOP
        FETCH v_cursor BULK COLLECT INTO v_count LIMIT 1;
        EXIT WHEN v_cursor%NOTFOUND;
    END LOOP;
    CLOSE v_cursor;
    
    DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_TYPE_SELECT_ALL: PASS');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_TYPE_SELECT_ALL: FAIL - ' || SQLERRM);
END;
/

-- Test ticket statuses selection
DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_TICKET_STATUS_SELECT_ALL(v_cursor);
    CLOSE v_cursor;
    DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_STATUS_SELECT_ALL: PASS');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_STATUS_SELECT_ALL: FAIL - ' || SQLERRM);
END;
/

-- Test ticket priorities selection
DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_TICKET_PRIORITY_SELECT_ALL(v_cursor);
    CLOSE v_cursor;
    DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_PRIORITY_SELECT_ALL: PASS');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_PRIORITY_SELECT_ALL: FAIL - ' || SQLERRM);
END;
/

-- Test ticket categories selection
DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_TICKET_CATEGORY_SELECT_ALL(v_cursor);
    CLOSE v_cursor;
    DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_CATEGORY_SELECT_ALL: PASS');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_CATEGORY_SELECT_ALL: FAIL - ' || SQLERRM);
END;
/

-- =============================================
-- Test 3: Test ticket type CRUD operations
-- =============================================
PROMPT Testing ticket type CRUD operations...

DECLARE
    v_new_id NUMBER;
    v_cursor SYS_REFCURSOR;
    v_test_passed BOOLEAN := TRUE;
BEGIN
    -- Test INSERT
    BEGIN
        SP_SYS_TICKET_TYPE_INSERT(
            P_TYPE_NAME_AR => 'اختبار',
            P_TYPE_NAME_EN => 'Test Type',
            P_DESCRIPTION_AR => 'نوع تذكرة للاختبار',
            P_DESCRIPTION_EN => 'Test ticket type',
            P_DEFAULT_PRIORITY_ID => (SELECT ROW_ID FROM SYS_TICKET_PRIORITY WHERE PRIORITY_LEVEL = 3 AND ROWNUM = 1),
            P_SLA_TARGET_HOURS => 24,
            P_CREATION_USER => 'test_user',
            P_NEW_ID => v_new_id
        );
        DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_TYPE_INSERT: PASS - ID: ' || v_new_id);
    EXCEPTION
        WHEN OTHERS THEN
            DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_TYPE_INSERT: FAIL - ' || SQLERRM);
            v_test_passed := FALSE;
    END;
    
    -- Test SELECT_BY_ID
    IF v_test_passed THEN
        BEGIN
            SP_SYS_TICKET_TYPE_SELECT_BY_ID(v_new_id, v_cursor);
            CLOSE v_cursor;
            DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_TYPE_SELECT_BY_ID: PASS');
        EXCEPTION
            WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_TYPE_SELECT_BY_ID: FAIL - ' || SQLERRM);
                v_test_passed := FALSE;
        END;
    END IF;
    
    -- Test UPDATE
    IF v_test_passed THEN
        BEGIN
            SP_SYS_TICKET_TYPE_UPDATE(
                P_ROW_ID => v_new_id,
                P_TYPE_NAME_AR => 'اختبار محدث',
                P_TYPE_NAME_EN => 'Updated Test Type',
                P_DESCRIPTION_AR => 'نوع تذكرة محدث للاختبار',
                P_DESCRIPTION_EN => 'Updated test ticket type',
                P_DEFAULT_PRIORITY_ID => (SELECT ROW_ID FROM SYS_TICKET_PRIORITY WHERE PRIORITY_LEVEL = 2 AND ROWNUM = 1),
                P_SLA_TARGET_HOURS => 12,
                P_UPDATE_USER => 'test_user'
            );
            DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_TYPE_UPDATE: PASS');
        EXCEPTION
            WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_TYPE_UPDATE: FAIL - ' || SQLERRM);
                v_test_passed := FALSE;
        END;
    END IF;
    
    -- Test DELETE
    IF v_test_passed THEN
        BEGIN
            SP_SYS_TICKET_TYPE_DELETE(
                P_ROW_ID => v_new_id,
                P_DELETE_USER => 'test_user'
            );
            DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_TYPE_DELETE: PASS');
        EXCEPTION
            WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_TYPE_DELETE: FAIL - ' || SQLERRM);
        END;
    END IF;
END;
/

-- =============================================
-- Test 4: Test reporting procedures (basic execution)
-- =============================================
PROMPT Testing reporting procedures...

-- Test volume report
DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_TICKET_REPORTS_VOLUME(
        P_START_DATE => SYSDATE - 30,
        P_END_DATE => SYSDATE,
        P_COMPANY_ID => 0,
        P_TICKET_TYPE_ID => 0,
        P_GROUP_BY => 'DAILY',
        P_RESULT_CURSOR => v_cursor
    );
    CLOSE v_cursor;
    DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_REPORTS_VOLUME: PASS');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_REPORTS_VOLUME: FAIL - ' || SQLERRM);
END;
/

-- Test SLA compliance report
DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_TICKET_REPORTS_SLA_COMPLIANCE(
        P_START_DATE => SYSDATE - 30,
        P_END_DATE => SYSDATE,
        P_COMPANY_ID => 0,
        P_RESULT_CURSOR => v_cursor
    );
    CLOSE v_cursor;
    DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_REPORTS_SLA_COMPLIANCE: PASS');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_REPORTS_SLA_COMPLIANCE: FAIL - ' || SQLERRM);
END;
/

-- Test workload report
DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_TICKET_REPORTS_WORKLOAD(
        P_START_DATE => SYSDATE - 30,
        P_END_DATE => SYSDATE,
        P_COMPANY_ID => 0,
        P_RESULT_CURSOR => v_cursor
    );
    CLOSE v_cursor;
    DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_REPORTS_WORKLOAD: PASS');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_REPORTS_WORKLOAD: FAIL - ' || SQLERRM);
END;
/

-- Test aging report
DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_TICKET_REPORTS_AGING(
        P_COMPANY_ID => 0,
        P_ASSIGNEE_ID => 0,
        P_RESULT_CURSOR => v_cursor
    );
    CLOSE v_cursor;
    DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_REPORTS_AGING: PASS');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_REPORTS_AGING: FAIL - ' || SQLERRM);
END;
/

-- Test trends report
DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_TICKET_REPORTS_TRENDS(
        P_START_DATE => SYSDATE - 7,
        P_END_DATE => SYSDATE,
        P_PERIOD_TYPE => 'DAILY',
        P_RESULT_CURSOR => v_cursor
    );
    CLOSE v_cursor;
    DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_REPORTS_TRENDS: PASS');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_REPORTS_TRENDS: FAIL - ' || SQLERRM);
END;
/

-- =============================================
-- Test 5: Test system statistics procedure
-- =============================================
PROMPT Testing system statistics procedure...

DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_TICKET_SYSTEM_STATS(v_cursor);
    CLOSE v_cursor;
    DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_SYSTEM_STATS: PASS');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('SP_SYS_TICKET_SYSTEM_STATS: FAIL - ' || SQLERRM);
END;
/

-- =============================================
-- Test 6: Verify seed data was inserted
-- =============================================
PROMPT Verifying seed data...

SELECT 'Ticket Types Count: ' || COUNT(*) AS info FROM SYS_TICKET_TYPE WHERE IS_ACTIVE = 'Y';
SELECT 'Ticket Categories Count: ' || COUNT(*) AS info FROM SYS_TICKET_CATEGORY WHERE IS_ACTIVE = 'Y';
SELECT 'Ticket Statuses Count: ' || COUNT(*) AS info FROM SYS_TICKET_STATUS WHERE IS_ACTIVE = 'Y';
SELECT 'Ticket Priorities Count: ' || COUNT(*) AS info FROM SYS_TICKET_PRIORITY WHERE IS_ACTIVE = 'Y';

-- Display new ticket types added by seed data
SELECT 'New Ticket Types:' AS info FROM DUAL;
SELECT TYPE_NAME_EN, SLA_TARGET_HOURS 
FROM SYS_TICKET_TYPE 
WHERE TYPE_NAME_EN IN ('Feature Request', 'Data Request', 'System Maintenance')
AND IS_ACTIVE = 'Y';

-- Display new categories added by seed data
SELECT 'New Categories:' AS info FROM DUAL;
SELECT CATEGORY_NAME_EN, DESCRIPTION_EN 
FROM SYS_TICKET_CATEGORY 
WHERE CATEGORY_NAME_EN IN ('General', 'Training', 'Integration')
AND IS_ACTIVE = 'Y';

-- =============================================
-- Test Summary
-- =============================================
PROMPT Test Summary:
PROMPT All ticket support procedures have been tested.
PROMPT Check the output above for any FAIL messages.
PROMPT If all tests show PASS, the implementation is successful.

-- =============================================
-- Performance Test (Optional)
-- =============================================
PROMPT Running performance test on reporting procedures...

SET TIMING ON;

-- Test volume report performance
DECLARE
    v_cursor SYS_REFCURSOR;
    v_start_time NUMBER;
    v_end_time NUMBER;
BEGIN
    v_start_time := DBMS_UTILITY.GET_TIME;
    
    SP_SYS_TICKET_REPORTS_VOLUME(
        P_START_DATE => SYSDATE - 365,
        P_END_DATE => SYSDATE,
        P_COMPANY_ID => 0,
        P_TICKET_TYPE_ID => 0,
        P_GROUP_BY => 'MONTHLY',
        P_RESULT_CURSOR => v_cursor
    );
    CLOSE v_cursor;
    
    v_end_time := DBMS_UTILITY.GET_TIME;
    DBMS_OUTPUT.PUT_LINE('Volume report (1 year) execution time: ' || 
                        ROUND((v_end_time - v_start_time) / 100, 2) || ' seconds');
END;
/

SET TIMING OFF;

PROMPT Performance test completed.
PROMPT 
PROMPT =============================================
PROMPT TICKET SUPPORT PROCEDURES TEST COMPLETED
PROMPT =============================================