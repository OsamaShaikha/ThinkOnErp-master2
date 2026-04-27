-- =====================================================
-- Populate BRANCH_ID for Legacy Audit Records
-- =====================================================
-- Purpose: Attempt to populate BRANCH_ID for legacy audit records where it can be derived
-- This script uses various strategies to infer branch context from related data
--
-- Strategies:
-- 1. For user-related actions: Use the user's primary branch
-- 2. For branch-related actions: Use the entity ID directly
-- 3. For company-related actions: Use the company's default branch
-- 4. For other entities: Attempt to derive from related records
--
-- Note: This is a best-effort migration. Some records may remain with NULL BRANCH_ID
-- =====================================================

SET SERVEROUTPUT ON;

DECLARE
    v_updated_count NUMBER := 0;
    v_total_null_branches NUMBER := 0;
    v_start_time TIMESTAMP := SYSTIMESTAMP;
    v_end_time TIMESTAMP;
    
BEGIN
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('Branch ID Population Started');
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('Start time: ' || TO_CHAR(v_start_time, 'YYYY-MM-DD HH24:MI:SS'));
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Count records with NULL BRANCH_ID
    SELECT COUNT(*)
    INTO v_total_null_branches
    FROM SYS_AUDIT_LOG
    WHERE BRANCH_ID IS NULL;
    
    DBMS_OUTPUT.PUT_LINE('Records with NULL BRANCH_ID: ' || v_total_null_branches);
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Strategy 1: For SYS_BRANCH entity actions, use ENTITY_ID as BRANCH_ID
    DBMS_OUTPUT.PUT_LINE('Strategy 1: Populating BRANCH_ID for SYS_BRANCH entity actions...');
    UPDATE SYS_AUDIT_LOG
    SET BRANCH_ID = ENTITY_ID
    WHERE BRANCH_ID IS NULL
    AND ENTITY_TYPE = 'SYS_BRANCH'
    AND ENTITY_ID IS NOT NULL
    AND EXISTS (SELECT 1 FROM SYS_BRANCH WHERE ROW_ID = ENTITY_ID);
    
    v_updated_count := SQL%ROWCOUNT;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('  Updated ' || v_updated_count || ' records');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Strategy 2: For user actions, derive BRANCH_ID from user's current branch
    DBMS_OUTPUT.PUT_LINE('Strategy 2: Populating BRANCH_ID from user branch assignments...');
    UPDATE SYS_AUDIT_LOG al
    SET BRANCH_ID = (
        SELECT u.BRANCH_ID
        FROM SYS_USERS u
        WHERE u.ROW_ID = al.ACTOR_ID
        AND al.ACTOR_TYPE = 'USER'
        AND u.BRANCH_ID IS NOT NULL
        AND ROWNUM = 1
    )
    WHERE al.BRANCH_ID IS NULL
    AND al.ACTOR_TYPE = 'USER'
    AND EXISTS (
        SELECT 1 
        FROM SYS_USERS u 
        WHERE u.ROW_ID = al.ACTOR_ID 
        AND u.BRANCH_ID IS NOT NULL
    );
    
    v_updated_count := SQL%ROWCOUNT;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('  Updated ' || v_updated_count || ' records');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Strategy 3: For SYS_USERS entity actions, derive from the target user's branch
    DBMS_OUTPUT.PUT_LINE('Strategy 3: Populating BRANCH_ID for user entity actions...');
    UPDATE SYS_AUDIT_LOG al
    SET BRANCH_ID = (
        SELECT u.BRANCH_ID
        FROM SYS_USERS u
        WHERE u.ROW_ID = al.ENTITY_ID
        AND al.ENTITY_TYPE = 'SYS_USERS'
        AND u.BRANCH_ID IS NOT NULL
        AND ROWNUM = 1
    )
    WHERE al.BRANCH_ID IS NULL
    AND al.ENTITY_TYPE = 'SYS_USERS'
    AND al.ENTITY_ID IS NOT NULL
    AND EXISTS (
        SELECT 1 
        FROM SYS_USERS u 
        WHERE u.ROW_ID = al.ENTITY_ID 
        AND u.BRANCH_ID IS NOT NULL
    );
    
    v_updated_count := SQL%ROWCOUNT;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('  Updated ' || v_updated_count || ' records');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Strategy 4: For company-level actions, use the company's first active branch
    DBMS_OUTPUT.PUT_LINE('Strategy 4: Populating BRANCH_ID from company default branches...');
    UPDATE SYS_AUDIT_LOG al
    SET BRANCH_ID = (
        SELECT b.ROW_ID
        FROM SYS_BRANCH b
        WHERE b.COMPANY_ID = al.COMPANY_ID
        AND b.IS_ACTIVE = 1
        AND ROWNUM = 1
        ORDER BY b.CREATION_DATE ASC
    )
    WHERE al.BRANCH_ID IS NULL
    AND al.COMPANY_ID IS NOT NULL
    AND EXISTS (
        SELECT 1 
        FROM SYS_BRANCH b 
        WHERE b.COMPANY_ID = al.COMPANY_ID 
        AND b.IS_ACTIVE = 1
    );
    
    v_updated_count := SQL%ROWCOUNT;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('  Updated ' || v_updated_count || ' records');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Strategy 5: For authentication events, try to derive from user's branch
    DBMS_OUTPUT.PUT_LINE('Strategy 5: Populating BRANCH_ID for authentication events...');
    UPDATE SYS_AUDIT_LOG al
    SET BRANCH_ID = (
        SELECT u.BRANCH_ID
        FROM SYS_USERS u
        WHERE u.ROW_ID = al.ACTOR_ID
        AND u.BRANCH_ID IS NOT NULL
        AND ROWNUM = 1
    )
    WHERE al.BRANCH_ID IS NULL
    AND al.ACTION IN ('LOGIN', 'LOGOUT', 'LOGIN_FAILED', 'TOKEN_REFRESH', 'TOKEN_REVOKED')
    AND EXISTS (
        SELECT 1 
        FROM SYS_USERS u 
        WHERE u.ROW_ID = al.ACTOR_ID 
        AND u.BRANCH_ID IS NOT NULL
    );
    
    v_updated_count := SQL%ROWCOUNT;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('  Updated ' || v_updated_count || ' records');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Update metadata to indicate branch derivation
    DBMS_OUTPUT.PUT_LINE('Updating metadata for records with derived BRANCH_ID...');
    UPDATE SYS_AUDIT_LOG
    SET METADATA = JSON_MERGEPATCH(
        METADATA,
        JSON_OBJECT(
            'branch_id_derived' VALUE 'true',
            'branch_derivation_date' VALUE TO_CHAR(SYSDATE, 'YYYY-MM-DD"T"HH24:MI:SS')
        )
    )
    WHERE CORRELATION_ID LIKE 'LEGACY-%'
    AND BRANCH_ID IS NOT NULL
    AND METADATA IS NOT NULL;
    
    v_updated_count := SQL%ROWCOUNT;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('  Updated metadata for ' || v_updated_count || ' records');
    DBMS_OUTPUT.PUT_LINE('');
    
    v_end_time := SYSTIMESTAMP;
    
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('Branch ID Population Completed');
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('End time: ' || TO_CHAR(v_end_time, 'YYYY-MM-DD HH24:MI:SS'));
    DBMS_OUTPUT.PUT_LINE('Duration: ' || 
                        EXTRACT(SECOND FROM (v_end_time - v_start_time)) || ' seconds');
    DBMS_OUTPUT.PUT_LINE('');
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('');
        DBMS_OUTPUT.PUT_LINE('==============================================');
        DBMS_OUTPUT.PUT_LINE('ERROR: Branch ID Population Failed');
        DBMS_OUTPUT.PUT_LINE('==============================================');
        DBMS_OUTPUT.PUT_LINE('Error Code: ' || SQLCODE);
        DBMS_OUTPUT.PUT_LINE('Error Message: ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('');
        RAISE;
END;
/

-- =====================================================
-- Verification Queries
-- =====================================================

PROMPT
PROMPT ==============================================
PROMPT Branch ID Population Verification
PROMPT ==============================================
PROMPT

-- Check BRANCH_ID population status
PROMPT Checking BRANCH_ID population status...
SELECT 
    COUNT(*) as total_records,
    SUM(CASE WHEN BRANCH_ID IS NOT NULL THEN 1 ELSE 0 END) as records_with_branch,
    SUM(CASE WHEN BRANCH_ID IS NULL THEN 1 ELSE 0 END) as records_without_branch,
    ROUND(SUM(CASE WHEN BRANCH_ID IS NOT NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as branch_coverage_percentage
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID LIKE 'LEGACY-%';

PROMPT
PROMPT Checking BRANCH_ID population by entity type...
SELECT 
    ENTITY_TYPE,
    COUNT(*) as total_records,
    SUM(CASE WHEN BRANCH_ID IS NOT NULL THEN 1 ELSE 0 END) as with_branch,
    SUM(CASE WHEN BRANCH_ID IS NULL THEN 1 ELSE 0 END) as without_branch,
    ROUND(SUM(CASE WHEN BRANCH_ID IS NOT NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as coverage_pct
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID LIKE 'LEGACY-%'
GROUP BY ENTITY_TYPE
ORDER BY total_records DESC;

PROMPT
PROMPT Checking BRANCH_ID population by action type...
SELECT 
    ACTION,
    COUNT(*) as total_records,
    SUM(CASE WHEN BRANCH_ID IS NOT NULL THEN 1 ELSE 0 END) as with_branch,
    SUM(CASE WHEN BRANCH_ID IS NULL THEN 1 ELSE 0 END) as without_branch,
    ROUND(SUM(CASE WHEN BRANCH_ID IS NOT NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as coverage_pct
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID LIKE 'LEGACY-%'
GROUP BY ACTION
ORDER BY total_records DESC;

PROMPT
PROMPT Sample records with derived BRANCH_ID (first 5)...
SELECT 
    ROW_ID,
    CORRELATION_ID,
    ENTITY_TYPE,
    ACTION,
    BRANCH_ID,
    COMPANY_ID,
    CREATION_DATE
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID LIKE 'LEGACY-%'
AND BRANCH_ID IS NOT NULL
ORDER BY CREATION_DATE DESC
FETCH FIRST 5 ROWS ONLY;

PROMPT
PROMPT Records still without BRANCH_ID by entity type...
SELECT 
    ENTITY_TYPE,
    COUNT(*) as records_without_branch
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID LIKE 'LEGACY-%'
AND BRANCH_ID IS NULL
GROUP BY ENTITY_TYPE
ORDER BY records_without_branch DESC;

PROMPT
PROMPT ==============================================
PROMPT Verification Complete
PROMPT ==============================================
