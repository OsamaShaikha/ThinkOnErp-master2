-- =====================================================
-- Legacy Audit Service Stored Procedures
-- Supports backward compatibility with logs.png interface
-- =====================================================

-- =====================================================
-- SP_SYS_AUDIT_LOG_LEGACY_SELECT
-- Retrieves audit logs in legacy format with business-friendly data
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_AUDIT_LOG_LEGACY_SELECT (
    p_company IN NVARCHAR2 DEFAULT NULL,
    p_module IN NVARCHAR2 DEFAULT NULL,
    p_branch IN NVARCHAR2 DEFAULT NULL,
    p_status IN NVARCHAR2 DEFAULT NULL,
    p_start_date IN DATE DEFAULT NULL,
    p_end_date IN DATE DEFAULT NULL,
    p_search_term IN NVARCHAR2 DEFAULT NULL,
    p_page_number IN NUMBER DEFAULT 1,
    p_page_size IN NUMBER DEFAULT 50,
    p_total_count OUT NUMBER,
    p_result OUT SYS_REFCURSOR
) AS
    v_offset NUMBER;
    v_sql NVARCHAR2(4000);
    v_where_clause NVARCHAR2(2000) := '';
    v_count_sql NVARCHAR2(4000);
BEGIN
    -- Calculate offset for pagination
    v_offset := (p_page_number - 1) * p_page_size;
    
    -- Build WHERE clause based on filters
    IF p_company IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND (c.COMPANY_NAME LIKE ''%' || p_company || '%'' OR c.COMPANY_NAME IS NULL)';
    END IF;
    
    IF p_module IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND (a.BUSINESS_MODULE = ''' || p_module || ''' OR a.BUSINESS_MODULE IS NULL)';
    END IF;
    
    IF p_branch IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND (b.BRANCH_NAME LIKE ''%' || p_branch || '%'' OR b.BRANCH_NAME IS NULL)';
    END IF;
    
    IF p_status IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND (st.STATUS = ''' || p_status || ''' OR (st.STATUS IS NULL AND ''' || p_status || ''' = ''Unresolved''))';
    END IF;
    
    IF p_start_date IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND a.CREATION_DATE >= ''' || TO_CHAR(p_start_date, 'YYYY-MM-DD') || '''';
    END IF;
    
    IF p_end_date IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND a.CREATION_DATE <= ''' || TO_CHAR(p_end_date, 'YYYY-MM-DD') || ' 23:59:59''';
    END IF;
    
    IF p_search_term IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND (
            UPPER(a.BUSINESS_DESCRIPTION) LIKE UPPER(''%' || p_search_term || '%'') OR
            UPPER(a.ERROR_CODE) LIKE UPPER(''%' || p_search_term || '%'') OR
            UPPER(u.USER_NAME) LIKE UPPER(''%' || p_search_term || '%'') OR
            UPPER(a.DEVICE_IDENTIFIER) LIKE UPPER(''%' || p_search_term || '%'') OR
            UPPER(a.EXCEPTION_MESSAGE) LIKE UPPER(''%' || p_search_term || '%'')
        )';
    END IF;
    
    -- Remove leading ' AND' from where clause
    IF LENGTH(v_where_clause) > 0 THEN
        v_where_clause := SUBSTR(v_where_clause, 5);
        v_where_clause := ' WHERE ' || v_where_clause;
    END IF;
    
    -- Build count query
    v_count_sql := 'SELECT COUNT(*) FROM SYS_AUDIT_LOG a
        LEFT JOIN SYS_COMPANY c ON a.COMPANY_ID = c.ROW_ID
        LEFT JOIN SYS_BRANCH b ON a.BRANCH_ID = b.ROW_ID
        LEFT JOIN SYS_USERS u ON a.ACTOR_ID = u.ROW_ID AND a.ACTOR_TYPE = ''USER''
        LEFT JOIN (
            SELECT AUDIT_LOG_ID, STATUS, ROW_NUMBER() OVER (PARTITION BY AUDIT_LOG_ID ORDER BY STATUS_CHANGED_DATE DESC) as rn
            FROM SYS_AUDIT_STATUS_TRACKING
        ) st_ranked ON a.ROW_ID = st_ranked.AUDIT_LOG_ID AND st_ranked.rn = 1
        LEFT JOIN SYS_AUDIT_STATUS_TRACKING st ON st_ranked.AUDIT_LOG_ID = st.AUDIT_LOG_ID AND st_ranked.STATUS = st.STATUS'
        || v_where_clause;
    
    -- Execute count query
    EXECUTE IMMEDIATE v_count_sql INTO p_total_count;
    
    -- Build main query with pagination
    v_sql := 'SELECT * FROM (
        SELECT 
            a.ROW_ID,
            a.BUSINESS_DESCRIPTION,
            a.BUSINESS_MODULE,
            COALESCE(c.COMPANY_NAME, ''Unknown'') as COMPANY_NAME,
            COALESCE(b.BRANCH_NAME, ''Unknown'') as BRANCH_NAME,
            COALESCE(u.USER_NAME, ''System'') as ACTOR_NAME,
            a.DEVICE_IDENTIFIER,
            a.CREATION_DATE,
            COALESCE(st.STATUS, 
                CASE 
                    WHEN a.SEVERITY = ''Critical'' THEN ''Critical''
                    WHEN a.SEVERITY = ''Error'' THEN ''Unresolved''
                    WHEN a.SEVERITY = ''Warning'' AND a.EVENT_CATEGORY = ''Permission'' THEN ''Unresolved''
                    ELSE ''Resolved''
                END
            ) as STATUS,
            a.ERROR_CODE,
            a.CORRELATION_ID,
            a.ENTITY_TYPE,
            a.ENDPOINT_PATH,
            a.USER_AGENT,
            a.IP_ADDRESS,
            a.EXCEPTION_TYPE,
            a.EXCEPTION_MESSAGE,
            a.SEVERITY,
            a.EVENT_CATEGORY,
            a.METADATA,
            a.ACTION,
            ROW_NUMBER() OVER (ORDER BY a.CREATION_DATE DESC) as RN
        FROM SYS_AUDIT_LOG a
        LEFT JOIN SYS_COMPANY c ON a.COMPANY_ID = c.ROW_ID
        LEFT JOIN SYS_BRANCH b ON a.BRANCH_ID = b.ROW_ID
        LEFT JOIN SYS_USERS u ON a.ACTOR_ID = u.ROW_ID AND a.ACTOR_TYPE = ''USER''
        LEFT JOIN (
            SELECT AUDIT_LOG_ID, STATUS, ROW_NUMBER() OVER (PARTITION BY AUDIT_LOG_ID ORDER BY STATUS_CHANGED_DATE DESC) as rn
            FROM SYS_AUDIT_STATUS_TRACKING
        ) st_ranked ON a.ROW_ID = st_ranked.AUDIT_LOG_ID AND st_ranked.rn = 1
        LEFT JOIN SYS_AUDIT_STATUS_TRACKING st ON st_ranked.AUDIT_LOG_ID = st.AUDIT_LOG_ID AND st_ranked.STATUS = st.STATUS'
        || v_where_clause || '
    ) WHERE RN > ' || v_offset || ' AND RN <= ' || (v_offset + p_page_size);
    
    -- Open cursor with results
    OPEN p_result FOR v_sql;
    
EXCEPTION
    WHEN OTHERS THEN
        p_total_count := 0;
        OPEN p_result FOR SELECT NULL FROM DUAL WHERE 1=0;
        RAISE;
END SP_SYS_AUDIT_LOG_LEGACY_SELECT;
/

-- =====================================================
-- SP_SYS_AUDIT_LOG_STATUS_COUNTERS
-- Retrieves status-based counters for legacy dashboard
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_AUDIT_LOG_STATUS_COUNTERS (
    p_unresolved_count OUT NUMBER,
    p_in_progress_count OUT NUMBER,
    p_resolved_count OUT NUMBER,
    p_critical_count OUT NUMBER
) AS
BEGIN
    -- Get counts from status tracking table with fallback logic
    SELECT 
        SUM(CASE WHEN COALESCE(st.STATUS, 
            CASE 
                WHEN a.SEVERITY = 'Critical' THEN 'Critical'
                WHEN a.SEVERITY = 'Error' THEN 'Unresolved'
                WHEN a.SEVERITY = 'Warning' AND a.EVENT_CATEGORY = 'Permission' THEN 'Unresolved'
                ELSE 'Resolved'
            END
        ) = 'Unresolved' THEN 1 ELSE 0 END),
        
        SUM(CASE WHEN COALESCE(st.STATUS, 
            CASE 
                WHEN a.SEVERITY = 'Critical' THEN 'Critical'
                WHEN a.SEVERITY = 'Error' THEN 'Unresolved'
                WHEN a.SEVERITY = 'Warning' AND a.EVENT_CATEGORY = 'Permission' THEN 'Unresolved'
                ELSE 'Resolved'
            END
        ) = 'In Progress' THEN 1 ELSE 0 END),
        
        SUM(CASE WHEN COALESCE(st.STATUS, 
            CASE 
                WHEN a.SEVERITY = 'Critical' THEN 'Critical'
                WHEN a.SEVERITY = 'Error' THEN 'Unresolved'
                WHEN a.SEVERITY = 'Warning' AND a.EVENT_CATEGORY = 'Permission' THEN 'Unresolved'
                ELSE 'Resolved'
            END
        ) = 'Resolved' THEN 1 ELSE 0 END),
        
        SUM(CASE WHEN COALESCE(st.STATUS, 
            CASE 
                WHEN a.SEVERITY = 'Critical' THEN 'Critical'
                WHEN a.SEVERITY = 'Error' THEN 'Unresolved'
                WHEN a.SEVERITY = 'Warning' AND a.EVENT_CATEGORY = 'Permission' THEN 'Unresolved'
                ELSE 'Resolved'
            END
        ) = 'Critical' THEN 1 ELSE 0 END)
    INTO p_unresolved_count, p_in_progress_count, p_resolved_count, p_critical_count
    FROM SYS_AUDIT_LOG a
    LEFT JOIN (
        SELECT AUDIT_LOG_ID, STATUS, ROW_NUMBER() OVER (PARTITION BY AUDIT_LOG_ID ORDER BY STATUS_CHANGED_DATE DESC) as rn
        FROM SYS_AUDIT_STATUS_TRACKING
    ) st_ranked ON a.ROW_ID = st_ranked.AUDIT_LOG_ID AND st_ranked.rn = 1
    LEFT JOIN SYS_AUDIT_STATUS_TRACKING st ON st_ranked.AUDIT_LOG_ID = st.AUDIT_LOG_ID AND st_ranked.STATUS = st.STATUS
    WHERE a.CREATION_DATE >= SYSDATE - 30; -- Last 30 days
    
    -- Ensure we have valid counts (not NULL)
    p_unresolved_count := COALESCE(p_unresolved_count, 0);
    p_in_progress_count := COALESCE(p_in_progress_count, 0);
    p_resolved_count := COALESCE(p_resolved_count, 0);
    p_critical_count := COALESCE(p_critical_count, 0);
    
EXCEPTION
    WHEN OTHERS THEN
        p_unresolved_count := 0;
        p_in_progress_count := 0;
        p_resolved_count := 0;
        p_critical_count := 0;
        RAISE;
END SP_SYS_AUDIT_LOG_STATUS_COUNTERS;
/

-- =====================================================
-- SP_SYS_AUDIT_STATUS_UPDATE
-- Updates the status of an audit log entry
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_AUDIT_STATUS_UPDATE (
    p_audit_log_id IN NUMBER,
    p_status IN NVARCHAR2,
    p_resolution_notes IN NVARCHAR2 DEFAULT NULL,
    p_assigned_to_user_id IN NUMBER DEFAULT NULL,
    p_status_changed_by IN NUMBER
) AS
    v_count NUMBER;
BEGIN
    -- Validate that the audit log entry exists
    SELECT COUNT(*) INTO v_count FROM SYS_AUDIT_LOG WHERE ROW_ID = p_audit_log_id;
    
    IF v_count = 0 THEN
        RAISE_APPLICATION_ERROR(-20001, 'Audit log entry not found: ' || p_audit_log_id);
    END IF;
    
    -- Validate status value
    IF p_status NOT IN ('Unresolved', 'In Progress', 'Resolved', 'Critical') THEN
        RAISE_APPLICATION_ERROR(-20002, 'Invalid status value: ' || p_status);
    END IF;
    
    -- Insert new status tracking record
    INSERT INTO SYS_AUDIT_STATUS_TRACKING (
        ROW_ID,
        AUDIT_LOG_ID,
        STATUS,
        ASSIGNED_TO_USER_ID,
        RESOLUTION_NOTES,
        STATUS_CHANGED_BY,
        STATUS_CHANGED_DATE
    ) VALUES (
        SEQ_SYS_AUDIT_STATUS_TRACKING.NEXTVAL,
        p_audit_log_id,
        p_status,
        p_assigned_to_user_id,
        p_resolution_notes,
        p_status_changed_by,
        SYSDATE
    );
    
    COMMIT;
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE;
END SP_SYS_AUDIT_STATUS_UPDATE;
/

-- =====================================================
-- SP_SYS_AUDIT_STATUS_GET_CURRENT
-- Gets the current status of an audit log entry
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_SYS_AUDIT_STATUS_GET_CURRENT (
    p_audit_log_id IN NUMBER,
    p_status OUT NVARCHAR2
) AS
    v_count NUMBER;
BEGIN
    -- Try to get the latest status from status tracking table
    SELECT STATUS INTO p_status
    FROM (
        SELECT STATUS, ROW_NUMBER() OVER (ORDER BY STATUS_CHANGED_DATE DESC) as rn
        FROM SYS_AUDIT_STATUS_TRACKING
        WHERE AUDIT_LOG_ID = p_audit_log_id
    )
    WHERE rn = 1;
    
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        -- Fallback: determine status based on audit log severity and category
        SELECT 
            CASE 
                WHEN SEVERITY = 'Critical' THEN 'Critical'
                WHEN SEVERITY = 'Error' THEN 'Unresolved'
                WHEN SEVERITY = 'Warning' AND EVENT_CATEGORY = 'Permission' THEN 'Unresolved'
                ELSE 'Resolved'
            END
        INTO p_status
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID = p_audit_log_id;
        
        -- If still no data found, return default
        IF p_status IS NULL THEN
            p_status := 'Unresolved';
        END IF;
        
    WHEN OTHERS THEN
        p_status := 'Unresolved';
        RAISE;
END SP_SYS_AUDIT_STATUS_GET_CURRENT;
/

-- =====================================================
-- Create sequence for status tracking if it doesn't exist
-- =====================================================
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM USER_SEQUENCES WHERE SEQUENCE_NAME = 'SEQ_SYS_AUDIT_STATUS_TRACKING';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_SYS_AUDIT_STATUS_TRACKING START WITH 1 INCREMENT BY 1 NOCACHE';
    END IF;
END;
/

-- =====================================================
-- Grant execute permissions
-- =====================================================
-- GRANT EXECUTE ON SP_SYS_AUDIT_LOG_LEGACY_SELECT TO your_app_user;
-- GRANT EXECUTE ON SP_SYS_AUDIT_LOG_STATUS_COUNTERS TO your_app_user;
-- GRANT EXECUTE ON SP_SYS_AUDIT_STATUS_UPDATE TO your_app_user;
-- GRANT EXECUTE ON SP_SYS_AUDIT_STATUS_GET_CURRENT TO your_app_user;

COMMIT;