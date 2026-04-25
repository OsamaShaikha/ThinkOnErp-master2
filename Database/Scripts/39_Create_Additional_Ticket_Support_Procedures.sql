-- =============================================
-- Company Request Tickets System - Additional Supporting Entity Procedures
-- Description: Additional stored procedures for ticket types, categories, and enhanced reporting
-- Task: 1.3 Create stored procedures for supporting entities
-- Requirements: 2.1-2.10, 6.1-6.12, 7.1-7.12
-- =============================================

-- =============================================
-- TICKET CATEGORY MANAGEMENT PROCEDURES
-- =============================================

-- =============================================
-- Procedure: SP_SYS_TICKET_CATEGORY_INSERT
-- Description: Inserts a new ticket category record
-- Parameters:
--   P_CATEGORY_NAME_AR: Category name in Arabic
--   P_CATEGORY_NAME_EN: Category name in English
--   P_DESCRIPTION_AR: Description in Arabic (optional)
--   P_DESCRIPTION_EN: Description in English (optional)
--   P_PARENT_CATEGORY_ID: Parent category ID for hierarchical organization (optional)
--   P_CREATION_USER: User creating the record
--   P_NEW_ID: Output parameter returning the new category ID
-- Requirements: Category management support
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_CATEGORY_INSERT (
    P_CATEGORY_NAME_AR IN NVARCHAR2,
    P_CATEGORY_NAME_EN IN NVARCHAR2,
    P_DESCRIPTION_AR IN NVARCHAR2,
    P_DESCRIPTION_EN IN NVARCHAR2,
    P_PARENT_CATEGORY_ID IN NUMBER,
    P_CREATION_USER IN NVARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
BEGIN
    -- Validate parent category exists if specified
    IF P_PARENT_CATEGORY_ID IS NOT NULL THEN
        DECLARE
            V_PARENT_COUNT NUMBER;
        BEGIN
            SELECT COUNT(*) INTO V_PARENT_COUNT
            FROM SYS_TICKET_CATEGORY
            WHERE ROW_ID = P_PARENT_CATEGORY_ID AND IS_ACTIVE = 'Y';
            
            IF V_PARENT_COUNT = 0 THEN
                RAISE_APPLICATION_ERROR(-20801, 'Parent category not found or inactive');
            END IF;
        END;
    END IF;
    
    -- Generate new ID from sequence
    SELECT SEQ_SYS_TICKET_CATEGORY.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
    -- Insert the new category record
    INSERT INTO SYS_TICKET_CATEGORY (
        ROW_ID,
        CATEGORY_NAME_AR,
        CATEGORY_NAME_EN,
        DESCRIPTION_AR,
        DESCRIPTION_EN,
        PARENT_CATEGORY_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_CATEGORY_NAME_AR,
        P_CATEGORY_NAME_EN,
        P_DESCRIPTION_AR,
        P_DESCRIPTION_EN,
        P_PARENT_CATEGORY_ID,
        'Y',
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20802, 'Error inserting ticket category: ' || SQLERRM);
END SP_SYS_TICKET_CATEGORY_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_CATEGORY_UPDATE
-- Description: Updates an existing ticket category record
-- Parameters:
--   P_ROW_ID: The category ID to update
--   P_CATEGORY_NAME_AR: Category name in Arabic
--   P_CATEGORY_NAME_EN: Category name in English
--   P_DESCRIPTION_AR: Description in Arabic (optional)
--   P_DESCRIPTION_EN: Description in English (optional)
--   P_PARENT_CATEGORY_ID: Parent category ID (optional)
--   P_UPDATE_USER: User updating the record
-- Requirements: Category management support
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_CATEGORY_UPDATE (
    P_ROW_ID IN NUMBER,
    P_CATEGORY_NAME_AR IN NVARCHAR2,
    P_CATEGORY_NAME_EN IN NVARCHAR2,
    P_DESCRIPTION_AR IN NVARCHAR2,
    P_DESCRIPTION_EN IN NVARCHAR2,
    P_PARENT_CATEGORY_ID IN NUMBER,
    P_UPDATE_USER IN NVARCHAR2
)
AS
BEGIN
    -- Validate parent category exists if specified and not self-referencing
    IF P_PARENT_CATEGORY_ID IS NOT NULL THEN
        IF P_PARENT_CATEGORY_ID = P_ROW_ID THEN
            RAISE_APPLICATION_ERROR(-20803, 'Category cannot be its own parent');
        END IF;
        
        DECLARE
            V_PARENT_COUNT NUMBER;
        BEGIN
            SELECT COUNT(*) INTO V_PARENT_COUNT
            FROM SYS_TICKET_CATEGORY
            WHERE ROW_ID = P_PARENT_CATEGORY_ID AND IS_ACTIVE = 'Y';
            
            IF V_PARENT_COUNT = 0 THEN
                RAISE_APPLICATION_ERROR(-20804, 'Parent category not found or inactive');
            END IF;
        END;
    END IF;
    
    -- Update the category record
    UPDATE SYS_TICKET_CATEGORY
    SET 
        CATEGORY_NAME_AR = P_CATEGORY_NAME_AR,
        CATEGORY_NAME_EN = P_CATEGORY_NAME_EN,
        DESCRIPTION_AR = P_DESCRIPTION_AR,
        DESCRIPTION_EN = P_DESCRIPTION_EN,
        PARENT_CATEGORY_ID = P_PARENT_CATEGORY_ID,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20805, 'No ticket category found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20806, 'Error updating ticket category: ' || SQLERRM);
END SP_SYS_TICKET_CATEGORY_UPDATE;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_CATEGORY_DELETE
-- Description: Soft deletes a ticket category by setting IS_ACTIVE to 'N'
-- Parameters:
--   P_ROW_ID: The category ID to delete
--   P_DELETE_USER: User performing the deletion
-- Requirements: Category management support
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_CATEGORY_DELETE (
    P_ROW_ID IN NUMBER,
    P_DELETE_USER IN NVARCHAR2
)
AS
    V_ACTIVE_TICKET_COUNT NUMBER;
    V_CHILD_CATEGORY_COUNT NUMBER;
BEGIN
    -- Check if there are active tickets using this category
    SELECT COUNT(*) INTO V_ACTIVE_TICKET_COUNT
    FROM SYS_REQUEST_TICKET
    WHERE TICKET_CATEGORY_ID = P_ROW_ID AND IS_ACTIVE = 'Y';
    
    IF V_ACTIVE_TICKET_COUNT > 0 THEN
        RAISE_APPLICATION_ERROR(-20807, 'Cannot delete category with active tickets');
    END IF;
    
    -- Check if there are child categories
    SELECT COUNT(*) INTO V_CHILD_CATEGORY_COUNT
    FROM SYS_TICKET_CATEGORY
    WHERE PARENT_CATEGORY_ID = P_ROW_ID AND IS_ACTIVE = 'Y';
    
    IF V_CHILD_CATEGORY_COUNT > 0 THEN
        RAISE_APPLICATION_ERROR(-20808, 'Cannot delete category with active child categories');
    END IF;
    
    -- Soft delete by setting IS_ACTIVE to 'N'
    UPDATE SYS_TICKET_CATEGORY
    SET 
        IS_ACTIVE = 'N',
        UPDATE_USER = P_DELETE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20809, 'No ticket category found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20810, 'Error deleting ticket category: ' || SQLERRM);
END SP_SYS_TICKET_CATEGORY_DELETE;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_CATEGORY_SELECT_BY_ID
-- Description: Retrieves a specific ticket category by ID
-- Parameters:
--   P_ROW_ID: The category ID to retrieve
-- Returns: SYS_REFCURSOR with the matching category
-- Requirements: Category management support
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_CATEGORY_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        c.ROW_ID,
        c.CATEGORY_NAME_AR,
        c.CATEGORY_NAME_EN,
        c.DESCRIPTION_AR,
        c.DESCRIPTION_EN,
        c.PARENT_CATEGORY_ID,
        pc.CATEGORY_NAME_AR AS PARENT_CATEGORY_NAME_AR,
        pc.CATEGORY_NAME_EN AS PARENT_CATEGORY_NAME_EN,
        c.IS_ACTIVE,
        c.CREATION_USER,
        c.CREATION_DATE,
        c.UPDATE_USER,
        c.UPDATE_DATE
    FROM SYS_TICKET_CATEGORY c
    LEFT JOIN SYS_TICKET_CATEGORY pc ON c.PARENT_CATEGORY_ID = pc.ROW_ID
    WHERE c.ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20811, 'Error retrieving ticket category by ID: ' || SQLERRM);
END SP_SYS_TICKET_CATEGORY_SELECT_BY_ID;
/

-- =============================================
-- ENHANCED REPORTING PROCEDURES
-- =============================================

-- =============================================
-- Procedure: SP_SYS_TICKET_REPORTS_ESCALATION
-- Description: Generates escalation reports for overdue tickets requiring management attention
-- Parameters:
--   P_COMPANY_ID: Filter by company (optional, 0 = all)
--   P_PRIORITY_LEVEL: Filter by priority level (optional, 0 = all)
-- Returns: SYS_REFCURSOR with escalation data
-- Requirements: 9.8
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_REPORTS_ESCALATION (
    P_COMPANY_ID IN NUMBER DEFAULT 0,
    P_PRIORITY_LEVEL IN NUMBER DEFAULT 0,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
    V_WHERE_CLAUSE NVARCHAR2(500);
    V_SQL NCLOB;
BEGIN
    -- Build WHERE clause for escalation criteria
    V_WHERE_CLAUSE := 'WHERE t.IS_ACTIVE = ''Y'' 
                       AND st.STATUS_CODE NOT IN (''CLOSED'', ''CANCELLED'', ''RESOLVED'')
                       AND SYSDATE > t.EXPECTED_RESOLUTION_DATE';
    
    IF P_COMPANY_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.COMPANY_ID = ' || P_COMPANY_ID;
    END IF;
    
    IF P_PRIORITY_LEVEL > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND pr.PRIORITY_LEVEL = ' || P_PRIORITY_LEVEL;
    END IF;
    
    V_SQL := 'SELECT 
        t.ROW_ID,
        t.TITLE_EN,
        c.ROW_DESC_E AS COMPANY_NAME,
        b.ROW_DESC_E AS BRANCH_NAME,
        req.ROW_DESC_E AS REQUESTER_NAME,
        req.EMAIL AS REQUESTER_EMAIL,
        ass.ROW_DESC_E AS ASSIGNEE_NAME,
        ass.EMAIL AS ASSIGNEE_EMAIL,
        tt.TYPE_NAME_EN AS TYPE_NAME,
        st.STATUS_NAME_EN AS STATUS_NAME,
        pr.PRIORITY_NAME_EN AS PRIORITY_NAME,
        pr.PRIORITY_LEVEL,
        t.CREATION_DATE,
        t.EXPECTED_RESOLUTION_DATE,
        ROUND((SYSDATE - t.EXPECTED_RESOLUTION_DATE) * 24, 2) AS OVERDUE_HOURS,
        ROUND((SYSDATE - t.EXPECTED_RESOLUTION_DATE), 0) AS OVERDUE_DAYS,
        CASE 
            WHEN (SYSDATE - t.EXPECTED_RESOLUTION_DATE) > 7 THEN ''Critical Escalation''
            WHEN (SYSDATE - t.EXPECTED_RESOLUTION_DATE) > 3 THEN ''High Escalation''
            WHEN (SYSDATE - t.EXPECTED_RESOLUTION_DATE) > 1 THEN ''Medium Escalation''
            ELSE ''Low Escalation''
        END AS ESCALATION_LEVEL,
        CASE 
            WHEN t.ASSIGNEE_ID IS NULL THEN ''Unassigned''
            WHEN pr.PRIORITY_LEVEL <= 2 AND (SYSDATE - t.EXPECTED_RESOLUTION_DATE) > 1 THEN ''Immediate Action Required''
            WHEN (SYSDATE - t.EXPECTED_RESOLUTION_DATE) > 3 THEN ''Management Review Required''
            ELSE ''Follow Up Required''
        END AS RECOMMENDED_ACTION
    FROM SYS_REQUEST_TICKET t
    LEFT JOIN SYS_COMPANY c ON t.COMPANY_ID = c.ROW_ID
    LEFT JOIN SYS_BRANCH b ON t.BRANCH_ID = b.ROW_ID
    LEFT JOIN SYS_USERS req ON t.REQUESTER_ID = req.ROW_ID
    LEFT JOIN SYS_USERS ass ON t.ASSIGNEE_ID = ass.ROW_ID
    LEFT JOIN SYS_TICKET_TYPE tt ON t.TICKET_TYPE_ID = tt.ROW_ID
    LEFT JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
    LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
    ' || V_WHERE_CLAUSE || '
    ORDER BY 
        pr.PRIORITY_LEVEL ASC,
        (SYSDATE - t.EXPECTED_RESOLUTION_DATE) DESC,
        t.CREATION_DATE ASC';
    
    OPEN P_RESULT_CURSOR FOR V_SQL;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20901, 'Error generating escalation report: ' || SQLERRM);
END SP_SYS_TICKET_REPORTS_ESCALATION;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_REPORTS_CUSTOMER_SATISFACTION
-- Description: Generates customer satisfaction metrics based on ticket feedback
-- Parameters:
--   P_START_DATE: Report start date
--   P_END_DATE: Report end date
--   P_COMPANY_ID: Filter by company (optional, 0 = all)
-- Returns: SYS_REFCURSOR with satisfaction metrics
-- Requirements: 9.6
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_REPORTS_CUSTOMER_SATISFACTION (
    P_START_DATE IN DATE,
    P_END_DATE IN DATE,
    P_COMPANY_ID IN NUMBER DEFAULT 0,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
    V_WHERE_CLAUSE NVARCHAR2(500);
    V_SQL NCLOB;
BEGIN
    -- Build WHERE clause
    V_WHERE_CLAUSE := 'WHERE t.ACTUAL_RESOLUTION_DATE >= :start_date 
                       AND t.ACTUAL_RESOLUTION_DATE <= :end_date 
                       AND t.IS_ACTIVE = ''Y''
                       AND st.STATUS_CODE IN (''RESOLVED'', ''CLOSED'')';
    
    IF P_COMPANY_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.COMPANY_ID = ' || P_COMPANY_ID;
    END IF;
    
    V_SQL := 'SELECT 
        c.ROW_DESC_E AS COMPANY_NAME,
        tt.TYPE_NAME_EN AS TYPE_NAME,
        pr.PRIORITY_NAME_EN AS PRIORITY_NAME,
        COUNT(*) AS TOTAL_RESOLVED_TICKETS,
        COUNT(CASE WHEN t.ACTUAL_RESOLUTION_DATE <= t.EXPECTED_RESOLUTION_DATE THEN 1 END) AS ON_TIME_RESOLUTIONS,
        ROUND(
            (COUNT(CASE WHEN t.ACTUAL_RESOLUTION_DATE <= t.EXPECTED_RESOLUTION_DATE THEN 1 END) * 100.0) / 
            NULLIF(COUNT(*), 0), 2
        ) AS ON_TIME_PERCENTAGE,
        ROUND(AVG((t.ACTUAL_RESOLUTION_DATE - t.CREATION_DATE) * 24), 2) AS AVG_RESOLUTION_HOURS,
        ROUND(AVG(pr.SLA_TARGET_HOURS), 2) AS AVG_SLA_TARGET_HOURS,
        COUNT(CASE WHEN (t.ACTUAL_RESOLUTION_DATE - t.CREATION_DATE) * 24 <= pr.SLA_TARGET_HOURS * 0.5 THEN 1 END) AS FAST_RESOLUTIONS,
        COUNT(CASE WHEN (t.ACTUAL_RESOLUTION_DATE - t.CREATION_DATE) * 24 > pr.SLA_TARGET_HOURS * 1.5 THEN 1 END) AS SLOW_RESOLUTIONS,
        CASE 
            WHEN AVG((t.ACTUAL_RESOLUTION_DATE - t.CREATION_DATE) * 24) <= AVG(pr.SLA_TARGET_HOURS) * 0.7 THEN ''Excellent''
            WHEN AVG((t.ACTUAL_RESOLUTION_DATE - t.CREATION_DATE) * 24) <= AVG(pr.SLA_TARGET_HOURS) THEN ''Good''
            WHEN AVG((t.ACTUAL_RESOLUTION_DATE - t.CREATION_DATE) * 24) <= AVG(pr.SLA_TARGET_HOURS) * 1.2 THEN ''Fair''
            ELSE ''Poor''
        END AS SATISFACTION_RATING
    FROM SYS_REQUEST_TICKET t
    LEFT JOIN SYS_COMPANY c ON t.COMPANY_ID = c.ROW_ID
    LEFT JOIN SYS_TICKET_TYPE tt ON t.TICKET_TYPE_ID = tt.ROW_ID
    LEFT JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
    LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
    ' || V_WHERE_CLAUSE || '
    GROUP BY c.ROW_DESC_E, tt.TYPE_NAME_EN, pr.PRIORITY_NAME_EN
    ORDER BY c.ROW_DESC_E, tt.TYPE_NAME_EN, pr.PRIORITY_LEVEL';
    
    OPEN P_RESULT_CURSOR FOR V_SQL USING P_START_DATE, P_END_DATE;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20902, 'Error generating customer satisfaction report: ' || SQLERRM);
END SP_SYS_TICKET_REPORTS_CUSTOMER_SATISFACTION;
/

-- =============================================
-- UTILITY AND MAINTENANCE PROCEDURES
-- =============================================

-- =============================================
-- Procedure: SP_SYS_TICKET_MAINTENANCE_CLEANUP
-- Description: Performs maintenance cleanup tasks for the ticket system
-- Parameters:
--   P_DAYS_TO_KEEP: Number of days to keep resolved/closed tickets (default 365)
--   P_DRY_RUN: If 'Y', only shows what would be cleaned up without actual deletion
-- Requirements: System maintenance support
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_MAINTENANCE_CLEANUP (
    P_DAYS_TO_KEEP IN NUMBER DEFAULT 365,
    P_DRY_RUN IN CHAR DEFAULT 'Y'
)
AS
    V_CUTOFF_DATE DATE;
    V_TICKETS_TO_ARCHIVE NUMBER;
    V_COMMENTS_TO_ARCHIVE NUMBER;
    V_ATTACHMENTS_TO_ARCHIVE NUMBER;
BEGIN
    V_CUTOFF_DATE := SYSDATE - P_DAYS_TO_KEEP;
    
    -- Count records that would be affected
    SELECT COUNT(*) INTO V_TICKETS_TO_ARCHIVE
    FROM SYS_REQUEST_TICKET t
    JOIN SYS_TICKET_STATUS s ON t.TICKET_STATUS_ID = s.ROW_ID
    WHERE s.STATUS_CODE IN ('CLOSED', 'CANCELLED')
    AND t.ACTUAL_RESOLUTION_DATE < V_CUTOFF_DATE
    AND t.IS_ACTIVE = 'Y';
    
    SELECT COUNT(*) INTO V_COMMENTS_TO_ARCHIVE
    FROM SYS_TICKET_COMMENT c
    JOIN SYS_REQUEST_TICKET t ON c.TICKET_ID = t.ROW_ID
    JOIN SYS_TICKET_STATUS s ON t.TICKET_STATUS_ID = s.ROW_ID
    WHERE s.STATUS_CODE IN ('CLOSED', 'CANCELLED')
    AND t.ACTUAL_RESOLUTION_DATE < V_CUTOFF_DATE
    AND t.IS_ACTIVE = 'Y';
    
    SELECT COUNT(*) INTO V_ATTACHMENTS_TO_ARCHIVE
    FROM SYS_TICKET_ATTACHMENT a
    JOIN SYS_REQUEST_TICKET t ON a.TICKET_ID = t.ROW_ID
    JOIN SYS_TICKET_STATUS s ON t.TICKET_STATUS_ID = s.ROW_ID
    WHERE s.STATUS_CODE IN ('CLOSED', 'CANCELLED')
    AND t.ACTUAL_RESOLUTION_DATE < V_CUTOFF_DATE
    AND t.IS_ACTIVE = 'Y';
    
    -- Output summary
    DBMS_OUTPUT.PUT_LINE('=== TICKET SYSTEM MAINTENANCE CLEANUP SUMMARY ===');
    DBMS_OUTPUT.PUT_LINE('Cutoff Date: ' || TO_CHAR(V_CUTOFF_DATE, 'YYYY-MM-DD HH24:MI:SS'));
    DBMS_OUTPUT.PUT_LINE('Days to Keep: ' || P_DAYS_TO_KEEP);
    DBMS_OUTPUT.PUT_LINE('Dry Run Mode: ' || P_DRY_RUN);
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Records to be archived:');
    DBMS_OUTPUT.PUT_LINE('- Tickets: ' || V_TICKETS_TO_ARCHIVE);
    DBMS_OUTPUT.PUT_LINE('- Comments: ' || V_COMMENTS_TO_ARCHIVE);
    DBMS_OUTPUT.PUT_LINE('- Attachments: ' || V_ATTACHMENTS_TO_ARCHIVE);
    
    IF P_DRY_RUN = 'N' AND V_TICKETS_TO_ARCHIVE > 0 THEN
        -- Perform actual cleanup (soft delete)
        UPDATE SYS_REQUEST_TICKET
        SET IS_ACTIVE = 'N',
            UPDATE_USER = 'system_cleanup',
            UPDATE_DATE = SYSDATE
        WHERE ROW_ID IN (
            SELECT t.ROW_ID
            FROM SYS_REQUEST_TICKET t
            JOIN SYS_TICKET_STATUS s ON t.TICKET_STATUS_ID = s.ROW_ID
            WHERE s.STATUS_CODE IN ('CLOSED', 'CANCELLED')
            AND t.ACTUAL_RESOLUTION_DATE < V_CUTOFF_DATE
            AND t.IS_ACTIVE = 'Y'
        );
        
        COMMIT;
        DBMS_OUTPUT.PUT_LINE('');
        DBMS_OUTPUT.PUT_LINE('Cleanup completed successfully.');
        DBMS_OUTPUT.PUT_LINE('Archived ' || SQL%ROWCOUNT || ' tickets.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('');
        DBMS_OUTPUT.PUT_LINE('No cleanup performed (dry run mode or no records to archive).');
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20903, 'Error during maintenance cleanup: ' || SQLERRM);
END SP_SYS_TICKET_MAINTENANCE_CLEANUP;
/

-- =============================================
-- ADDITIONAL SEED DATA INSERTION
-- =============================================

-- Insert additional seed data for enhanced system functionality
BEGIN
    -- Insert additional ticket statuses if needed
    DECLARE
        V_COUNT NUMBER;
    BEGIN
        SELECT COUNT(*) INTO V_COUNT FROM SYS_TICKET_STATUS WHERE STATUS_CODE = 'ON_HOLD';
        IF V_COUNT = 0 THEN
            INSERT INTO SYS_TICKET_STATUS (ROW_ID, STATUS_NAME_AR, STATUS_NAME_EN, STATUS_CODE, DISPLAY_ORDER, IS_FINAL_STATUS, CREATION_USER)
            VALUES (SEQ_SYS_TICKET_STATUS.NEXTVAL, 'معلق', 'On Hold', 'ON_HOLD', 7, 'N', 'system');
        END IF;
        
        SELECT COUNT(*) INTO V_COUNT FROM SYS_TICKET_STATUS WHERE STATUS_CODE = 'REOPENED';
        IF V_COUNT = 0 THEN
            INSERT INTO SYS_TICKET_STATUS (ROW_ID, STATUS_NAME_AR, STATUS_NAME_EN, STATUS_CODE, DISPLAY_ORDER, IS_FINAL_STATUS, CREATION_USER)
            VALUES (SEQ_SYS_TICKET_STATUS.NEXTVAL, 'معاد فتحه', 'Reopened', 'REOPENED', 8, 'N', 'system');
        END IF;
    END;
    
    -- Insert additional categories for better organization
    DECLARE
        V_COUNT NUMBER;
    BEGIN
        SELECT COUNT(*) INTO V_COUNT FROM SYS_TICKET_CATEGORY WHERE CATEGORY_NAME_EN = 'Performance';
        IF V_COUNT = 0 THEN
            INSERT INTO SYS_TICKET_CATEGORY (ROW_ID, CATEGORY_NAME_AR, CATEGORY_NAME_EN, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
            VALUES (SEQ_SYS_TICKET_CATEGORY.NEXTVAL, 'الأداء', 'Performance', 'مشاكل وتحسينات الأداء', 'Performance issues and improvements', 'system');
        END IF;
        
        SELECT COUNT(*) INTO V_COUNT FROM SYS_TICKET_CATEGORY WHERE CATEGORY_NAME_EN = 'Security';
        IF V_COUNT = 0 THEN
            INSERT INTO SYS_TICKET_CATEGORY (ROW_ID, CATEGORY_NAME_AR, CATEGORY_NAME_EN, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
            VALUES (SEQ_SYS_TICKET_CATEGORY.NEXTVAL, 'الأمان', 'Security', 'طلبات ومشاكل الأمان', 'Security requests and issues', 'system');
        END IF;
        
        SELECT COUNT(*) INTO V_COUNT FROM SYS_TICKET_CATEGORY WHERE CATEGORY_NAME_EN = 'Configuration';
        IF V_COUNT = 0 THEN
            INSERT INTO SYS_TICKET_CATEGORY (ROW_ID, CATEGORY_NAME_AR, CATEGORY_NAME_EN, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
            VALUES (SEQ_SYS_TICKET_CATEGORY.NEXTVAL, 'الإعدادات', 'Configuration', 'طلبات تغيير الإعدادات', 'Configuration change requests', 'system');
        END IF;
    END;
    
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Additional seed data inserted successfully.');
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20904, 'Error inserting additional seed data: ' || SQLERRM);
END;
/

-- =============================================
-- Verification: Display all created procedures
-- =============================================
SELECT object_name, object_type, status, created
FROM user_objects
WHERE object_name IN (
    'SP_SYS_TICKET_CATEGORY_INSERT',
    'SP_SYS_TICKET_CATEGORY_UPDATE',
    'SP_SYS_TICKET_CATEGORY_DELETE',
    'SP_SYS_TICKET_CATEGORY_SELECT_BY_ID',
    'SP_SYS_TICKET_REPORTS_ESCALATION',
    'SP_SYS_TICKET_REPORTS_CUSTOMER_SATISFACTION',
    'SP_SYS_TICKET_MAINTENANCE_CLEANUP'
)
ORDER BY object_name;

-- Display summary of all ticket-related procedures
SELECT 'Total Ticket System Procedures: ' || COUNT(*) AS SUMMARY
FROM user_objects
WHERE object_name LIKE 'SP_SYS_TICKET%' OR object_name LIKE 'SP_SYS_REQUEST_TICKET%'
AND object_type = 'PROCEDURE'
AND status = 'VALID';

-- Display summary of all ticket-related tables
SELECT 'Total Ticket System Tables: ' || COUNT(*) AS SUMMARY
FROM user_tables
WHERE table_name LIKE 'SYS_TICKET%' OR table_name = 'SYS_REQUEST_TICKET';

-- Display summary of all ticket-related sequences
SELECT 'Total Ticket System Sequences: ' || COUNT(*) AS SUMMARY
FROM user_sequences
WHERE sequence_name LIKE 'SEQ_SYS_TICKET%' OR sequence_name = 'SEQ_SYS_REQUEST_TICKET';

COMMIT;