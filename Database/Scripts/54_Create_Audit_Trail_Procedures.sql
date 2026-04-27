-- =====================================================
-- Audit Trail Stored Procedures for Ticket System
-- Provides comprehensive audit logging and retrieval
-- Validates Requirements 17.1-17.12
-- =====================================================

-- =====================================================
-- SP_SYS_AUDIT_LOG_INSERT
-- Inserts a new audit log entry
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_AUDIT_LOG_INSERT (
    p_correlation_id IN NVARCHAR2,
    p_actor_type IN NVARCHAR2,
    p_actor_id IN NUMBER,
    p_company_id IN NUMBER DEFAULT NULL,
    p_branch_id IN NUMBER DEFAULT NULL,
    p_action IN NVARCHAR2,
    p_entity_type IN NVARCHAR2,
    p_entity_id IN NUMBER DEFAULT NULL,
    p_ip_address IN NVARCHAR2 DEFAULT NULL,
    p_user_agent IN NVARCHAR2 DEFAULT NULL,
    p_severity IN NVARCHAR2 DEFAULT 'Info',
    p_event_category IN NVARCHAR2 DEFAULT 'DataChange',
    p_metadata IN CLOB DEFAULT NULL
)
AS
BEGIN
    INSERT INTO SYS_AUDIT_LOG (
        CORRELATION_ID,
        ACTOR_TYPE,
        ACTOR_ID,
        COMPANY_ID,
        BRANCH_ID,
        ACTION,
        ENTITY_TYPE,
        ENTITY_ID,
        IP_ADDRESS,
        USER_AGENT,
        SEVERITY,
        EVENT_CATEGORY,
        METADATA,
        CREATION_DATE
    ) VALUES (
        p_correlation_id,
        p_actor_type,
        p_actor_id,
        p_company_id,
        p_branch_id,
        p_action,
        p_entity_type,
        p_entity_id,
        p_ip_address,
        p_user_agent,
        p_severity,
        p_event_category,
        p_metadata,
        SYSDATE
    );
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE;
END SP_SYS_AUDIT_LOG_INSERT;
/

-- =====================================================
-- SP_SYS_AUDIT_LOG_SELECT_BY_TICKET
-- Retrieves audit trail for a specific ticket
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_AUDIT_LOG_SELECT_BY_TICKET (
    p_ticket_id IN NUMBER,
    p_from_date IN DATE DEFAULT NULL,
    p_to_date IN DATE DEFAULT NULL,
    p_action_filter IN NVARCHAR2 DEFAULT NULL,
    p_user_id_filter IN NUMBER DEFAULT NULL,
    p_result OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_result FOR
        SELECT 
            ROW_ID,
            CORRELATION_ID,
            ACTOR_TYPE,
            ACTOR_ID,
            COMPANY_ID,
            BRANCH_ID,
            ACTION,
            ENTITY_TYPE,
            ENTITY_ID,
            IP_ADDRESS,
            USER_AGENT,
            SEVERITY,
            EVENT_CATEGORY,
            METADATA,
            CREATION_DATE
        FROM SYS_AUDIT_LOG
        WHERE ENTITY_TYPE = 'Ticket'
          AND ENTITY_ID = p_ticket_id
          AND (p_from_date IS NULL OR CREATION_DATE >= p_from_date)
          AND (p_to_date IS NULL OR CREATION_DATE <= p_to_date)
          AND (p_action_filter IS NULL OR ACTION = p_action_filter)
          AND (p_user_id_filter IS NULL OR ACTOR_ID = p_user_id_filter)
        ORDER BY CREATION_DATE DESC;
END SP_SYS_AUDIT_LOG_SELECT_BY_TICKET;
/

-- =====================================================
-- SP_SYS_AUDIT_LOG_SEARCH
-- Advanced search for audit trail with pagination
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_AUDIT_LOG_SEARCH (
    p_entity_type IN NVARCHAR2 DEFAULT NULL,
    p_entity_id IN NUMBER DEFAULT NULL,
    p_user_id IN NUMBER DEFAULT NULL,
    p_company_id IN NUMBER DEFAULT NULL,
    p_branch_id IN NUMBER DEFAULT NULL,
    p_action IN NVARCHAR2 DEFAULT NULL,
    p_from_date IN DATE DEFAULT NULL,
    p_to_date IN DATE DEFAULT NULL,
    p_severity IN NVARCHAR2 DEFAULT NULL,
    p_event_category IN NVARCHAR2 DEFAULT NULL,
    p_page IN NUMBER DEFAULT 1,
    p_page_size IN NUMBER DEFAULT 50,
    p_total_count OUT NUMBER,
    p_result OUT SYS_REFCURSOR
)
AS
    v_offset NUMBER;
BEGIN
    -- Calculate offset for pagination
    v_offset := (p_page - 1) * p_page_size;
    
    -- Get total count
    SELECT COUNT(*)
    INTO p_total_count
    FROM SYS_AUDIT_LOG
    WHERE (p_entity_type IS NULL OR ENTITY_TYPE = p_entity_type)
      AND (p_entity_id IS NULL OR ENTITY_ID = p_entity_id)
      AND (p_user_id IS NULL OR ACTOR_ID = p_user_id)
      AND (p_company_id IS NULL OR COMPANY_ID = p_company_id)
      AND (p_branch_id IS NULL OR BRANCH_ID = p_branch_id)
      AND (p_action IS NULL OR ACTION = p_action)
      AND (p_from_date IS NULL OR CREATION_DATE >= p_from_date)
      AND (p_to_date IS NULL OR CREATION_DATE <= p_to_date)
      AND (p_severity IS NULL OR SEVERITY = p_severity)
      AND (p_event_category IS NULL OR EVENT_CATEGORY = p_event_category);
    
    -- Get paginated results
    OPEN p_result FOR
        SELECT * FROM (
            SELECT 
                ROW_ID,
                CORRELATION_ID,
                ACTOR_TYPE,
                ACTOR_ID,
                COMPANY_ID,
                BRANCH_ID,
                ACTION,
                ENTITY_TYPE,
                ENTITY_ID,
                IP_ADDRESS,
                USER_AGENT,
                SEVERITY,
                EVENT_CATEGORY,
                METADATA,
                CREATION_DATE,
                ROW_NUMBER() OVER (ORDER BY CREATION_DATE DESC) AS RN
            FROM SYS_AUDIT_LOG
            WHERE (p_entity_type IS NULL OR ENTITY_TYPE = p_entity_type)
              AND (p_entity_id IS NULL OR ENTITY_ID = p_entity_id)
              AND (p_user_id IS NULL OR ACTOR_ID = p_user_id)
              AND (p_company_id IS NULL OR COMPANY_ID = p_company_id)
              AND (p_branch_id IS NULL OR BRANCH_ID = p_branch_id)
              AND (p_action IS NULL OR ACTION = p_action)
              AND (p_from_date IS NULL OR CREATION_DATE >= p_from_date)
              AND (p_to_date IS NULL OR CREATION_DATE <= p_to_date)
              AND (p_severity IS NULL OR SEVERITY = p_severity)
              AND (p_event_category IS NULL OR EVENT_CATEGORY = p_event_category)
        )
        WHERE RN > v_offset AND RN <= v_offset + p_page_size;
END SP_SYS_AUDIT_LOG_SEARCH;
/

-- =====================================================
-- SP_SYS_AUDIT_LOG_SELECT_BY_CORRELATION
-- Retrieves all audit entries for a correlation ID
-- Useful for tracing a complete request through the system
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_AUDIT_LOG_SELECT_BY_CORRELATION (
    p_correlation_id IN NVARCHAR2,
    p_result OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_result FOR
        SELECT 
            ROW_ID,
            CORRELATION_ID,
            ACTOR_TYPE,
            ACTOR_ID,
            COMPANY_ID,
            BRANCH_ID,
            ACTION,
            ENTITY_TYPE,
            ENTITY_ID,
            IP_ADDRESS,
            USER_AGENT,
            SEVERITY,
            EVENT_CATEGORY,
            METADATA,
            CREATION_DATE
        FROM SYS_AUDIT_LOG
        WHERE CORRELATION_ID = p_correlation_id
        ORDER BY CREATION_DATE ASC;
END SP_SYS_AUDIT_LOG_SELECT_BY_CORRELATION;
/

-- =====================================================
-- SP_SYS_AUDIT_LOG_SELECT_SECURITY_EVENTS
-- Retrieves security-related audit events
-- Includes authorization failures and suspicious activities
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_AUDIT_LOG_SELECT_SECURITY_EVENTS (
    p_from_date IN DATE DEFAULT NULL,
    p_to_date IN DATE DEFAULT NULL,
    p_company_id IN NUMBER DEFAULT NULL,
    p_severity IN NVARCHAR2 DEFAULT NULL,
    p_page IN NUMBER DEFAULT 1,
    p_page_size IN NUMBER DEFAULT 50,
    p_total_count OUT NUMBER,
    p_result OUT SYS_REFCURSOR
)
AS
    v_offset NUMBER;
BEGIN
    -- Calculate offset for pagination
    v_offset := (p_page - 1) * p_page_size;
    
    -- Get total count
    SELECT COUNT(*)
    INTO p_total_count
    FROM SYS_AUDIT_LOG
    WHERE (EVENT_CATEGORY = 'Permission' OR SEVERITY IN ('Warning', 'Critical', 'Error'))
      AND (p_from_date IS NULL OR CREATION_DATE >= p_from_date)
      AND (p_to_date IS NULL OR CREATION_DATE <= p_to_date)
      AND (p_company_id IS NULL OR COMPANY_ID = p_company_id)
      AND (p_severity IS NULL OR SEVERITY = p_severity);
    
    -- Get paginated results
    OPEN p_result FOR
        SELECT * FROM (
            SELECT 
                ROW_ID,
                CORRELATION_ID,
                ACTOR_TYPE,
                ACTOR_ID,
                COMPANY_ID,
                BRANCH_ID,
                ACTION,
                ENTITY_TYPE,
                ENTITY_ID,
                IP_ADDRESS,
                USER_AGENT,
                SEVERITY,
                EVENT_CATEGORY,
                METADATA,
                CREATION_DATE,
                ROW_NUMBER() OVER (ORDER BY CREATION_DATE DESC) AS RN
            FROM SYS_AUDIT_LOG
            WHERE (EVENT_CATEGORY = 'Permission' OR SEVERITY IN ('Warning', 'Critical', 'Error'))
              AND (p_from_date IS NULL OR CREATION_DATE >= p_from_date)
              AND (p_to_date IS NULL OR CREATION_DATE <= p_to_date)
              AND (p_company_id IS NULL OR COMPANY_ID = p_company_id)
              AND (p_severity IS NULL OR SEVERITY = p_severity)
        )
        WHERE RN > v_offset AND RN <= v_offset + p_page_size;
END SP_SYS_AUDIT_LOG_SELECT_SECURITY_EVENTS;
/

-- =====================================================
-- SP_SYS_AUDIT_LOG_GET_STATISTICS
-- Retrieves audit log statistics for reporting
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_AUDIT_LOG_GET_STATISTICS (
    p_from_date IN DATE DEFAULT NULL,
    p_to_date IN DATE DEFAULT NULL,
    p_company_id IN NUMBER DEFAULT NULL,
    p_result OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_result FOR
        SELECT 
            EVENT_CATEGORY,
            ACTION,
            SEVERITY,
            COUNT(*) AS EVENT_COUNT,
            COUNT(DISTINCT ACTOR_ID) AS UNIQUE_USERS,
            COUNT(DISTINCT COMPANY_ID) AS UNIQUE_COMPANIES,
            MIN(CREATION_DATE) AS FIRST_EVENT,
            MAX(CREATION_DATE) AS LAST_EVENT
        FROM SYS_AUDIT_LOG
        WHERE (p_from_date IS NULL OR CREATION_DATE >= p_from_date)
          AND (p_to_date IS NULL OR CREATION_DATE <= p_to_date)
          AND (p_company_id IS NULL OR COMPANY_ID = p_company_id)
        GROUP BY EVENT_CATEGORY, ACTION, SEVERITY
        ORDER BY EVENT_COUNT DESC;
END SP_SYS_AUDIT_LOG_GET_STATISTICS;
/

-- =====================================================
-- SP_SYS_AUDIT_LOG_GET_USER_ACTIVITY
-- Retrieves audit trail for a specific user
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_AUDIT_LOG_GET_USER_ACTIVITY (
    p_user_id IN NUMBER,
    p_from_date IN DATE DEFAULT NULL,
    p_to_date IN DATE DEFAULT NULL,
    p_action_filter IN NVARCHAR2 DEFAULT NULL,
    p_page IN NUMBER DEFAULT 1,
    p_page_size IN NUMBER DEFAULT 50,
    p_total_count OUT NUMBER,
    p_result OUT SYS_REFCURSOR
)
AS
    v_offset NUMBER;
BEGIN
    -- Calculate offset for pagination
    v_offset := (p_page - 1) * p_page_size;
    
    -- Get total count
    SELECT COUNT(*)
    INTO p_total_count
    FROM SYS_AUDIT_LOG
    WHERE ACTOR_ID = p_user_id
      AND (p_from_date IS NULL OR CREATION_DATE >= p_from_date)
      AND (p_to_date IS NULL OR CREATION_DATE <= p_to_date)
      AND (p_action_filter IS NULL OR ACTION = p_action_filter);
    
    -- Get paginated results
    OPEN p_result FOR
        SELECT * FROM (
            SELECT 
                ROW_ID,
                CORRELATION_ID,
                ACTOR_TYPE,
                ACTOR_ID,
                COMPANY_ID,
                BRANCH_ID,
                ACTION,
                ENTITY_TYPE,
                ENTITY_ID,
                IP_ADDRESS,
                USER_AGENT,
                SEVERITY,
                EVENT_CATEGORY,
                METADATA,
                CREATION_DATE,
                ROW_NUMBER() OVER (ORDER BY CREATION_DATE DESC) AS RN
            FROM SYS_AUDIT_LOG
            WHERE ACTOR_ID = p_user_id
              AND (p_from_date IS NULL OR CREATION_DATE >= p_from_date)
              AND (p_to_date IS NULL OR CREATION_DATE <= p_to_date)
              AND (p_action_filter IS NULL OR ACTION = p_action_filter)
        )
        WHERE RN > v_offset AND RN <= v_offset + p_page_size;
END SP_SYS_AUDIT_LOG_GET_USER_ACTIVITY;
/

-- =====================================================
-- Grant execute permissions
-- =====================================================
-- GRANT EXECUTE ON SP_SYS_AUDIT_LOG_INSERT TO your_app_user;
-- GRANT EXECUTE ON SP_SYS_AUDIT_LOG_SELECT_BY_TICKET TO your_app_user;
-- GRANT EXECUTE ON SP_SYS_AUDIT_LOG_SEARCH TO your_app_user;
-- GRANT EXECUTE ON SP_SYS_AUDIT_LOG_SELECT_BY_CORRELATION TO your_app_user;
-- GRANT EXECUTE ON SP_SYS_AUDIT_LOG_SELECT_SECURITY_EVENTS TO your_app_user;
-- GRANT EXECUTE ON SP_SYS_AUDIT_LOG_GET_STATISTICS TO your_app_user;
-- GRANT EXECUTE ON SP_SYS_AUDIT_LOG_GET_USER_ACTIVITY TO your_app_user;

COMMIT;
