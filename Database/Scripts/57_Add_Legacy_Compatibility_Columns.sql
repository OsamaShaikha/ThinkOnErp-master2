-- Add legacy compatibility columns to SYS_AUDIT_LOG table
-- Task 1.2: Add legacy compatibility columns (BUSINESS_MODULE, DEVICE_IDENTIFIER, ERROR_CODE, BUSINESS_DESCRIPTION)
-- These columns support the existing logs.png interface format for backward compatibility

-- Add legacy compatibility columns to SYS_AUDIT_LOG table
ALTER TABLE SYS_AUDIT_LOG ADD (
    BUSINESS_MODULE NVARCHAR2(50),        -- Business module classification (POS, HR, Accounting, etc.)
    DEVICE_IDENTIFIER NVARCHAR2(100),     -- Structured device information (POS Terminal 03, Desktop-HR-02, etc.)
    ERROR_CODE NVARCHAR2(50),             -- Standardized error codes (DB_TIMEOUT_001, API_HR_045, etc.)
    BUSINESS_DESCRIPTION NVARCHAR2(4000)  -- Human-readable error descriptions for business users
);

-- Add comments for the new legacy compatibility columns
COMMENT ON COLUMN SYS_AUDIT_LOG.BUSINESS_MODULE IS 'Business module classification for legacy compatibility (POS, HR, Accounting, Finance, Inventory, Reports, Administration, etc.)';
COMMENT ON COLUMN SYS_AUDIT_LOG.DEVICE_IDENTIFIER IS 'Structured device information extracted from User-Agent (POS Terminal 03, Desktop-HR-02, Mobile-Sales-01, etc.)';
COMMENT ON COLUMN SYS_AUDIT_LOG.ERROR_CODE IS 'Standardized error codes for business users (DB_TIMEOUT_001, API_HR_045, VALIDATION_POS_012, etc.)';
COMMENT ON COLUMN SYS_AUDIT_LOG.BUSINESS_DESCRIPTION IS 'Human-readable error descriptions translated from technical exceptions for business users';

-- Create indexes for legacy compatibility columns to support filtering and searching
CREATE INDEX IDX_AUDIT_LOG_BUSINESS_MODULE ON SYS_AUDIT_LOG(BUSINESS_MODULE);
CREATE INDEX IDX_AUDIT_LOG_ERROR_CODE ON SYS_AUDIT_LOG(ERROR_CODE);

-- Create composite index for common legacy query patterns (module + date)
CREATE INDEX IDX_AUDIT_LOG_MODULE_DATE ON SYS_AUDIT_LOG(BUSINESS_MODULE, CREATION_DATE);

COMMIT;

-- Verify the new columns were added successfully
SELECT COLUMN_NAME, DATA_TYPE, DATA_LENGTH, CHAR_LENGTH, NULLABLE
FROM USER_TAB_COLUMNS 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG' 
AND COLUMN_NAME IN ('BUSINESS_MODULE', 'DEVICE_IDENTIFIER', 'ERROR_CODE', 'BUSINESS_DESCRIPTION')
ORDER BY COLUMN_NAME;

-- Display table structure to confirm all columns
SELECT COUNT(*) as TOTAL_COLUMNS
FROM USER_TAB_COLUMNS 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG';

-- Show sample of column names for verification
SELECT COLUMN_NAME
FROM USER_TAB_COLUMNS 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
ORDER BY COLUMN_ID;