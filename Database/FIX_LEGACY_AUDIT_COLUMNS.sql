-- =====================================================
-- FIX: Legacy Audit Column Mismatch
-- =====================================================
-- This script fixes the column mismatch in SP_SYS_AUDIT_LOG_LEGACY_SELECT
-- The service expects: ACTOR_NAME, ENDPOINT_PATH, USER_AGENT, IP_ADDRESS, CORRELATION_ID
-- But the procedure was returning: USER_NAME and missing the other columns
-- =====================================================

PROMPT =====================================================
PROMPT Fixing Legacy Audit Log Column Mismatch
PROMPT =====================================================

-- Update the procedure to return all required columns
CREATE OR REPLACE PROCEDURE SP_SYS_AUDIT_LOG_LEGACY_SELECT (
    p_company_id IN NUMBER DEFAULT NULL,
    p_business_module IN VARCHAR2 DEFAULT NULL,
    p_branch_id IN NUMBER DEFAULT NULL,
    p_status IN VARCHAR2 DEFAULT NULL,
    p_start_date IN DATE DEFAULT NULL,
    p_end_date IN DATE DEFAULT NULL,
    p_search_term IN VARCHAR2 DEFAULT NULL,
    p_page_number IN NUMBER DEFAULT 1,
    p_page_size IN NUMBER DEFAULT 50,
    p_total_count OUT NUMBER,
    p_result OUT SYS_REFCURSOR
)
AS
    v_where_clause VARCHAR2(4000) := '';
    v_sql CLOB;
    v_count_sql CLOB;
    v_offset NUMBER;
BEGIN
    -- Calculate offset for pagination
    v_offset := (p_page_number - 1) * p_page_size;
    
    -- Build WHERE clause dynamically based on provided filters
    IF p_company_id IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND a.COMPANY_ID = ' || p_company_id;
    END IF;
    
    IF p_business_module IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND a.BUSINESS_MODULE = ''' || p_business_module || '''';
    END IF;
    
    IF p_branch_id IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND a.BRANCH_ID = ' || p_branch_id;
    END IF;
    
    IF p_status IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND st.STATUS = ''' || p_status || '''';
    END IF;
    
    -- Use proper date comparison instead of string concatenation
    IF p_start_date IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND a.CREATION_DATE >= TO_DATE(''' || TO_CHAR(p_start_date, 'YYYY-MM-DD HH24:MI:SS') || ''', ''YYYY-MM-DD HH24:MI:SS'')';
    END IF;
    
    IF p_end_date IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND a.CREATION_DATE <= TO_DATE(''' || TO_CHAR(p_end_date, 'YYYY-MM-DD HH24:MI:SS') || ''', ''YYYY-MM-DD HH24:MI:SS'')';
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
    -- IMPORTANT: Return ACTOR_NAME (not USER_NAME) and include all required columns
    v_sql := 'SELECT * FROM (
        SELECT 
            a.ROW_ID,
            a.BUSINESS_MODULE,
            a.DEVICE_IDENTIFIER,
            a.ERROR_CODE,
            a.BUSINESS_DESCRIPTION,
            a.CREATION_DATE,
            c.ROW_DESC AS COMPANY_NAME,
            c.ROW_DESC_E AS COMPANY_NAME_E,
            b.ROW_DESC AS BRANCH_NAME,
            b.ROW_DESC_E AS BRANCH_NAME_E,
            COALESCE(u.USER_NAME, ''System'') AS ACTOR_NAME,
            u.ROW_DESC AS USER_FULL_NAME,
            u.ROW_DESC_E AS USER_FULL_NAME_E,
            COALESCE(st.STATUS, 
                CASE 
                    WHEN a.SEVERITY = ''Critical'' THEN ''Critical''
                    WHEN a.SEVERITY = ''Error'' THEN ''Unresolved''
                    WHEN a.SEVERITY = ''Warning'' AND a.EVENT_CATEGORY = ''Permission'' THEN ''Unresolved''
                    ELSE ''Resolved''
                END
            ) AS STATUS,
            st.STATUS_CHANGED_DATE,
            st.ASSIGNED_TO_USER_ID,
            st.RESOLUTION_NOTES,
            a.EXCEPTION_TYPE,
            a.EXCEPTION_MESSAGE,
            a.SEVERITY,
            a.ACTOR_TYPE,
            a.ACTION,
            a.ENTITY_TYPE,
            a.ENTITY_ID,
            a.CORRELATION_ID,
            a.ENDPOINT_PATH,
            a.USER_AGENT,
            a.IP_ADDRESS,
            a.EVENT_CATEGORY,
            a.METADATA,
            ROW_NUMBER() OVER (ORDER BY a.CREATION_DATE DESC) AS RN
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

PROMPT ✓ Updated SP_SYS_AUDIT_LOG_LEGACY_SELECT with all required columns
PROMPT

-- Verify the procedure was created successfully
PROMPT Verifying procedure:
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name = 'SP_SYS_AUDIT_LOG_LEGACY_SELECT'
ORDER BY object_name;

PROMPT
PROMPT =====================================================
PROMPT Fix completed successfully!
PROMPT =====================================================
PROMPT
PROMPT The procedure now returns:
PROMPT - ACTOR_NAME (instead of USER_NAME)
PROMPT - CORRELATION_ID
PROMPT - ENDPOINT_PATH
PROMPT - USER_AGENT
PROMPT - IP_ADDRESS
PROMPT - All other required columns
PROMPT
PROMPT =====================================================
