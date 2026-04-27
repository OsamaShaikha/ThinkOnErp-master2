-- =====================================================
-- Update Legacy Audit Search Functionality
-- Task 5.6: Implement search functionality (matches logs.png search)
-- =====================================================
-- This script updates SP_SYS_AUDIT_LOG_LEGACY_SELECT to include BUSINESS_MODULE in search
-- Search now works across: Error Description, User, Device, Error Code, Module, and Exception Message

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
    
    -- Enhanced search functionality - now includes BUSINESS_MODULE
    -- Searches across: Error Description, User, Device, Error Code, Module, and Exception Message
    IF p_search_term IS NOT NULL THEN
        v_where_clause := v_where_clause || ' AND (
            UPPER(a.BUSINESS_DESCRIPTION) LIKE UPPER(''%' || p_search_term || '%'') OR
            UPPER(a.ERROR_CODE) LIKE UPPER(''%' || p_search_term || '%'') OR
            UPPER(u.USER_NAME) LIKE UPPER(''%' || p_search_term || '%'') OR
            UPPER(a.DEVICE_IDENTIFIER) LIKE UPPER(''%' || p_search_term || '%'') OR
            UPPER(a.BUSINESS_MODULE) LIKE UPPER(''%' || p_search_term || '%'') OR
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

COMMIT;
