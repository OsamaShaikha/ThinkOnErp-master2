-- Update SYS_AUDIT_LOG_ARCHIVE table to include legacy compatibility columns
-- Task 1.7: Create SYS_AUDIT_LOG_ARCHIVE table with identical structure plus archival metadata
-- This script adds the missing legacy compatibility columns to the existing archive table

-- Add legacy compatibility columns to archive table to match main table structure
ALTER TABLE SYS_AUDIT_LOG_ARCHIVE ADD (
    BUSINESS_MODULE NVARCHAR2(50),        -- Business module classification (POS, HR, Accounting, etc.)
    DEVICE_IDENTIFIER NVARCHAR2(100),     -- Structured device information (POS Terminal 03, Desktop-HR-02, etc.)
    ERROR_CODE NVARCHAR2(50),             -- Standardized error codes (DB_TIMEOUT_001, API_HR_045, etc.)
    BUSINESS_DESCRIPTION NVARCHAR2(4000)  -- Human-readable error descriptions for business users
);

-- Add comments for the new legacy compatibility columns in archive table
COMMENT ON COLUMN SYS_AUDIT_LOG_ARCHIVE.BUSINESS_MODULE IS 'Business module classification for legacy compatibility (POS, HR, Accounting, Finance, Inventory, Reports, Administration, etc.)';
COMMENT ON COLUMN SYS_AUDIT_LOG_ARCHIVE.DEVICE_IDENTIFIER IS 'Structured device information extracted from User-Agent (POS Terminal 03, Desktop-HR-02, Mobile-Sales-01, etc.)';
COMMENT ON COLUMN SYS_AUDIT_LOG_ARCHIVE.ERROR_CODE IS 'Standardized error codes for business users (DB_TIMEOUT_001, API_HR_045, VALIDATION_POS_012, etc.)';
COMMENT ON COLUMN SYS_AUDIT_LOG_ARCHIVE.BUSINESS_DESCRIPTION IS 'Human-readable error descriptions translated from technical exceptions for business users';

-- Create indexes for legacy compatibility columns in archive table to support queries
CREATE INDEX IDX_ARCHIVE_BUSINESS_MODULE ON SYS_AUDIT_LOG_ARCHIVE(BUSINESS_MODULE);
CREATE INDEX IDX_ARCHIVE_ERROR_CODE ON SYS_AUDIT_LOG_ARCHIVE(ERROR_CODE);

-- Create composite index for common legacy query patterns (module + date) in archive
CREATE INDEX IDX_ARCHIVE_MODULE_DATE ON SYS_AUDIT_LOG_ARCHIVE(BUSINESS_MODULE, CREATION_DATE);

-- Create additional indexes for archive table queries as specified in requirements
CREATE INDEX IDX_ARCHIVE_ENTITY_DATE ON SYS_AUDIT_LOG_ARCHIVE(ENTITY_TYPE, ENTITY_ID, CREATION_DATE);
CREATE INDEX IDX_ARCHIVE_ACTOR_DATE ON SYS_AUDIT_LOG_ARCHIVE(ACTOR_ID, CREATION_DATE);
CREATE INDEX IDX_ARCHIVE_SEVERITY ON SYS_AUDIT_LOG_ARCHIVE(SEVERITY);
CREATE INDEX IDX_ARCHIVE_ENDPOINT ON SYS_AUDIT_LOG_ARCHIVE(ENDPOINT_PATH);

COMMIT;

-- Verify the archive table now has identical structure to main table plus archival metadata
SELECT 'Archive table structure verification:' AS status FROM DUAL;

SELECT COLUMN_NAME, DATA_TYPE, DATA_LENGTH, NULLABLE
FROM USER_TAB_COLUMNS 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG_ARCHIVE' 
AND COLUMN_NAME IN ('BUSINESS_MODULE', 'DEVICE_IDENTIFIER', 'ERROR_CODE', 'BUSINESS_DESCRIPTION',
                    'ARCHIVED_DATE', 'ARCHIVE_BATCH_ID', 'CHECKSUM')
ORDER BY COLUMN_NAME;

-- Verify all main table columns exist in archive table (should return 0 missing columns)
SELECT COUNT(*) AS missing_columns_count
FROM (
    SELECT COLUMN_NAME FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
    MINUS
    SELECT COLUMN_NAME FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'SYS_AUDIT_LOG_ARCHIVE'
    WHERE COLUMN_NAME NOT IN ('ARCHIVED_DATE', 'ARCHIVE_BATCH_ID', 'CHECKSUM')
);
