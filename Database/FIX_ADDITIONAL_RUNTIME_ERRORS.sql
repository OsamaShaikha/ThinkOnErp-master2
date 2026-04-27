-- =====================================================
-- Fix Additional Runtime Errors
-- Description: Fixes two runtime errors discovered after initial fixes
-- 1. Company repository column mismatch (HAS_LOGO)
-- 2. Legacy audit log date format error
-- Date: 2026-05-05
-- =====================================================

SET SERVEROUTPUT ON SIZE UNLIMITED
SET ECHO ON
SET FEEDBACK ON

PROMPT =====================================================
PROMPT Starting Additional Runtime Error Fixes
PROMPT =====================================================
PROMPT

-- =====================================================
-- FIX 1: Ensure Company Procedures Return HAS_LOGO Column
-- =====================================================
PROMPT =====================================================
PROMPT FIX 1: Updating Company Procedures to Include HAS_LOGO
PROMPT =====================================================

-- This procedure should already exist from script 55, but we'll recreate it to be sure
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        LEGAL_NAME,
        LEGAL_NAME_E,
        COMPANY_CODE,
        TAX_NUMBER,
        COUNTRY_ID,
        CURR_ID,
        DEFAULT_BRANCH_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        CASE 
            WHEN COMPANY_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_COMPANY
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20201, 'Error retrieving companies: ' || SQLERRM);
END SP_SYS_COMPANY_SELECT_ALL;
/

PROMPT ✓ Updated SP_SYS_COMPANY_SELECT_ALL procedure
PROMPT

-- Also update the SELECT_BY_ID procedure
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        LEGAL_NAME,
        LEGAL_NAME_E,
        COMPANY_CODE,
        TAX_NUMBER,
        COUNTRY_ID,
        CURR_ID,
        DEFAULT_BRANCH_ID,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        CASE 
            WHEN COMPANY_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_COMPANY
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20202, 'Error retrieving company by ID: ' || SQLERRM);
END SP_SYS_COMPANY_SELECT_BY_ID;
/

PROMPT ✓ Updated SP_SYS_COMPANY_SELECT_BY_ID procedure
PROMPT

-- =====================================================
-- FIX 2: Fix Legacy Audit Log Date Format Issue
-- =====================================================
PROMPT =====================================================
PROMPT FIX 2: Fixing Legacy Audit Log Date Format
PROMPT =====================================================

-- The issue is in the dynamic SQL construction where dates are concatenated as strings
-- We need to use proper date handling with TO_DATE function
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
    
    -- FIX: Use proper date comparison instead of string concatenation
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

PROMPT ✓ Updated SP_SYS_AUDIT_LOG_LEGACY_SELECT procedure with proper date handling
PROMPT

-- Verify the procedures were created
PROMPT Verifying procedures:
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN ('SP_SYS_COMPANY_SELECT_ALL', 
                      'SP_SYS_COMPANY_SELECT_BY_ID',
                      'SP_SYS_AUDIT_LOG_LEGACY_SELECT')
ORDER BY object_name;

COMMIT;

PROMPT
PROMPT =====================================================
PROMPT Additional Runtime Error Fixes Completed Successfully!
PROMPT =====================================================
PROMPT
PROMPT Summary of Changes:
PROMPT 1. ✓ Updated SP_SYS_COMPANY_SELECT_ALL to include HAS_LOGO column
PROMPT 2. ✓ Updated SP_SYS_COMPANY_SELECT_BY_ID to include HAS_LOGO column
PROMPT 3. ✓ Fixed SP_SYS_AUDIT_LOG_LEGACY_SELECT date format handling
PROMPT
PROMPT Next Steps:
PROMPT - No need to restart your application
PROMPT - GET /api/companies should now work correctly
PROMPT - Legacy audit log viewer should now work correctly
PROMPT =====================================================
