-- =============================================
-- Company Request Tickets System - Supporting Entity Procedures
-- Description: Stored procedures for ticket comments, attachments, and types
-- Requirements: 2.1-2.10, 6.1-6.12, 7.1-7.12
-- =============================================

-- =============================================
-- TICKET COMMENT PROCEDURES
-- =============================================

-- =============================================
-- Procedure: SP_SYS_TICKET_COMMENT_INSERT
-- Description: Adds a comment to a ticket
-- Parameters:
--   P_TICKET_ID: Ticket ID (foreign key)
--   P_COMMENT_TEXT: Comment text content
--   P_IS_INTERNAL: Internal comment flag ('Y' or 'N')
--   P_CREATION_USER: User creating the comment
--   P_NEW_ID: Output parameter returning the new comment ID
-- Requirements: 6.1-6.5
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_COMMENT_INSERT (
    P_TICKET_ID IN NUMBER,
    P_COMMENT_TEXT IN NCLOB,
    P_IS_INTERNAL IN CHAR,
    P_CREATION_USER IN NVARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
BEGIN
    -- Validate ticket exists and is active
    DECLARE
        V_TICKET_COUNT NUMBER;
    BEGIN
        SELECT COUNT(*) INTO V_TICKET_COUNT
        FROM SYS_REQUEST_TICKET
        WHERE ROW_ID = P_TICKET_ID AND IS_ACTIVE = 'Y';
        
        IF V_TICKET_COUNT = 0 THEN
            RAISE_APPLICATION_ERROR(-20501, 'Ticket not found or inactive');
        END IF;
    END;
    
    -- Generate new ID from sequence
    SELECT SEQ_SYS_TICKET_COMMENT.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
    -- Insert the new comment record
    INSERT INTO SYS_TICKET_COMMENT (
        ROW_ID,
        TICKET_ID,
        COMMENT_TEXT,
        IS_INTERNAL,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_TICKET_ID,
        P_COMMENT_TEXT,
        COALESCE(P_IS_INTERNAL, 'N'),
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20502, 'Error inserting comment: ' || SQLERRM);
END SP_SYS_TICKET_COMMENT_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_COMMENT_SELECT_BY_TICKET
-- Description: Retrieves all comments for a specific ticket
-- Parameters:
--   P_TICKET_ID: Ticket ID to get comments for
--   P_INCLUDE_INTERNAL: Include internal comments ('Y' or 'N')
-- Returns: SYS_REFCURSOR with comments ordered by creation date
-- Requirements: 6.8, 6.6-6.7
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_COMMENT_SELECT_BY_TICKET (
    P_TICKET_ID IN NUMBER,
    P_INCLUDE_INTERNAL IN CHAR DEFAULT 'N',
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    IF P_INCLUDE_INTERNAL = 'Y' THEN
        -- Include all comments (for admin users)
        OPEN P_RESULT_CURSOR FOR
        SELECT 
            c.ROW_ID,
            c.TICKET_ID,
            c.COMMENT_TEXT,
            c.IS_INTERNAL,
            c.CREATION_USER,
            c.CREATION_DATE,
            u.ROW_DESC_E AS COMMENTER_NAME,
            u.USER_NAME AS COMMENTER_USERNAME
        FROM SYS_TICKET_COMMENT c
        LEFT JOIN SYS_USERS u ON c.CREATION_USER = u.USER_NAME
        WHERE c.TICKET_ID = P_TICKET_ID
        ORDER BY c.CREATION_DATE ASC;
    ELSE
        -- Include only public comments (for regular users)
        OPEN P_RESULT_CURSOR FOR
        SELECT 
            c.ROW_ID,
            c.TICKET_ID,
            c.COMMENT_TEXT,
            c.IS_INTERNAL,
            c.CREATION_USER,
            c.CREATION_DATE,
            u.ROW_DESC_E AS COMMENTER_NAME,
            u.USER_NAME AS COMMENTER_USERNAME
        FROM SYS_TICKET_COMMENT c
        LEFT JOIN SYS_USERS u ON c.CREATION_USER = u.USER_NAME
        WHERE c.TICKET_ID = P_TICKET_ID
        AND c.IS_INTERNAL = 'N'
        ORDER BY c.CREATION_DATE ASC;
    END IF;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20503, 'Error retrieving comments: ' || SQLERRM);
END SP_SYS_TICKET_COMMENT_SELECT_BY_TICKET;
/

-- =============================================
-- TICKET ATTACHMENT PROCEDURES
-- =============================================

-- =============================================
-- Procedure: SP_SYS_TICKET_ATTACHMENT_INSERT
-- Description: Adds a file attachment to a ticket
-- Parameters:
--   P_TICKET_ID: Ticket ID (foreign key)
--   P_FILE_NAME: Original file name
--   P_FILE_SIZE: File size in bytes
--   P_MIME_TYPE: File MIME type
--   P_FILE_CONTENT: Binary file content (BLOB)
--   P_CREATION_USER: User uploading the file
--   P_NEW_ID: Output parameter returning the new attachment ID
-- Requirements: 7.1-7.6
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_ATTACHMENT_INSERT (
    P_TICKET_ID IN NUMBER,
    P_FILE_NAME IN NVARCHAR2,
    P_FILE_SIZE IN NUMBER,
    P_MIME_TYPE IN NVARCHAR2,
    P_FILE_CONTENT IN BLOB,
    P_CREATION_USER IN NVARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
    V_ATTACHMENT_COUNT NUMBER;
    V_TOTAL_SIZE NUMBER;
BEGIN
    -- Validate ticket exists and is active
    DECLARE
        V_TICKET_COUNT NUMBER;
    BEGIN
        SELECT COUNT(*) INTO V_TICKET_COUNT
        FROM SYS_REQUEST_TICKET
        WHERE ROW_ID = P_TICKET_ID AND IS_ACTIVE = 'Y';
        
        IF V_TICKET_COUNT = 0 THEN
            RAISE_APPLICATION_ERROR(-20601, 'Ticket not found or inactive');
        END IF;
    END;
    
    -- Check attachment count limit (max 5 per ticket)
    SELECT COUNT(*) INTO V_ATTACHMENT_COUNT
    FROM SYS_TICKET_ATTACHMENT
    WHERE TICKET_ID = P_TICKET_ID;
    
    IF V_ATTACHMENT_COUNT >= 5 THEN
        RAISE_APPLICATION_ERROR(-20602, 'Maximum number of attachments (5) exceeded for this ticket');
    END IF;
    
    -- Validate file size (max 10MB = 10485760 bytes)
    IF P_FILE_SIZE > 10485760 THEN
        RAISE_APPLICATION_ERROR(-20603, 'File size exceeds maximum limit of 10MB');
    END IF;
    
    -- Validate file type (basic MIME type validation)
    IF P_MIME_TYPE NOT IN (
        'application/pdf',
        'application/msword',
        'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        'application/vnd.ms-excel',
        'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        'image/jpeg',
        'image/png',
        'text/plain'
    ) THEN
        RAISE_APPLICATION_ERROR(-20604, 'File type not allowed. Supported types: PDF, DOC, DOCX, XLS, XLSX, JPG, PNG, TXT');
    END IF;
    
    -- Generate new ID from sequence
    SELECT SEQ_SYS_TICKET_ATTACHMENT.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
    -- Insert the new attachment record
    INSERT INTO SYS_TICKET_ATTACHMENT (
        ROW_ID,
        TICKET_ID,
        FILE_NAME,
        FILE_SIZE,
        MIME_TYPE,
        FILE_CONTENT,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_TICKET_ID,
        P_FILE_NAME,
        P_FILE_SIZE,
        P_MIME_TYPE,
        P_FILE_CONTENT,
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20605, 'Error inserting attachment: ' || SQLERRM);
END SP_SYS_TICKET_ATTACHMENT_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_ATTACHMENT_SELECT_BY_TICKET
-- Description: Retrieves attachment metadata for a specific ticket
-- Parameters:
--   P_TICKET_ID: Ticket ID to get attachments for
-- Returns: SYS_REFCURSOR with attachment metadata (without BLOB content)
-- Requirements: 7.9, 11.9
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_ATTACHMENT_SELECT_BY_TICKET (
    P_TICKET_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        a.ROW_ID,
        a.TICKET_ID,
        a.FILE_NAME,
        a.FILE_SIZE,
        a.MIME_TYPE,
        a.CREATION_USER,
        a.CREATION_DATE,
        u.ROW_DESC_E AS UPLOADER_NAME,
        u.USER_NAME AS UPLOADER_USERNAME,
        ROUND(a.FILE_SIZE / 1024, 2) AS FILE_SIZE_KB
    FROM SYS_TICKET_ATTACHMENT a
    LEFT JOIN SYS_USERS u ON a.CREATION_USER = u.USER_NAME
    WHERE a.TICKET_ID = P_TICKET_ID
    ORDER BY a.CREATION_DATE DESC;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20606, 'Error retrieving attachments: ' || SQLERRM);
END SP_SYS_TICKET_ATTACHMENT_SELECT_BY_TICKET;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_ATTACHMENT_SELECT_BY_ID
-- Description: Retrieves a specific attachment including BLOB content for download
-- Parameters:
--   P_ROW_ID: Attachment ID to retrieve
-- Returns: SYS_REFCURSOR with complete attachment data including BLOB
-- Requirements: 7.9, 11.10
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_ATTACHMENT_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        a.ROW_ID,
        a.TICKET_ID,
        a.FILE_NAME,
        a.FILE_SIZE,
        a.MIME_TYPE,
        a.FILE_CONTENT,
        a.CREATION_USER,
        a.CREATION_DATE
    FROM SYS_TICKET_ATTACHMENT a
    WHERE a.ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20607, 'Error retrieving attachment: ' || SQLERRM);
END SP_SYS_TICKET_ATTACHMENT_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_ATTACHMENT_DELETE
-- Description: Deletes an attachment (hard delete for security)
-- Parameters:
--   P_ROW_ID: Attachment ID to delete
--   P_DELETE_USER: User performing the deletion
-- Requirements: 7.10
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_ATTACHMENT_DELETE (
    P_ROW_ID IN NUMBER,
    P_DELETE_USER IN NVARCHAR2
)
AS
BEGIN
    -- Hard delete the attachment
    DELETE FROM SYS_TICKET_ATTACHMENT
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was deleted
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20608, 'No attachment found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20609, 'Error deleting attachment: ' || SQLERRM);
END SP_SYS_TICKET_ATTACHMENT_DELETE;
/

-- =============================================
-- TICKET TYPE PROCEDURES
-- =============================================

-- =============================================
-- Procedure: SP_SYS_TICKET_TYPE_SELECT_ALL
-- Description: Retrieves all active ticket types
-- Returns: SYS_REFCURSOR with all ticket types where IS_ACTIVE = 'Y'
-- Requirements: 2.7, 11.14
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_TYPE_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        tt.ROW_ID,
        tt.TYPE_NAME_AR,
        tt.TYPE_NAME_EN,
        tt.DESCRIPTION_AR,
        tt.DESCRIPTION_EN,
        tt.DEFAULT_PRIORITY_ID,
        pr.PRIORITY_NAME_EN AS DEFAULT_PRIORITY_NAME,
        pr.PRIORITY_LEVEL AS DEFAULT_PRIORITY_LEVEL,
        tt.SLA_TARGET_HOURS,
        tt.IS_ACTIVE,
        tt.CREATION_USER,
        tt.CREATION_DATE,
        tt.UPDATE_USER,
        tt.UPDATE_DATE
    FROM SYS_TICKET_TYPE tt
    LEFT JOIN SYS_TICKET_PRIORITY pr ON tt.DEFAULT_PRIORITY_ID = pr.ROW_ID
    WHERE tt.IS_ACTIVE = 'Y'
    ORDER BY tt.TYPE_NAME_EN;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20701, 'Error retrieving ticket types: ' || SQLERRM);
END SP_SYS_TICKET_TYPE_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_TYPE_SELECT_BY_ID
-- Description: Retrieves a specific ticket type by ID
-- Parameters:
--   P_ROW_ID: The ticket type ID to retrieve
-- Returns: SYS_REFCURSOR with the matching ticket type
-- Requirements: 2.7
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_TYPE_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        tt.ROW_ID,
        tt.TYPE_NAME_AR,
        tt.TYPE_NAME_EN,
        tt.DESCRIPTION_AR,
        tt.DESCRIPTION_EN,
        tt.DEFAULT_PRIORITY_ID,
        pr.PRIORITY_NAME_AR AS DEFAULT_PRIORITY_NAME_AR,
        pr.PRIORITY_NAME_EN AS DEFAULT_PRIORITY_NAME_EN,
        pr.PRIORITY_LEVEL AS DEFAULT_PRIORITY_LEVEL,
        tt.SLA_TARGET_HOURS,
        tt.IS_ACTIVE,
        tt.CREATION_USER,
        tt.CREATION_DATE,
        tt.UPDATE_USER,
        tt.UPDATE_DATE
    FROM SYS_TICKET_TYPE tt
    LEFT JOIN SYS_TICKET_PRIORITY pr ON tt.DEFAULT_PRIORITY_ID = pr.ROW_ID
    WHERE tt.ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20702, 'Error retrieving ticket type by ID: ' || SQLERRM);
END SP_SYS_TICKET_TYPE_SELECT_BY_ID;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_TYPE_INSERT
-- Description: Inserts a new ticket type record
-- Parameters:
--   P_TYPE_NAME_AR: Type name in Arabic
--   P_TYPE_NAME_EN: Type name in English
--   P_DESCRIPTION_AR: Description in Arabic (optional)
--   P_DESCRIPTION_EN: Description in English (optional)
--   P_DEFAULT_PRIORITY_ID: Default priority ID (foreign key)
--   P_SLA_TARGET_HOURS: SLA target hours for this type
--   P_CREATION_USER: User creating the record
--   P_NEW_ID: Output parameter returning the new type ID
-- Requirements: 2.1-2.5, 11.15
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_TYPE_INSERT (
    P_TYPE_NAME_AR IN NVARCHAR2,
    P_TYPE_NAME_EN IN NVARCHAR2,
    P_DESCRIPTION_AR IN NVARCHAR2,
    P_DESCRIPTION_EN IN NVARCHAR2,
    P_DEFAULT_PRIORITY_ID IN NUMBER,
    P_SLA_TARGET_HOURS IN NUMBER,
    P_CREATION_USER IN NVARCHAR2,
    P_NEW_ID OUT NUMBER
)
AS
BEGIN
    -- Validate default priority exists and is active
    DECLARE
        V_PRIORITY_COUNT NUMBER;
    BEGIN
        SELECT COUNT(*) INTO V_PRIORITY_COUNT
        FROM SYS_TICKET_PRIORITY
        WHERE ROW_ID = P_DEFAULT_PRIORITY_ID AND IS_ACTIVE = 'Y';
        
        IF V_PRIORITY_COUNT = 0 THEN
            RAISE_APPLICATION_ERROR(-20703, 'Default priority not found or inactive');
        END IF;
    END;
    
    -- Generate new ID from sequence
    SELECT SEQ_SYS_TICKET_TYPE.NEXTVAL INTO P_NEW_ID FROM DUAL;
    
    -- Insert the new ticket type record
    INSERT INTO SYS_TICKET_TYPE (
        ROW_ID,
        TYPE_NAME_AR,
        TYPE_NAME_EN,
        DESCRIPTION_AR,
        DESCRIPTION_EN,
        DEFAULT_PRIORITY_ID,
        SLA_TARGET_HOURS,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        P_NEW_ID,
        P_TYPE_NAME_AR,
        P_TYPE_NAME_EN,
        P_DESCRIPTION_AR,
        P_DESCRIPTION_EN,
        P_DEFAULT_PRIORITY_ID,
        P_SLA_TARGET_HOURS,
        'Y',
        P_CREATION_USER,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20704, 'Error inserting ticket type: ' || SQLERRM);
END SP_SYS_TICKET_TYPE_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_TYPE_UPDATE
-- Description: Updates an existing ticket type record
-- Parameters:
--   P_ROW_ID: The ticket type ID to update
--   P_TYPE_NAME_AR: Type name in Arabic
--   P_TYPE_NAME_EN: Type name in English
--   P_DESCRIPTION_AR: Description in Arabic (optional)
--   P_DESCRIPTION_EN: Description in English (optional)
--   P_DEFAULT_PRIORITY_ID: Default priority ID (foreign key)
--   P_SLA_TARGET_HOURS: SLA target hours for this type
--   P_UPDATE_USER: User updating the record
-- Requirements: 2.1-2.5
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_TYPE_UPDATE (
    P_ROW_ID IN NUMBER,
    P_TYPE_NAME_AR IN NVARCHAR2,
    P_TYPE_NAME_EN IN NVARCHAR2,
    P_DESCRIPTION_AR IN NVARCHAR2,
    P_DESCRIPTION_EN IN NVARCHAR2,
    P_DEFAULT_PRIORITY_ID IN NUMBER,
    P_SLA_TARGET_HOURS IN NUMBER,
    P_UPDATE_USER IN NVARCHAR2
)
AS
BEGIN
    -- Validate default priority exists and is active
    DECLARE
        V_PRIORITY_COUNT NUMBER;
    BEGIN
        SELECT COUNT(*) INTO V_PRIORITY_COUNT
        FROM SYS_TICKET_PRIORITY
        WHERE ROW_ID = P_DEFAULT_PRIORITY_ID AND IS_ACTIVE = 'Y';
        
        IF V_PRIORITY_COUNT = 0 THEN
            RAISE_APPLICATION_ERROR(-20705, 'Default priority not found or inactive');
        END IF;
    END;
    
    -- Update the ticket type record
    UPDATE SYS_TICKET_TYPE
    SET 
        TYPE_NAME_AR = P_TYPE_NAME_AR,
        TYPE_NAME_EN = P_TYPE_NAME_EN,
        DESCRIPTION_AR = P_DESCRIPTION_AR,
        DESCRIPTION_EN = P_DESCRIPTION_EN,
        DEFAULT_PRIORITY_ID = P_DEFAULT_PRIORITY_ID,
        SLA_TARGET_HOURS = P_SLA_TARGET_HOURS,
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20706, 'No ticket type found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20707, 'Error updating ticket type: ' || SQLERRM);
END SP_SYS_TICKET_TYPE_UPDATE;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_TYPE_DELETE
-- Description: Soft deletes a ticket type by setting IS_ACTIVE to 'N'
-- Parameters:
--   P_ROW_ID: The ticket type ID to delete
--   P_DELETE_USER: User performing the deletion
-- Requirements: 2.6
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_TYPE_DELETE (
    P_ROW_ID IN NUMBER,
    P_DELETE_USER IN NVARCHAR2
)
AS
    V_ACTIVE_TICKET_COUNT NUMBER;
BEGIN
    -- Check if there are active tickets using this type
    SELECT COUNT(*) INTO V_ACTIVE_TICKET_COUNT
    FROM SYS_REQUEST_TICKET
    WHERE TICKET_TYPE_ID = P_ROW_ID AND IS_ACTIVE = 'Y';
    
    IF V_ACTIVE_TICKET_COUNT > 0 THEN
        RAISE_APPLICATION_ERROR(-20708, 'Cannot delete ticket type with active tickets');
    END IF;
    
    -- Soft delete by setting IS_ACTIVE to 'N'
    UPDATE SYS_TICKET_TYPE
    SET 
        IS_ACTIVE = 'N',
        UPDATE_USER = P_DELETE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20709, 'No ticket type found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20710, 'Error deleting ticket type: ' || SQLERRM);
END SP_SYS_TICKET_TYPE_DELETE;
/

-- =============================================
-- LOOKUP DATA PROCEDURES
-- =============================================

-- =============================================
-- Procedure: SP_SYS_TICKET_STATUS_SELECT_ALL
-- Description: Retrieves all active ticket statuses
-- Returns: SYS_REFCURSOR with all statuses ordered by display order
-- Requirements: 3.1
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_STATUS_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        STATUS_NAME_AR,
        STATUS_NAME_EN,
        STATUS_CODE,
        DISPLAY_ORDER,
        IS_FINAL_STATUS,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_TICKET_STATUS
    WHERE IS_ACTIVE = 'Y'
    ORDER BY DISPLAY_ORDER;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20801, 'Error retrieving ticket statuses: ' || SQLERRM);
END SP_SYS_TICKET_STATUS_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_PRIORITY_SELECT_ALL
-- Description: Retrieves all active ticket priorities
-- Returns: SYS_REFCURSOR with all priorities ordered by priority level
-- Requirements: 4.1
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_PRIORITY_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        PRIORITY_NAME_AR,
        PRIORITY_NAME_EN,
        PRIORITY_LEVEL,
        SLA_TARGET_HOURS,
        ESCALATION_THRESHOLD_HOURS,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_TICKET_PRIORITY
    WHERE IS_ACTIVE = 'Y'
    ORDER BY PRIORITY_LEVEL;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20802, 'Error retrieving ticket priorities: ' || SQLERRM);
END SP_SYS_TICKET_PRIORITY_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_CATEGORY_SELECT_ALL
-- Description: Retrieves all active ticket categories
-- Returns: SYS_REFCURSOR with all categories
-- Requirements: Category management
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_CATEGORY_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        CATEGORY_NAME_AR,
        CATEGORY_NAME_EN,
        DESCRIPTION_AR,
        DESCRIPTION_EN,
        PARENT_CATEGORY_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE
    FROM SYS_TICKET_CATEGORY
    WHERE IS_ACTIVE = 'Y'
    ORDER BY CATEGORY_NAME_EN;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20803, 'Error retrieving ticket categories: ' || SQLERRM);
END SP_SYS_TICKET_CATEGORY_SELECT_ALL;
/

-- =============================================
-- REPORTING AND ANALYTICS PROCEDURES
-- =============================================

-- =============================================
-- Procedure: SP_SYS_TICKET_REPORTS_VOLUME
-- Description: Generates ticket volume reports by time period, company, and type
-- Parameters:
--   P_START_DATE: Report start date
--   P_END_DATE: Report end date
--   P_COMPANY_ID: Filter by company (optional, 0 = all)
--   P_TICKET_TYPE_ID: Filter by ticket type (optional, 0 = all)
--   P_GROUP_BY: Grouping option ('DAILY', 'WEEKLY', 'MONTHLY', 'COMPANY', 'TYPE')
-- Returns: SYS_REFCURSOR with volume statistics
-- Requirements: 9.1
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_REPORTS_VOLUME (
    P_START_DATE IN DATE,
    P_END_DATE IN DATE,
    P_COMPANY_ID IN NUMBER DEFAULT 0,
    P_TICKET_TYPE_ID IN NUMBER DEFAULT 0,
    P_GROUP_BY IN VARCHAR2 DEFAULT 'DAILY',
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
    V_SQL NCLOB;
    V_WHERE_CLAUSE NVARCHAR2(500) := '';
    V_GROUP_CLAUSE NVARCHAR2(200);
    V_SELECT_CLAUSE NVARCHAR2(1000);
BEGIN
    -- Build WHERE clause
    V_WHERE_CLAUSE := 'WHERE t.CREATION_DATE >= :start_date AND t.CREATION_DATE <= :end_date AND t.IS_ACTIVE = ''Y''';
    
    IF P_COMPANY_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.COMPANY_ID = ' || P_COMPANY_ID;
    END IF;
    
    IF P_TICKET_TYPE_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.TICKET_TYPE_ID = ' || P_TICKET_TYPE_ID;
    END IF;
    
    -- Build SELECT and GROUP BY clauses based on grouping option
    CASE UPPER(P_GROUP_BY)
        WHEN 'DAILY' THEN
            V_SELECT_CLAUSE := 'TRUNC(t.CREATION_DATE) AS PERIOD_DATE, TO_CHAR(t.CREATION_DATE, ''YYYY-MM-DD'') AS PERIOD_LABEL';
            V_GROUP_CLAUSE := 'GROUP BY TRUNC(t.CREATION_DATE) ORDER BY TRUNC(t.CREATION_DATE)';
        WHEN 'WEEKLY' THEN
            V_SELECT_CLAUSE := 'TRUNC(t.CREATION_DATE, ''IW'') AS PERIOD_DATE, TO_CHAR(TRUNC(t.CREATION_DATE, ''IW''), ''YYYY-MM-DD'') || '' (Week)'' AS PERIOD_LABEL';
            V_GROUP_CLAUSE := 'GROUP BY TRUNC(t.CREATION_DATE, ''IW'') ORDER BY TRUNC(t.CREATION_DATE, ''IW'')';
        WHEN 'MONTHLY' THEN
            V_SELECT_CLAUSE := 'TRUNC(t.CREATION_DATE, ''MM'') AS PERIOD_DATE, TO_CHAR(t.CREATION_DATE, ''YYYY-MM'') AS PERIOD_LABEL';
            V_GROUP_CLAUSE := 'GROUP BY TRUNC(t.CREATION_DATE, ''MM'') ORDER BY TRUNC(t.CREATION_DATE, ''MM'')';
        WHEN 'COMPANY' THEN
            V_SELECT_CLAUSE := 't.COMPANY_ID, c.ROW_DESC_E AS COMPANY_NAME, NULL AS PERIOD_DATE, c.ROW_DESC_E AS PERIOD_LABEL';
            V_GROUP_CLAUSE := 'GROUP BY t.COMPANY_ID, c.ROW_DESC_E ORDER BY c.ROW_DESC_E';
        WHEN 'TYPE' THEN
            V_SELECT_CLAUSE := 't.TICKET_TYPE_ID, tt.TYPE_NAME_EN AS TYPE_NAME, NULL AS PERIOD_DATE, tt.TYPE_NAME_EN AS PERIOD_LABEL';
            V_GROUP_CLAUSE := 'GROUP BY t.TICKET_TYPE_ID, tt.TYPE_NAME_EN ORDER BY tt.TYPE_NAME_EN';
        ELSE
            V_SELECT_CLAUSE := 'TRUNC(t.CREATION_DATE) AS PERIOD_DATE, TO_CHAR(t.CREATION_DATE, ''YYYY-MM-DD'') AS PERIOD_LABEL';
            V_GROUP_CLAUSE := 'GROUP BY TRUNC(t.CREATION_DATE) ORDER BY TRUNC(t.CREATION_DATE)';
    END CASE;
    
    -- Build complete query
    V_SQL := 'SELECT 
        ' || V_SELECT_CLAUSE || ',
        COUNT(*) AS TOTAL_TICKETS,
        COUNT(CASE WHEN st.STATUS_CODE = ''OPEN'' THEN 1 END) AS OPEN_TICKETS,
        COUNT(CASE WHEN st.STATUS_CODE = ''IN_PROGRESS'' THEN 1 END) AS IN_PROGRESS_TICKETS,
        COUNT(CASE WHEN st.STATUS_CODE = ''RESOLVED'' THEN 1 END) AS RESOLVED_TICKETS,
        COUNT(CASE WHEN st.STATUS_CODE = ''CLOSED'' THEN 1 END) AS CLOSED_TICKETS,
        COUNT(CASE WHEN pr.PRIORITY_LEVEL = 1 THEN 1 END) AS CRITICAL_TICKETS,
        COUNT(CASE WHEN pr.PRIORITY_LEVEL = 2 THEN 1 END) AS HIGH_TICKETS,
        COUNT(CASE WHEN pr.PRIORITY_LEVEL = 3 THEN 1 END) AS MEDIUM_TICKETS,
        COUNT(CASE WHEN pr.PRIORITY_LEVEL = 4 THEN 1 END) AS LOW_TICKETS
    FROM SYS_REQUEST_TICKET t
    LEFT JOIN SYS_COMPANY c ON t.COMPANY_ID = c.ROW_ID
    LEFT JOIN SYS_TICKET_TYPE tt ON t.TICKET_TYPE_ID = tt.ROW_ID
    LEFT JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
    LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
    ' || V_WHERE_CLAUSE || '
    ' || V_GROUP_CLAUSE;
    
    OPEN P_RESULT_CURSOR FOR V_SQL USING P_START_DATE, P_END_DATE;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20901, 'Error generating volume report: ' || SQLERRM);
END SP_SYS_TICKET_REPORTS_VOLUME;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_REPORTS_SLA_COMPLIANCE
-- Description: Calculates SLA compliance percentages by priority and type
-- Parameters:
--   P_START_DATE: Report start date
--   P_END_DATE: Report end date
--   P_COMPANY_ID: Filter by company (optional, 0 = all)
-- Returns: SYS_REFCURSOR with SLA compliance statistics
-- Requirements: 9.2
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_REPORTS_SLA_COMPLIANCE (
    P_START_DATE IN DATE,
    P_END_DATE IN DATE,
    P_COMPANY_ID IN NUMBER DEFAULT 0,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
    V_WHERE_CLAUSE NVARCHAR2(500);
BEGIN
    -- Build WHERE clause
    V_WHERE_CLAUSE := 'WHERE t.CREATION_DATE >= :start_date AND t.CREATION_DATE <= :end_date AND t.IS_ACTIVE = ''Y''';
    
    IF P_COMPANY_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.COMPANY_ID = ' || P_COMPANY_ID;
    END IF;
    
    OPEN P_RESULT_CURSOR FOR
    'SELECT 
        pr.PRIORITY_NAME_EN AS PRIORITY_NAME,
        pr.PRIORITY_LEVEL,
        tt.TYPE_NAME_EN AS TYPE_NAME,
        COUNT(*) AS TOTAL_TICKETS,
        COUNT(CASE 
            WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL 
                AND t.ACTUAL_RESOLUTION_DATE <= t.EXPECTED_RESOLUTION_DATE 
            THEN 1 
        END) AS ON_TIME_RESOLVED,
        COUNT(CASE 
            WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL 
                AND t.ACTUAL_RESOLUTION_DATE > t.EXPECTED_RESOLUTION_DATE 
            THEN 1 
        END) AS OVERDUE_RESOLVED,
        COUNT(CASE 
            WHEN t.ACTUAL_RESOLUTION_DATE IS NULL 
                AND SYSDATE > t.EXPECTED_RESOLUTION_DATE 
            THEN 1 
        END) AS CURRENTLY_OVERDUE,
        ROUND(
            (COUNT(CASE 
                WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL 
                    AND t.ACTUAL_RESOLUTION_DATE <= t.EXPECTED_RESOLUTION_DATE 
                THEN 1 
            END) * 100.0) / NULLIF(COUNT(CASE WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL THEN 1 END), 0), 
            2
        ) AS SLA_COMPLIANCE_PERCENTAGE,
        ROUND(
            AVG(CASE 
                WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL 
                THEN (t.ACTUAL_RESOLUTION_DATE - t.CREATION_DATE) * 24 
            END), 
            2
        ) AS AVG_RESOLUTION_HOURS
    FROM SYS_REQUEST_TICKET t
    LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
    LEFT JOIN SYS_TICKET_TYPE tt ON t.TICKET_TYPE_ID = tt.ROW_ID
    ' || V_WHERE_CLAUSE || '
    GROUP BY pr.PRIORITY_NAME_EN, pr.PRIORITY_LEVEL, tt.TYPE_NAME_EN
    ORDER BY pr.PRIORITY_LEVEL, tt.TYPE_NAME_EN'
    USING P_START_DATE, P_END_DATE;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20902, 'Error generating SLA compliance report: ' || SQLERRM);
END SP_SYS_TICKET_REPORTS_SLA_COMPLIANCE;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_REPORTS_WORKLOAD
-- Description: Generates workload reports showing active and resolved tickets per assignee
-- Parameters:
--   P_START_DATE: Report start date (optional)
--   P_END_DATE: Report end date (optional)
--   P_COMPANY_ID: Filter by company (optional, 0 = all)
-- Returns: SYS_REFCURSOR with workload statistics per assignee
-- Requirements: 9.4
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_REPORTS_WORKLOAD (
    P_START_DATE IN DATE DEFAULT NULL,
    P_END_DATE IN DATE DEFAULT NULL,
    P_COMPANY_ID IN NUMBER DEFAULT 0,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
    V_WHERE_CLAUSE NVARCHAR2(500);
    V_SQL NCLOB;
BEGIN
    -- Build WHERE clause
    V_WHERE_CLAUSE := 'WHERE t.IS_ACTIVE = ''Y'' AND t.ASSIGNEE_ID IS NOT NULL';
    
    IF P_START_DATE IS NOT NULL AND P_END_DATE IS NOT NULL THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.CREATION_DATE >= :start_date AND t.CREATION_DATE <= :end_date';
    END IF;
    
    IF P_COMPANY_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.COMPANY_ID = ' || P_COMPANY_ID;
    END IF;
    
    V_SQL := 'SELECT 
        u.ROW_ID AS ASSIGNEE_ID,
        u.ROW_DESC_E AS ASSIGNEE_NAME,
        u.USER_NAME AS ASSIGNEE_USERNAME,
        u.EMAIL AS ASSIGNEE_EMAIL,
        COUNT(*) AS TOTAL_ASSIGNED_TICKETS,
        COUNT(CASE WHEN st.STATUS_CODE IN (''OPEN'', ''IN_PROGRESS'', ''PENDING_CUSTOMER'') THEN 1 END) AS ACTIVE_TICKETS,
        COUNT(CASE WHEN st.STATUS_CODE = ''RESOLVED'' THEN 1 END) AS RESOLVED_TICKETS,
        COUNT(CASE WHEN st.STATUS_CODE IN (''CLOSED'', ''CANCELLED'') THEN 1 END) AS CLOSED_TICKETS,
        COUNT(CASE WHEN pr.PRIORITY_LEVEL = 1 THEN 1 END) AS CRITICAL_TICKETS,
        COUNT(CASE WHEN pr.PRIORITY_LEVEL = 2 THEN 1 END) AS HIGH_TICKETS,
        COUNT(CASE 
            WHEN t.ACTUAL_RESOLUTION_DATE IS NULL 
                AND SYSDATE > t.EXPECTED_RESOLUTION_DATE 
            THEN 1 
        END) AS OVERDUE_TICKETS,
        ROUND(
            AVG(CASE 
                WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL 
                THEN (t.ACTUAL_RESOLUTION_DATE - t.CREATION_DATE) * 24 
            END), 
            2
        ) AS AVG_RESOLUTION_HOURS,
        ROUND(
            (COUNT(CASE 
                WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL 
                    AND t.ACTUAL_RESOLUTION_DATE <= t.EXPECTED_RESOLUTION_DATE 
                THEN 1 
            END) * 100.0) / NULLIF(COUNT(CASE WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL THEN 1 END), 0), 
            2
        ) AS SLA_COMPLIANCE_PERCENTAGE
    FROM SYS_REQUEST_TICKET t
    LEFT JOIN SYS_USERS u ON t.ASSIGNEE_ID = u.ROW_ID
    LEFT JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
    LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
    ' || V_WHERE_CLAUSE || '
    GROUP BY u.ROW_ID, u.ROW_DESC_E, u.USER_NAME, u.EMAIL
    ORDER BY COUNT(CASE WHEN st.STATUS_CODE IN (''OPEN'', ''IN_PROGRESS'', ''PENDING_CUSTOMER'') THEN 1 END) DESC';
    
    IF P_START_DATE IS NOT NULL AND P_END_DATE IS NOT NULL THEN
        OPEN P_RESULT_CURSOR FOR V_SQL USING P_START_DATE, P_END_DATE;
    ELSE
        OPEN P_RESULT_CURSOR FOR V_SQL;
    END IF;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20903, 'Error generating workload report: ' || SQLERRM);
END SP_SYS_TICKET_REPORTS_WORKLOAD;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_REPORTS_AGING
-- Description: Generates aging reports showing open ticket durations and SLA status
-- Parameters:
--   P_COMPANY_ID: Filter by company (optional, 0 = all)
--   P_ASSIGNEE_ID: Filter by assignee (optional, 0 = all)
-- Returns: SYS_REFCURSOR with aging analysis
-- Requirements: 9.7
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_REPORTS_AGING (
    P_COMPANY_ID IN NUMBER DEFAULT 0,
    P_ASSIGNEE_ID IN NUMBER DEFAULT 0,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
    V_WHERE_CLAUSE NVARCHAR2(500);
    V_SQL NCLOB;
BEGIN
    -- Build WHERE clause for open tickets only
    V_WHERE_CLAUSE := 'WHERE t.IS_ACTIVE = ''Y'' AND st.STATUS_CODE NOT IN (''CLOSED'', ''CANCELLED'', ''RESOLVED'')';
    
    IF P_COMPANY_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.COMPANY_ID = ' || P_COMPANY_ID;
    END IF;
    
    IF P_ASSIGNEE_ID > 0 THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.ASSIGNEE_ID = ' || P_ASSIGNEE_ID;
    END IF;
    
    V_SQL := 'SELECT 
        t.ROW_ID,
        t.TITLE_EN,
        c.ROW_DESC_E AS COMPANY_NAME,
        b.ROW_DESC_E AS BRANCH_NAME,
        req.ROW_DESC_E AS REQUESTER_NAME,
        ass.ROW_DESC_E AS ASSIGNEE_NAME,
        tt.TYPE_NAME_EN AS TYPE_NAME,
        st.STATUS_NAME_EN AS STATUS_NAME,
        pr.PRIORITY_NAME_EN AS PRIORITY_NAME,
        pr.PRIORITY_LEVEL,
        t.CREATION_DATE,
        t.EXPECTED_RESOLUTION_DATE,
        ROUND((SYSDATE - t.CREATION_DATE) * 24, 2) AS AGE_HOURS,
        ROUND((SYSDATE - t.CREATION_DATE), 0) AS AGE_DAYS,
        CASE 
            WHEN SYSDATE > t.EXPECTED_RESOLUTION_DATE THEN ''Overdue''
            WHEN SYSDATE > (t.EXPECTED_RESOLUTION_DATE - (pr.ESCALATION_THRESHOLD_HOURS / 24)) THEN ''At Risk''
            ELSE ''On Time''
        END AS SLA_STATUS,
        CASE 
            WHEN SYSDATE > t.EXPECTED_RESOLUTION_DATE 
            THEN ROUND((SYSDATE - t.EXPECTED_RESOLUTION_DATE) * 24, 2)
            ELSE 0
        END AS OVERDUE_HOURS,
        CASE 
            WHEN (SYSDATE - t.CREATION_DATE) <= 1 THEN ''0-1 Days''
            WHEN (SYSDATE - t.CREATION_DATE) <= 3 THEN ''1-3 Days''
            WHEN (SYSDATE - t.CREATION_DATE) <= 7 THEN ''3-7 Days''
            WHEN (SYSDATE - t.CREATION_DATE) <= 14 THEN ''1-2 Weeks''
            WHEN (SYSDATE - t.CREATION_DATE) <= 30 THEN ''2-4 Weeks''
            ELSE ''Over 1 Month''
        END AS AGE_BUCKET
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
        CASE 
            WHEN SYSDATE > t.EXPECTED_RESOLUTION_DATE THEN 1
            WHEN SYSDATE > (t.EXPECTED_RESOLUTION_DATE - (pr.ESCALATION_THRESHOLD_HOURS / 24)) THEN 2
            ELSE 3
        END,
        pr.PRIORITY_LEVEL,
        t.CREATION_DATE';
    
    OPEN P_RESULT_CURSOR FOR V_SQL;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20904, 'Error generating aging report: ' || SQLERRM);
END SP_SYS_TICKET_REPORTS_AGING;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_REPORTS_TRENDS
-- Description: Provides trend analysis showing ticket creation and resolution patterns over time
-- Parameters:
--   P_START_DATE: Analysis start date
--   P_END_DATE: Analysis end date
--   P_PERIOD_TYPE: Period grouping ('DAILY', 'WEEKLY', 'MONTHLY')
-- Returns: SYS_REFCURSOR with trend data
-- Requirements: 9.5
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_REPORTS_TRENDS (
    P_START_DATE IN DATE,
    P_END_DATE IN DATE,
    P_PERIOD_TYPE IN VARCHAR2 DEFAULT 'DAILY',
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
    V_SQL NCLOB;
    V_DATE_TRUNC VARCHAR2(10);
    V_DATE_FORMAT VARCHAR2(20);
BEGIN
    -- Set date truncation and format based on period type
    CASE UPPER(P_PERIOD_TYPE)
        WHEN 'WEEKLY' THEN
            V_DATE_TRUNC := 'IW';
            V_DATE_FORMAT := 'YYYY-MM-DD';
        WHEN 'MONTHLY' THEN
            V_DATE_TRUNC := 'MM';
            V_DATE_FORMAT := 'YYYY-MM';
        ELSE -- DAILY
            V_DATE_TRUNC := 'DD';
            V_DATE_FORMAT := 'YYYY-MM-DD';
    END CASE;
    
    V_SQL := 'WITH date_periods AS (
        SELECT 
            TRUNC(dt, ''' || V_DATE_TRUNC || ''') AS period_date,
            TO_CHAR(TRUNC(dt, ''' || V_DATE_TRUNC || '''), ''' || V_DATE_FORMAT || ''') AS period_label
        FROM (
            SELECT :start_date + LEVEL - 1 AS dt
            FROM DUAL
            CONNECT BY LEVEL <= (:end_date - :start_date + 1)
        )
        GROUP BY TRUNC(dt, ''' || V_DATE_TRUNC || ''')
    ),
    created_tickets AS (
        SELECT 
            TRUNC(t.CREATION_DATE, ''' || V_DATE_TRUNC || ''') AS period_date,
            COUNT(*) AS tickets_created,
            COUNT(CASE WHEN pr.PRIORITY_LEVEL = 1 THEN 1 END) AS critical_created,
            COUNT(CASE WHEN pr.PRIORITY_LEVEL = 2 THEN 1 END) AS high_created,
            AVG(pr.SLA_TARGET_HOURS) AS avg_sla_hours
        FROM SYS_REQUEST_TICKET t
        LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
        WHERE t.CREATION_DATE >= :start_date2 AND t.CREATION_DATE <= :end_date2
        AND t.IS_ACTIVE = ''Y''
        GROUP BY TRUNC(t.CREATION_DATE, ''' || V_DATE_TRUNC || ''')
    ),
    resolved_tickets AS (
        SELECT 
            TRUNC(t.ACTUAL_RESOLUTION_DATE, ''' || V_DATE_TRUNC || ''') AS period_date,
            COUNT(*) AS tickets_resolved,
            COUNT(CASE WHEN t.ACTUAL_RESOLUTION_DATE <= t.EXPECTED_RESOLUTION_DATE THEN 1 END) AS on_time_resolved,
            AVG((t.ACTUAL_RESOLUTION_DATE - t.CREATION_DATE) * 24) AS avg_resolution_hours
        FROM SYS_REQUEST_TICKET t
        WHERE t.ACTUAL_RESOLUTION_DATE >= :start_date3 AND t.ACTUAL_RESOLUTION_DATE <= :end_date3
        AND t.IS_ACTIVE = ''Y''
        GROUP BY TRUNC(t.ACTUAL_RESOLUTION_DATE, ''' || V_DATE_TRUNC || ''')
    )
    SELECT 
        dp.period_date,
        dp.period_label,
        COALESCE(ct.tickets_created, 0) AS tickets_created,
        COALESCE(rt.tickets_resolved, 0) AS tickets_resolved,
        COALESCE(ct.critical_created, 0) AS critical_created,
        COALESCE(ct.high_created, 0) AS high_created,
        COALESCE(rt.on_time_resolved, 0) AS on_time_resolved,
        ROUND(COALESCE(ct.avg_sla_hours, 0), 2) AS avg_sla_hours,
        ROUND(COALESCE(rt.avg_resolution_hours, 0), 2) AS avg_resolution_hours,
        ROUND(
            (COALESCE(rt.on_time_resolved, 0) * 100.0) / NULLIF(COALESCE(rt.tickets_resolved, 0), 0), 
            2
        ) AS sla_compliance_percentage,
        (COALESCE(ct.tickets_created, 0) - COALESCE(rt.tickets_resolved, 0)) AS net_ticket_change
    FROM date_periods dp
    LEFT JOIN created_tickets ct ON dp.period_date = ct.period_date
    LEFT JOIN resolved_tickets rt ON dp.period_date = rt.period_date
    ORDER BY dp.period_date';
    
    OPEN P_RESULT_CURSOR FOR V_SQL 
        USING P_START_DATE, P_END_DATE, P_START_DATE, P_END_DATE, P_START_DATE, P_END_DATE;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20905, 'Error generating trends report: ' || SQLERRM);
END SP_SYS_TICKET_REPORTS_TRENDS;
/

-- =============================================
-- SEED DATA AND UTILITY PROCEDURES
-- =============================================

-- =============================================
-- Procedure: SP_SYS_TICKET_SEED_DATA_INSERT
-- Description: Inserts additional seed data for ticket system
-- This procedure can be called to ensure all required seed data exists
-- Requirements: Task 1.3 - Add seed data for statuses, priorities, and default types
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_SEED_DATA_INSERT
AS
    V_COUNT NUMBER;
BEGIN
    -- Check and insert additional ticket categories if needed
    SELECT COUNT(*) INTO V_COUNT FROM SYS_TICKET_CATEGORY WHERE CATEGORY_NAME_EN = 'General';
    IF V_COUNT = 0 THEN
        INSERT INTO SYS_TICKET_CATEGORY (ROW_ID, CATEGORY_NAME_AR, CATEGORY_NAME_EN, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
        VALUES (SEQ_SYS_TICKET_CATEGORY.NEXTVAL, 'عام', 'General', 'فئة عامة للطلبات المتنوعة', 'General category for miscellaneous requests', 'system');
    END IF;
    
    SELECT COUNT(*) INTO V_COUNT FROM SYS_TICKET_CATEGORY WHERE CATEGORY_NAME_EN = 'Training';
    IF V_COUNT = 0 THEN
        INSERT INTO SYS_TICKET_CATEGORY (ROW_ID, CATEGORY_NAME_AR, CATEGORY_NAME_EN, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
        VALUES (SEQ_SYS_TICKET_CATEGORY.NEXTVAL, 'التدريب', 'Training', 'طلبات التدريب والتأهيل', 'Training and qualification requests', 'system');
    END IF;
    
    SELECT COUNT(*) INTO V_COUNT FROM SYS_TICKET_CATEGORY WHERE CATEGORY_NAME_EN = 'Integration';
    IF V_COUNT = 0 THEN
        INSERT INTO SYS_TICKET_CATEGORY (ROW_ID, CATEGORY_NAME_AR, CATEGORY_NAME_EN, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
        VALUES (SEQ_SYS_TICKET_CATEGORY.NEXTVAL, 'التكامل', 'Integration', 'طلبات التكامل مع الأنظمة الأخرى', 'Integration requests with other systems', 'system');
    END IF;
    
    -- Check and insert additional ticket types if needed
    SELECT COUNT(*) INTO V_COUNT FROM SYS_TICKET_TYPE WHERE TYPE_NAME_EN = 'Feature Request';
    IF V_COUNT = 0 THEN
        INSERT INTO SYS_TICKET_TYPE (ROW_ID, TYPE_NAME_AR, TYPE_NAME_EN, DESCRIPTION_AR, DESCRIPTION_EN, DEFAULT_PRIORITY_ID, SLA_TARGET_HOURS, CREATION_USER)
        VALUES (SEQ_SYS_TICKET_TYPE.NEXTVAL, 'طلب ميزة جديدة', 'Feature Request', 'طلبات إضافة ميزات جديدة للنظام', 'Requests for new system features', 
                (SELECT ROW_ID FROM SYS_TICKET_PRIORITY WHERE PRIORITY_LEVEL = 4), 96, 'system');
    END IF;
    
    SELECT COUNT(*) INTO V_COUNT FROM SYS_TICKET_TYPE WHERE TYPE_NAME_EN = 'Data Request';
    IF V_COUNT = 0 THEN
        INSERT INTO SYS_TICKET_TYPE (ROW_ID, TYPE_NAME_AR, TYPE_NAME_EN, DESCRIPTION_AR, DESCRIPTION_EN, DEFAULT_PRIORITY_ID, SLA_TARGET_HOURS, CREATION_USER)
        VALUES (SEQ_SYS_TICKET_TYPE.NEXTVAL, 'طلب بيانات', 'Data Request', 'طلبات استخراج البيانات والتقارير المخصصة', 'Data extraction and custom report requests', 
                (SELECT ROW_ID FROM SYS_TICKET_PRIORITY WHERE PRIORITY_LEVEL = 3), 48, 'system');
    END IF;
    
    SELECT COUNT(*) INTO V_COUNT FROM SYS_TICKET_TYPE WHERE TYPE_NAME_EN = 'System Maintenance';
    IF V_COUNT = 0 THEN
        INSERT INTO SYS_TICKET_TYPE (ROW_ID, TYPE_NAME_AR, TYPE_NAME_EN, DESCRIPTION_AR, DESCRIPTION_EN, DEFAULT_PRIORITY_ID, SLA_TARGET_HOURS, CREATION_USER)
        VALUES (SEQ_SYS_TICKET_TYPE.NEXTVAL, 'صيانة النظام', 'System Maintenance', 'طلبات صيانة النظام والتحديثات', 'System maintenance and update requests', 
                (SELECT ROW_ID FROM SYS_TICKET_PRIORITY WHERE PRIORITY_LEVEL = 2), 12, 'system');
    END IF;
    
    COMMIT;
    
    DBMS_OUTPUT.PUT_LINE('Seed data insertion completed successfully.');
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20906, 'Error inserting seed data: ' || SQLERRM);
END SP_SYS_TICKET_SEED_DATA_INSERT;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_SYSTEM_STATS
-- Description: Provides overall system statistics for dashboard
-- Returns: SYS_REFCURSOR with system-wide ticket statistics
-- Requirements: Dashboard and monitoring support
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_SYSTEM_STATS (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        -- Overall ticket counts
        (SELECT COUNT(*) FROM SYS_REQUEST_TICKET WHERE IS_ACTIVE = 'Y') AS total_tickets,
        (SELECT COUNT(*) FROM SYS_REQUEST_TICKET t 
         JOIN SYS_TICKET_STATUS s ON t.TICKET_STATUS_ID = s.ROW_ID 
         WHERE t.IS_ACTIVE = 'Y' AND s.STATUS_CODE = 'OPEN') AS open_tickets,
        (SELECT COUNT(*) FROM SYS_REQUEST_TICKET t 
         JOIN SYS_TICKET_STATUS s ON t.TICKET_STATUS_ID = s.ROW_ID 
         WHERE t.IS_ACTIVE = 'Y' AND s.STATUS_CODE = 'IN_PROGRESS') AS in_progress_tickets,
        (SELECT COUNT(*) FROM SYS_REQUEST_TICKET t 
         JOIN SYS_TICKET_STATUS s ON t.TICKET_STATUS_ID = s.ROW_ID 
         WHERE t.IS_ACTIVE = 'Y' AND s.STATUS_CODE = 'RESOLVED') AS resolved_tickets,
        (SELECT COUNT(*) FROM SYS_REQUEST_TICKET t 
         JOIN SYS_TICKET_STATUS s ON t.TICKET_STATUS_ID = s.ROW_ID 
         WHERE t.IS_ACTIVE = 'Y' AND s.STATUS_CODE IN ('CLOSED', 'CANCELLED')) AS closed_tickets,
        
        -- Priority breakdown
        (SELECT COUNT(*) FROM SYS_REQUEST_TICKET t 
         JOIN SYS_TICKET_PRIORITY p ON t.TICKET_PRIORITY_ID = p.ROW_ID 
         WHERE t.IS_ACTIVE = 'Y' AND p.PRIORITY_LEVEL = 1) AS critical_tickets,
        (SELECT COUNT(*) FROM SYS_REQUEST_TICKET t 
         JOIN SYS_TICKET_PRIORITY p ON t.TICKET_PRIORITY_ID = p.ROW_ID 
         WHERE t.IS_ACTIVE = 'Y' AND p.PRIORITY_LEVEL = 2) AS high_tickets,
        
        -- SLA status
        (SELECT COUNT(*) FROM SYS_REQUEST_TICKET t 
         JOIN SYS_TICKET_STATUS s ON t.TICKET_STATUS_ID = s.ROW_ID 
         WHERE t.IS_ACTIVE = 'Y' AND s.STATUS_CODE NOT IN ('CLOSED', 'CANCELLED', 'RESOLVED')
         AND SYSDATE > t.EXPECTED_RESOLUTION_DATE) AS overdue_tickets,
        
        -- Recent activity (last 24 hours)
        (SELECT COUNT(*) FROM SYS_REQUEST_TICKET 
         WHERE IS_ACTIVE = 'Y' AND CREATION_DATE >= SYSDATE - 1) AS tickets_created_today,
        (SELECT COUNT(*) FROM SYS_REQUEST_TICKET 
         WHERE IS_ACTIVE = 'Y' AND ACTUAL_RESOLUTION_DATE >= SYSDATE - 1) AS tickets_resolved_today,
        
        -- System configuration counts
        (SELECT COUNT(*) FROM SYS_TICKET_TYPE WHERE IS_ACTIVE = 'Y') AS active_ticket_types,
        (SELECT COUNT(*) FROM SYS_TICKET_CATEGORY WHERE IS_ACTIVE = 'Y') AS active_categories,
        (SELECT COUNT(*) FROM SYS_USERS WHERE IS_ACTIVE = '1' AND IS_ADMIN = '1') AS available_assignees,
        
        -- Average metrics
        (SELECT ROUND(AVG((ACTUAL_RESOLUTION_DATE - CREATION_DATE) * 24), 2) 
         FROM SYS_REQUEST_TICKET 
         WHERE IS_ACTIVE = 'Y' AND ACTUAL_RESOLUTION_DATE IS NOT NULL 
         AND CREATION_DATE >= SYSDATE - 30) AS avg_resolution_hours_30days,
        
        -- SLA compliance (last 30 days)
        (SELECT ROUND(
            (COUNT(CASE WHEN ACTUAL_RESOLUTION_DATE <= EXPECTED_RESOLUTION_DATE THEN 1 END) * 100.0) / 
            NULLIF(COUNT(*), 0), 2)
         FROM SYS_REQUEST_TICKET 
         WHERE IS_ACTIVE = 'Y' AND ACTUAL_RESOLUTION_DATE IS NOT NULL 
         AND CREATION_DATE >= SYSDATE - 30) AS sla_compliance_30days
    FROM DUAL;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20907, 'Error retrieving system statistics: ' || SQLERRM);
END SP_SYS_TICKET_SYSTEM_STATS;
/

-- Execute seed data insertion
BEGIN
    SP_SYS_TICKET_SEED_DATA_INSERT;
END;
/

-- =============================================
-- Verification: Display all created procedures
-- =============================================
SELECT object_name, object_type, status
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