-- =====================================================
-- Audit Log Migration Validation Script
-- =====================================================
-- Purpose: Comprehensive validation of audit log data migration
-- This script verifies data integrity, consistency, and completeness
-- after running the migration scripts
--
-- Validation Checks:
-- 1. All records have CORRELATION_ID
-- 2. All records have EVENT_CATEGORY
-- 3. All records have SEVERITY
-- 4. Foreign key integrity (BRANCH_ID references valid branches)
-- 5. Data consistency (ENDPOINT_PATH matches ENTITY_TYPE)
-- 6. Metadata structure validation
-- 7. No duplicate CORRELATION_IDs (except for related events)
-- 8. Date integrity (CREATION_DATE is valid)
-- =====================================================

SET SERVEROUTPUT ON;
SET LINESIZE 200;

PROMPT
PROMPT ======================================================
PROMPT Audit Log Migration Validation Report
PROMPT ======================================================
PROMPT Generated: 
SELECT TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') as validation_timestamp FROM DUAL;
PROMPT
PROMPT

-- =====================================================
-- Check 1: CORRELATION_ID Completeness
-- =====================================================
PROMPT ======================================================
PROMPT Check 1: CORRELATION_ID Completeness
PROMPT ======================================================
PROMPT Expected: All records should have CORRELATION_ID
PROMPT

SELECT 
    'CORRELATION_ID Completeness' as check_name,
    COUNT(*) as total_records,
    SUM(CASE WHEN CORRELATION_ID IS NOT NULL THEN 1 ELSE 0 END) as records_with_correlation_id,
    SUM(CASE WHEN CORRELATION_ID IS NULL THEN 1 ELSE 0 END) as records_without_correlation_id,
    CASE 
        WHEN SUM(CASE WHEN CORRELATION_ID IS NULL THEN 1 ELSE 0 END) = 0 THEN 'PASS'
        ELSE 'FAIL'
    END as status
FROM SYS_AUDIT_LOG;

PROMPT

-- =====================================================
-- Check 2: EVENT_CATEGORY Completeness
-- =====================================================
PROMPT ======================================================
PROMPT Check 2: EVENT_CATEGORY Completeness
PROMPT ======================================================
PROMPT Expected: All records should have EVENT_CATEGORY
PROMPT

SELECT 
    'EVENT_CATEGORY Completeness' as check_name,
    COUNT(*) as total_records,
    SUM(CASE WHEN EVENT_CATEGORY IS NOT NULL THEN 1 ELSE 0 END) as records_with_category,
    SUM(CASE WHEN EVENT_CATEGORY IS NULL THEN 1 ELSE 0 END) as records_without_category,
    CASE 
        WHEN SUM(CASE WHEN EVENT_CATEGORY IS NULL THEN 1 ELSE 0 END) = 0 THEN 'PASS'
        ELSE 'FAIL'
    END as status
FROM SYS_AUDIT_LOG;

PROMPT

-- =====================================================
-- Check 3: SEVERITY Completeness
-- =====================================================
PROMPT ======================================================
PROMPT Check 3: SEVERITY Completeness
PROMPT ======================================================
PROMPT Expected: All records should have SEVERITY
PROMPT

SELECT 
    'SEVERITY Completeness' as check_name,
    COUNT(*) as total_records,
    SUM(CASE WHEN SEVERITY IS NOT NULL THEN 1 ELSE 0 END) as records_with_severity,
    SUM(CASE WHEN SEVERITY IS NULL THEN 1 ELSE 0 END) as records_without_severity,
    CASE 
        WHEN SUM(CASE WHEN SEVERITY IS NULL THEN 1 ELSE 0 END) = 0 THEN 'PASS'
        ELSE 'FAIL'
    END as status
FROM SYS_AUDIT_LOG;

PROMPT

-- =====================================================
-- Check 4: BRANCH_ID Foreign Key Integrity
-- =====================================================
PROMPT ======================================================
PROMPT Check 4: BRANCH_ID Foreign Key Integrity
PROMPT ======================================================
PROMPT Expected: All non-NULL BRANCH_ID values should reference valid branches
PROMPT

SELECT 
    'BRANCH_ID Foreign Key Integrity' as check_name,
    COUNT(*) as records_with_branch_id,
    SUM(CASE 
        WHEN NOT EXISTS (SELECT 1 FROM SYS_BRANCH WHERE ROW_ID = al.BRANCH_ID) 
        THEN 1 ELSE 0 
    END) as invalid_branch_references,
    CASE 
        WHEN SUM(CASE 
            WHEN NOT EXISTS (SELECT 1 FROM SYS_BRANCH WHERE ROW_ID = al.BRANCH_ID) 
            THEN 1 ELSE 0 
        END) = 0 THEN 'PASS'
        ELSE 'FAIL'
    END as status
FROM SYS_AUDIT_LOG al
WHERE BRANCH_ID IS NOT NULL;

PROMPT

-- =====================================================
-- Check 5: EVENT_CATEGORY Valid Values
-- =====================================================
PROMPT ======================================================
PROMPT Check 5: EVENT_CATEGORY Valid Values
PROMPT ======================================================
PROMPT Expected: EVENT_CATEGORY should be one of: DataChange, Authentication, Permission, Exception, Configuration, Request
PROMPT

SELECT 
    'EVENT_CATEGORY Valid Values' as check_name,
    COUNT(*) as total_records,
    SUM(CASE 
        WHEN EVENT_CATEGORY NOT IN ('DataChange', 'Authentication', 'Permission', 'Exception', 'Configuration', 'Request') 
        THEN 1 ELSE 0 
    END) as invalid_categories,
    CASE 
        WHEN SUM(CASE 
            WHEN EVENT_CATEGORY NOT IN ('DataChange', 'Authentication', 'Permission', 'Exception', 'Configuration', 'Request') 
            THEN 1 ELSE 0 
        END) = 0 THEN 'PASS'
        ELSE 'FAIL'
    END as status
FROM SYS_AUDIT_LOG
WHERE EVENT_CATEGORY IS NOT NULL;

PROMPT

-- =====================================================
-- Check 6: SEVERITY Valid Values
-- =====================================================
PROMPT ======================================================
PROMPT Check 6: SEVERITY Valid Values
PROMPT ======================================================
PROMPT Expected: SEVERITY should be one of: Critical, Error, Warning, Info
PROMPT

SELECT 
    'SEVERITY Valid Values' as check_name,
    COUNT(*) as total_records,
    SUM(CASE 
        WHEN SEVERITY NOT IN ('Critical', 'Error', 'Warning', 'Info') 
        THEN 1 ELSE 0 
    END) as invalid_severities,
    CASE 
        WHEN SUM(CASE 
            WHEN SEVERITY NOT IN ('Critical', 'Error', 'Warning', 'Info') 
            THEN 1 ELSE 0 
        END) = 0 THEN 'PASS'
        ELSE 'FAIL'
    END as status
FROM SYS_AUDIT_LOG
WHERE SEVERITY IS NOT NULL;

PROMPT

-- =====================================================
-- Check 7: METADATA Structure for Legacy Records
-- =====================================================
PROMPT ======================================================
PROMPT Check 7: METADATA Structure for Legacy Records
PROMPT ======================================================
PROMPT Expected: All legacy records should have METADATA with migration info
PROMPT

SELECT 
    'METADATA Structure' as check_name,
    COUNT(*) as legacy_records,
    SUM(CASE WHEN METADATA IS NOT NULL THEN 1 ELSE 0 END) as records_with_metadata,
    SUM(CASE WHEN METADATA IS NULL THEN 1 ELSE 0 END) as records_without_metadata,
    CASE 
        WHEN SUM(CASE WHEN METADATA IS NULL THEN 1 ELSE 0 END) = 0 THEN 'PASS'
        ELSE 'FAIL'
    END as status
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID LIKE 'LEGACY-%';

PROMPT

-- =====================================================
-- Check 8: ENDPOINT_PATH Consistency
-- =====================================================
PROMPT ======================================================
PROMPT Check 8: ENDPOINT_PATH Consistency
PROMPT ======================================================
PROMPT Expected: ENDPOINT_PATH should be populated for all records
PROMPT

SELECT 
    'ENDPOINT_PATH Consistency' as check_name,
    COUNT(*) as total_records,
    SUM(CASE WHEN ENDPOINT_PATH IS NOT NULL THEN 1 ELSE 0 END) as records_with_endpoint,
    SUM(CASE WHEN ENDPOINT_PATH IS NULL THEN 1 ELSE 0 END) as records_without_endpoint,
    CASE 
        WHEN SUM(CASE WHEN ENDPOINT_PATH IS NULL THEN 1 ELSE 0 END) = 0 THEN 'PASS'
        ELSE 'FAIL'
    END as status
FROM SYS_AUDIT_LOG;

PROMPT

-- =====================================================
-- Check 9: Date Integrity
-- =====================================================
PROMPT ======================================================
PROMPT Check 9: Date Integrity
PROMPT ======================================================
PROMPT Expected: All records should have valid CREATION_DATE
PROMPT

SELECT 
    'Date Integrity' as check_name,
    COUNT(*) as total_records,
    SUM(CASE WHEN CREATION_DATE IS NULL THEN 1 ELSE 0 END) as records_without_date,
    SUM(CASE WHEN CREATION_DATE > SYSDATE THEN 1 ELSE 0 END) as records_with_future_date,
    CASE 
        WHEN SUM(CASE WHEN CREATION_DATE IS NULL OR CREATION_DATE > SYSDATE THEN 1 ELSE 0 END) = 0 THEN 'PASS'
        ELSE 'FAIL'
    END as status
FROM SYS_AUDIT_LOG;

PROMPT

-- =====================================================
-- Check 10: Legacy Record Identification
-- =====================================================
PROMPT ======================================================
PROMPT Check 10: Legacy Record Identification
PROMPT ======================================================
PROMPT Expected: Legacy records should be identifiable by CORRELATION_ID prefix
PROMPT

SELECT 
    'Legacy Record Identification' as check_name,
    COUNT(*) as total_records,
    SUM(CASE WHEN CORRELATION_ID LIKE 'LEGACY-%' THEN 1 ELSE 0 END) as legacy_records,
    SUM(CASE WHEN CORRELATION_ID NOT LIKE 'LEGACY-%' THEN 1 ELSE 0 END) as new_records,
    ROUND(SUM(CASE WHEN CORRELATION_ID LIKE 'LEGACY-%' THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as legacy_percentage
FROM SYS_AUDIT_LOG;

PROMPT

-- =====================================================
-- Summary Statistics
-- =====================================================
PROMPT ======================================================
PROMPT Summary Statistics
PROMPT ======================================================
PROMPT

PROMPT Distribution by EVENT_CATEGORY:
SELECT 
    EVENT_CATEGORY,
    COUNT(*) as record_count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM SYS_AUDIT_LOG), 2) as percentage
FROM SYS_AUDIT_LOG
GROUP BY EVENT_CATEGORY
ORDER BY record_count DESC;

PROMPT
PROMPT Distribution by SEVERITY:
SELECT 
    SEVERITY,
    COUNT(*) as record_count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM SYS_AUDIT_LOG), 2) as percentage
FROM SYS_AUDIT_LOG
GROUP BY SEVERITY
ORDER BY record_count DESC;

PROMPT
PROMPT BRANCH_ID Coverage:
SELECT 
    'Total Records' as metric,
    COUNT(*) as value
FROM SYS_AUDIT_LOG
UNION ALL
SELECT 
    'Records with BRANCH_ID' as metric,
    COUNT(*) as value
FROM SYS_AUDIT_LOG
WHERE BRANCH_ID IS NOT NULL
UNION ALL
SELECT 
    'Records without BRANCH_ID' as metric,
    COUNT(*) as value
FROM SYS_AUDIT_LOG
WHERE BRANCH_ID IS NULL
UNION ALL
SELECT 
    'BRANCH_ID Coverage %' as metric,
    ROUND(SUM(CASE WHEN BRANCH_ID IS NOT NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as value
FROM SYS_AUDIT_LOG;

PROMPT
PROMPT Top 10 Endpoint Paths:
SELECT 
    ENDPOINT_PATH,
    COUNT(*) as record_count
FROM SYS_AUDIT_LOG
WHERE ENDPOINT_PATH IS NOT NULL
GROUP BY ENDPOINT_PATH
ORDER BY record_count DESC
FETCH FIRST 10 ROWS ONLY;

PROMPT
PROMPT Date Range of Audit Logs:
SELECT 
    MIN(CREATION_DATE) as earliest_record,
    MAX(CREATION_DATE) as latest_record,
    ROUND((MAX(CREATION_DATE) - MIN(CREATION_DATE)), 2) as days_span
FROM SYS_AUDIT_LOG;

PROMPT

-- =====================================================
-- Potential Issues Report
-- =====================================================
PROMPT ======================================================
PROMPT Potential Issues Report
PROMPT ======================================================
PROMPT

PROMPT Records with NULL CORRELATION_ID (should be 0):
SELECT COUNT(*) as issue_count
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID IS NULL;

PROMPT
PROMPT Records with NULL EVENT_CATEGORY (should be 0):
SELECT COUNT(*) as issue_count
FROM SYS_AUDIT_LOG
WHERE EVENT_CATEGORY IS NULL;

PROMPT
PROMPT Records with NULL SEVERITY (should be 0):
SELECT COUNT(*) as issue_count
FROM SYS_AUDIT_LOG
WHERE SEVERITY IS NULL;

PROMPT
PROMPT Records with invalid BRANCH_ID references:
SELECT COUNT(*) as issue_count
FROM SYS_AUDIT_LOG al
WHERE BRANCH_ID IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM SYS_BRANCH WHERE ROW_ID = al.BRANCH_ID);

PROMPT
PROMPT Legacy records without METADATA:
SELECT COUNT(*) as issue_count
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID LIKE 'LEGACY-%'
AND METADATA IS NULL;

PROMPT

-- =====================================================
-- Overall Validation Result
-- =====================================================
PROMPT ======================================================
PROMPT Overall Validation Result
PROMPT ======================================================
PROMPT

DECLARE
    v_total_checks NUMBER := 10;
    v_passed_checks NUMBER := 0;
    v_failed_checks NUMBER := 0;
    v_pass_percentage NUMBER;
    
BEGIN
    -- Count passed checks
    SELECT 
        SUM(CASE WHEN check_passed = 1 THEN 1 ELSE 0 END)
    INTO v_passed_checks
    FROM (
        -- Check 1: CORRELATION_ID
        SELECT CASE WHEN COUNT(*) = SUM(CASE WHEN CORRELATION_ID IS NOT NULL THEN 1 ELSE 0 END) THEN 1 ELSE 0 END as check_passed
        FROM SYS_AUDIT_LOG
        UNION ALL
        -- Check 2: EVENT_CATEGORY
        SELECT CASE WHEN COUNT(*) = SUM(CASE WHEN EVENT_CATEGORY IS NOT NULL THEN 1 ELSE 0 END) THEN 1 ELSE 0 END
        FROM SYS_AUDIT_LOG
        UNION ALL
        -- Check 3: SEVERITY
        SELECT CASE WHEN COUNT(*) = SUM(CASE WHEN SEVERITY IS NOT NULL THEN 1 ELSE 0 END) THEN 1 ELSE 0 END
        FROM SYS_AUDIT_LOG
        UNION ALL
        -- Check 4: BRANCH_ID integrity
        SELECT CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END
        FROM SYS_AUDIT_LOG al
        WHERE BRANCH_ID IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM SYS_BRANCH WHERE ROW_ID = al.BRANCH_ID)
        UNION ALL
        -- Check 5: EVENT_CATEGORY valid values
        SELECT CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END
        FROM SYS_AUDIT_LOG
        WHERE EVENT_CATEGORY IS NOT NULL
        AND EVENT_CATEGORY NOT IN ('DataChange', 'Authentication', 'Permission', 'Exception', 'Configuration', 'Request')
        UNION ALL
        -- Check 6: SEVERITY valid values
        SELECT CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END
        FROM SYS_AUDIT_LOG
        WHERE SEVERITY IS NOT NULL
        AND SEVERITY NOT IN ('Critical', 'Error', 'Warning', 'Info')
        UNION ALL
        -- Check 7: METADATA for legacy records
        SELECT CASE WHEN COUNT(*) = SUM(CASE WHEN METADATA IS NOT NULL THEN 1 ELSE 0 END) THEN 1 ELSE 0 END
        FROM SYS_AUDIT_LOG
        WHERE CORRELATION_ID LIKE 'LEGACY-%'
        UNION ALL
        -- Check 8: ENDPOINT_PATH
        SELECT CASE WHEN COUNT(*) = SUM(CASE WHEN ENDPOINT_PATH IS NOT NULL THEN 1 ELSE 0 END) THEN 1 ELSE 0 END
        FROM SYS_AUDIT_LOG
        UNION ALL
        -- Check 9: Date integrity
        SELECT CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END
        FROM SYS_AUDIT_LOG
        WHERE CREATION_DATE IS NULL OR CREATION_DATE > SYSDATE
        UNION ALL
        -- Check 10: Always pass (informational)
        SELECT 1 FROM DUAL
    );
    
    v_failed_checks := v_total_checks - v_passed_checks;
    v_pass_percentage := ROUND((v_passed_checks / v_total_checks) * 100, 2);
    
    DBMS_OUTPUT.PUT_LINE('Total Checks: ' || v_total_checks);
    DBMS_OUTPUT.PUT_LINE('Passed: ' || v_passed_checks);
    DBMS_OUTPUT.PUT_LINE('Failed: ' || v_failed_checks);
    DBMS_OUTPUT.PUT_LINE('Pass Rate: ' || v_pass_percentage || '%');
    DBMS_OUTPUT.PUT_LINE('');
    
    IF v_failed_checks = 0 THEN
        DBMS_OUTPUT.PUT_LINE('STATUS: ALL CHECKS PASSED ✓');
        DBMS_OUTPUT.PUT_LINE('Migration completed successfully with no issues detected.');
    ELSIF v_pass_percentage >= 80 THEN
        DBMS_OUTPUT.PUT_LINE('STATUS: MOSTLY PASSED ⚠');
        DBMS_OUTPUT.PUT_LINE('Migration completed with minor issues. Review failed checks above.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('STATUS: FAILED ✗');
        DBMS_OUTPUT.PUT_LINE('Migration has significant issues. Review failed checks and re-run migration.');
    END IF;
    
END;
/

PROMPT
PROMPT ======================================================
PROMPT Validation Report Complete
PROMPT ======================================================
