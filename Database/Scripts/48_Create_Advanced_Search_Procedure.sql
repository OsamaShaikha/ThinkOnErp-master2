-- =============================================
-- Advanced Search Enhancement - Enhanced Search with Relevance Scoring
-- Description: Advanced search procedure with multi-criteria AND/OR logic and relevance scoring
-- Requirements: 8.1-8.12, 8.9
-- Task: 10.1 - Create advanced search functionality
-- =============================================

-- =============================================
-- Procedure: SP_SYS_REQUEST_TICKET_ADVANCED_SEARCH
-- Description: Advanced search with relevance scoring and flexible filtering
-- Parameters:
--   P_SEARCH_TERM: Full-text search term (searches title and description)
--   P_COMPANY_ID: Filter by company (0 = all)
--   P_BRANCH_ID: Filter by branch (0 = all)
--   P_ASSIGNEE_ID: Filter by assignee (0 = all)
--   P_REQUESTER_ID: Filter by requester (0 = all)
--   P_STATUS_IDS: Comma-separated status IDs (NULL = all)
--   P_PRIORITY_IDS: Comma-separated priority IDs (NULL = all)
--   P_TYPE_IDS: Comma-separated type IDs (NULL = all)
--   P_CATEGORY_IDS: Comma-separated category IDs (NULL = all)
--   P_CREATED_FROM: Creation date range start
--   P_CREATED_TO: Creation date range end
--   P_DUE_FROM: Expected resolution date range start
--   P_DUE_TO: Expected resolution date range end
--   P_SLA_STATUS: Filter by SLA status (OnTime, AtRisk, Overdue, NULL = all)
--   P_FILTER_LOGIC: AND or OR for combining multiple criteria (default AND)
--   P_INCLUDE_INACTIVE: Include inactive tickets (default N)
--   P_PAGE_NUMBER: Page number for pagination
--   P_PAGE_SIZE: Records per page
--   P_SORT_BY: Sort field (RELEVANCE, CREATION_DATE, PRIORITY, STATUS, DUE_DATE)
--   P_SORT_DIRECTION: Sort direction (ASC or DESC)
-- Returns: SYS_REFCURSOR with tickets and relevance scores
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_ADVANCED_SEARCH (
    P_SEARCH_TERM IN NVARCHAR2 DEFAULT NULL,
    P_COMPANY_ID IN NUMBER DEFAULT 0,
    P_BRANCH_ID IN NUMBER DEFAULT 0,
    P_ASSIGNEE_ID IN NUMBER DEFAULT 0,
    P_REQUESTER_ID IN NUMBER DEFAULT 0,
    P_STATUS_IDS IN VARCHAR2 DEFAULT NULL,
    P_PRIORITY_IDS IN VARCHAR2 DEFAULT NULL,
    P_TYPE_IDS IN VARCHAR2 DEFAULT NULL,
    P_CATEGORY_IDS IN VARCHAR2 DEFAULT NULL,
    P_CREATED_FROM IN DATE DEFAULT NULL,
    P_CREATED_TO IN DATE DEFAULT NULL,
    P_DUE_FROM IN DATE DEFAULT NULL,
    P_DUE_TO IN DATE DEFAULT NULL,
    P_SLA_STATUS IN VARCHAR2 DEFAULT NULL,
    P_FILTER_LOGIC IN VARCHAR2 DEFAULT 'AND',
    P_INCLUDE_INACTIVE IN CHAR DEFAULT 'N',
    P_PAGE_NUMBER IN NUMBER DEFAULT 1,
    P_PAGE_SIZE IN NUMBER DEFAULT 20,
    P_SORT_BY IN VARCHAR2 DEFAULT 'RELEVANCE',
    P_SORT_DIRECTION IN VARCHAR2 DEFAULT 'DESC',
    P_RESULT_CURSOR OUT SYS_REFCURSOR,
    P_TOTAL_COUNT OUT NUMBER
)
AS
    V_SQL NCLOB;
    V_COUNT_SQL NCLOB;
    V_WHERE_CLAUSE NCLOB := '';
    V_RELEVANCE_CLAUSE NCLOB;
    V_ORDER_CLAUSE NVARCHAR2(200);
    V_OFFSET NUMBER;
    V_FILTER_COUNT NUMBER := 0;
    V_LOGIC_OP VARCHAR2(5);
BEGIN
    -- Calculate offset for pagination
    V_OFFSET := (P_PAGE_NUMBER - 1) * P_PAGE_SIZE;
    
    -- Determine filter logic operator
    V_LOGIC_OP := CASE WHEN UPPER(P_FILTER_LOGIC) = 'OR' THEN ' OR ' ELSE ' AND ' END;
    
    -- Build relevance scoring clause
    V_RELEVANCE_CLAUSE := '0';
    IF P_SEARCH_TERM IS NOT NULL THEN
        V_RELEVANCE_CLAUSE := '(
            CASE WHEN UPPER(t.TITLE_EN) = UPPER(''' || P_SEARCH_TERM || ''') THEN 100
                 WHEN UPPER(t.TITLE_AR) = UPPER(''' || P_SEARCH_TERM || ''') THEN 100
                 WHEN UPPER(t.TITLE_EN) LIKE UPPER(''' || P_SEARCH_TERM || '%'') THEN 80
                 WHEN UPPER(t.TITLE_AR) LIKE UPPER(''' || P_SEARCH_TERM || '%'') THEN 80
                 WHEN UPPER(t.TITLE_EN) LIKE UPPER(''%' || P_SEARCH_TERM || '%'') THEN 60
                 WHEN UPPER(t.TITLE_AR) LIKE UPPER(''%' || P_SEARCH_TERM || '%'') THEN 60
                 WHEN UPPER(t.DESCRIPTION) LIKE UPPER(''%' || P_SEARCH_TERM || '%'') THEN 40
                 ELSE 0
            END +
            CASE WHEN pr.PRIORITY_LEVEL = 4 THEN 20  -- Critical priority boost
                 WHEN pr.PRIORITY_LEVEL = 3 THEN 10  -- High priority boost
                 ELSE 0
            END +
            CASE WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE THEN 15  -- Overdue boost
                 WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE + 1 THEN 10  -- Due soon boost
                 ELSE 0
            END
        )';
    END IF;
    
    -- Build WHERE clause with flexible logic
    V_WHERE_CLAUSE := 'WHERE 1=1';
    
    -- Active/Inactive filter (always AND)
    IF P_INCLUDE_INACTIVE = 'N' THEN
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND t.IS_ACTIVE = ''Y''';
    END IF;
    
    -- Start building optional filters
    IF P_COMPANY_ID > 0 OR P_BRANCH_ID > 0 OR P_ASSIGNEE_ID > 0 OR P_REQUESTER_ID > 0 OR
       P_STATUS_IDS IS NOT NULL OR P_PRIORITY_IDS IS NOT NULL OR P_TYPE_IDS IS NOT NULL OR
       P_CATEGORY_IDS IS NOT NULL OR P_CREATED_FROM IS NOT NULL OR P_CREATED_TO IS NOT NULL OR
       P_DUE_FROM IS NOT NULL OR P_DUE_TO IS NOT NULL OR P_SLA_STATUS IS NOT NULL OR
       P_SEARCH_TERM IS NOT NULL THEN
        
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ' AND (';
        
        -- Company filter
        IF P_COMPANY_ID > 0 THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || 't.COMPANY_ID = ' || P_COMPANY_ID;
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        -- Branch filter
        IF P_BRANCH_ID > 0 THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || 't.BRANCH_ID = ' || P_BRANCH_ID;
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        -- Assignee filter
        IF P_ASSIGNEE_ID > 0 THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || 't.ASSIGNEE_ID = ' || P_ASSIGNEE_ID;
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        -- Requester filter
        IF P_REQUESTER_ID > 0 THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || 't.REQUESTER_ID = ' || P_REQUESTER_ID;
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        -- Status IDs filter (supports multiple)
        IF P_STATUS_IDS IS NOT NULL THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || 't.TICKET_STATUS_ID IN (' || P_STATUS_IDS || ')';
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        -- Priority IDs filter (supports multiple)
        IF P_PRIORITY_IDS IS NOT NULL THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || 't.TICKET_PRIORITY_ID IN (' || P_PRIORITY_IDS || ')';
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        -- Type IDs filter (supports multiple)
        IF P_TYPE_IDS IS NOT NULL THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || 't.TICKET_TYPE_ID IN (' || P_TYPE_IDS || ')';
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        -- Category IDs filter (supports multiple)
        IF P_CATEGORY_IDS IS NOT NULL THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || 't.TICKET_CATEGORY_ID IN (' || P_CATEGORY_IDS || ')';
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        -- Creation date range
        IF P_CREATED_FROM IS NOT NULL THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || 't.CREATION_DATE >= TO_DATE(''' || TO_CHAR(P_CREATED_FROM, 'YYYY-MM-DD HH24:MI:SS') || ''', ''YYYY-MM-DD HH24:MI:SS'')';
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        IF P_CREATED_TO IS NOT NULL THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || 't.CREATION_DATE <= TO_DATE(''' || TO_CHAR(P_CREATED_TO, 'YYYY-MM-DD HH24:MI:SS') || ''', ''YYYY-MM-DD HH24:MI:SS'')';
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        -- Due date range
        IF P_DUE_FROM IS NOT NULL THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || 't.EXPECTED_RESOLUTION_DATE >= TO_DATE(''' || TO_CHAR(P_DUE_FROM, 'YYYY-MM-DD HH24:MI:SS') || ''', ''YYYY-MM-DD HH24:MI:SS'')';
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        IF P_DUE_TO IS NOT NULL THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || 't.EXPECTED_RESOLUTION_DATE <= TO_DATE(''' || TO_CHAR(P_DUE_TO, 'YYYY-MM-DD HH24:MI:SS') || ''', ''YYYY-MM-DD HH24:MI:SS'')';
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        -- SLA status filter
        IF P_SLA_STATUS IS NOT NULL THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || '(CASE 
                WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL THEN ''Resolved''
                WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE THEN ''Overdue''
                WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE + (pr.ESCALATION_THRESHOLD_HOURS / 24) THEN ''AtRisk''
                ELSE ''OnTime''
            END) = ''' || P_SLA_STATUS || '''';
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        -- Search term filter
        IF P_SEARCH_TERM IS NOT NULL THEN
            IF V_FILTER_COUNT > 0 THEN V_WHERE_CLAUSE := V_WHERE_CLAUSE || V_LOGIC_OP; END IF;
            V_WHERE_CLAUSE := V_WHERE_CLAUSE || '(UPPER(t.TITLE_AR) LIKE UPPER(''%' || P_SEARCH_TERM || '%'') OR UPPER(t.TITLE_EN) LIKE UPPER(''%' || P_SEARCH_TERM || '%'') OR UPPER(t.DESCRIPTION) LIKE UPPER(''%' || P_SEARCH_TERM || '%''))';
            V_FILTER_COUNT := V_FILTER_COUNT + 1;
        END IF;
        
        V_WHERE_CLAUSE := V_WHERE_CLAUSE || ')';
    END IF;
    
    -- Build ORDER BY clause
    V_ORDER_CLAUSE := 'ORDER BY ';
    CASE UPPER(P_SORT_BY)
        WHEN 'RELEVANCE' THEN V_ORDER_CLAUSE := V_ORDER_CLAUSE || 'RELEVANCE_SCORE';
        WHEN 'CREATION_DATE' THEN V_ORDER_CLAUSE := V_ORDER_CLAUSE || 't.CREATION_DATE';
        WHEN 'PRIORITY' THEN V_ORDER_CLAUSE := V_ORDER_CLAUSE || 'pr.PRIORITY_LEVEL';
        WHEN 'STATUS' THEN V_ORDER_CLAUSE := V_ORDER_CLAUSE || 'st.DISPLAY_ORDER';
        WHEN 'TITLE' THEN V_ORDER_CLAUSE := V_ORDER_CLAUSE || 't.TITLE_EN';
        WHEN 'DUE_DATE' THEN V_ORDER_CLAUSE := V_ORDER_CLAUSE || 't.EXPECTED_RESOLUTION_DATE';
        ELSE V_ORDER_CLAUSE := V_ORDER_CLAUSE || 'RELEVANCE_SCORE';
    END CASE;
    
    V_ORDER_CLAUSE := V_ORDER_CLAUSE || ' ' || UPPER(P_SORT_DIRECTION);
    
    -- Get total count
    V_COUNT_SQL := 'SELECT COUNT(*) FROM SYS_REQUEST_TICKET t
        LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
        ' || V_WHERE_CLAUSE;
    
    EXECUTE IMMEDIATE V_COUNT_SQL INTO P_TOTAL_COUNT;
    
    -- Build main query with pagination and relevance scoring
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
                WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE + (pr.ESCALATION_THRESHOLD_HOURS / 24) THEN ''AtRisk''
                ELSE ''OnTime''
            END AS SLA_STATUS,
            ' || V_RELEVANCE_CLAUSE || ' AS RELEVANCE_SCORE,
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
        RAISE_APPLICATION_ERROR(-20460, 'Error in advanced search: ' || SQLERRM);
END SP_SYS_REQUEST_TICKET_ADVANCED_SEARCH;
/

-- =============================================
-- Verification
-- =============================================
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name = 'SP_SYS_REQUEST_TICKET_ADVANCED_SEARCH'
ORDER BY object_name;
