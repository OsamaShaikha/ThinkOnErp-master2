-- Validation script for SYS_AUDIT_LOG_ARCHIVE table structure
-- Task 1.7: Verify archive table has identical structure plus archival metadata

-- Check if archive table exists
SELECT 'Archive table exists: ' || CASE WHEN COUNT(*) > 0 THEN 'YES' ELSE 'NO' END AS status
FROM USER_TABLES 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG_ARCHIVE';

-- Verify all main table columns exist in archive table
SELECT 'Missing columns from main table in archive:' AS check_type, COUNT(*) AS missing_count
FROM (
    SELECT COLUMN_NAME FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
    MINUS
    SELECT COLUMN_NAME FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'SYS_AUDIT_LOG_ARCHIVE'
);

-- Verify archival metadata columns exist
SELECT 'Archival metadata columns:' AS check_type, COUNT(*) AS found_count
FROM USER_TAB_COLUMNS 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG_ARCHIVE' 
AND COLUMN_NAME IN ('ARCHIVED_DATE', 'ARCHIVE_BATCH_ID', 'CHECKSUM');

-- Verify legacy compatibility columns exist in archive table
SELECT 'Legacy compatibility columns in archive:' AS check_type, COUNT(*) AS found_count
FROM USER_TAB_COLUMNS 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG_ARCHIVE' 
AND COLUMN_NAME IN ('BUSINESS_MODULE', 'DEVICE_IDENTIFIER', 'ERROR_CODE', 'BUSINESS_DESCRIPTION');

-- Show archive table column count vs main table column count
SELECT 
    'SYS_AUDIT_LOG' AS table_name,
    COUNT(*) AS column_count
FROM USER_TAB_COLUMNS 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
UNION ALL
SELECT 
    'SYS_AUDIT_LOG_ARCHIVE' AS table_name,
    COUNT(*) AS column_count
FROM USER_TAB_COLUMNS 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG_ARCHIVE'
ORDER BY table_name;

-- Verify archive table indexes exist
SELECT 'Archive table indexes:' AS check_type, COUNT(*) AS index_count
FROM USER_INDEXES 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG_ARCHIVE';

-- List all archive table indexes
SELECT INDEX_NAME, UNIQUENESS, STATUS
FROM USER_INDEXES 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG_ARCHIVE'
ORDER BY INDEX_NAME;

-- Verify specific required indexes exist
SELECT 
    CASE WHEN COUNT(*) >= 10 THEN 'PASS' ELSE 'FAIL' END AS index_check_status,
    COUNT(*) AS found_indexes,
    10 AS required_indexes
FROM USER_INDEXES 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG_ARCHIVE'
AND INDEX_NAME IN (
    'IDX_ARCHIVE_COMPANY_DATE',
    'IDX_ARCHIVE_CORRELATION', 
    'IDX_ARCHIVE_BATCH',
    'IDX_ARCHIVE_CATEGORY_DATE',
    'IDX_ARCHIVE_BUSINESS_MODULE',
    'IDX_ARCHIVE_ERROR_CODE',
    'IDX_ARCHIVE_MODULE_DATE',
    'IDX_ARCHIVE_ENTITY_DATE',
    'IDX_ARCHIVE_ACTOR_DATE',
    'IDX_ARCHIVE_SEVERITY'
);

-- Final validation summary
SELECT 
    CASE 
        WHEN main_cols + 3 = archive_cols THEN 'PASS: Archive table structure is correct'
        ELSE 'FAIL: Archive table structure mismatch'
    END AS validation_result,
    main_cols AS main_table_columns,
    archive_cols AS archive_table_columns,
    (archive_cols - main_cols) AS additional_archive_columns
FROM (
    SELECT 
        (SELECT COUNT(*) FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'SYS_AUDIT_LOG') AS main_cols,
        (SELECT COUNT(*) FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'SYS_AUDIT_LOG_ARCHIVE') AS archive_cols
    FROM DUAL
);