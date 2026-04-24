-- =============================================
-- ThinkOnErp API - Remove SystemLanguage Column from SYS_COMPANY
-- Description: Removes the SYSTEM_LANGUAGE column from SYS_COMPANY table
-- Version: 1.0
-- Date: April 24, 2026
-- Author: ThinkOnErp Development Team
-- =============================================

-- This script removes the SYSTEM_LANGUAGE column from SYS_COMPANY table
-- since system language functionality has been removed from the company level.

PROMPT '=== Starting SystemLanguage Column Removal ==='
PROMPT 'Script: 33_Remove_SystemLanguage_Column.sql'
PROMPT 'Purpose: Remove SYSTEM_LANGUAGE column from SYS_COMPANY table'
PROMPT ''

-- Check if the column exists before attempting to drop it
DECLARE
    column_exists NUMBER;
BEGIN
    SELECT COUNT(*)
    INTO column_exists
    FROM user_tab_columns
    WHERE table_name = 'SYS_COMPANY'
    AND column_name = 'SYSTEM_LANGUAGE';
    
    IF column_exists > 0 THEN
        DBMS_OUTPUT.PUT_LINE('SYSTEM_LANGUAGE column found in SYS_COMPANY table. Proceeding with removal...');
        
        -- Drop the SYSTEM_LANGUAGE column
        EXECUTE IMMEDIATE 'ALTER TABLE SYS_COMPANY DROP COLUMN SYSTEM_LANGUAGE';
        
        DBMS_OUTPUT.PUT_LINE('✓ SYSTEM_LANGUAGE column removed successfully from SYS_COMPANY table.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('ℹ SYSTEM_LANGUAGE column does not exist in SYS_COMPANY table. No action needed.');
    END IF;
END;
/

-- Verify the column has been removed
PROMPT ''
PROMPT 'Verifying column removal...'

DECLARE
    column_exists NUMBER;
BEGIN
    SELECT COUNT(*)
    INTO column_exists
    FROM user_tab_columns
    WHERE table_name = 'SYS_COMPANY'
    AND column_name = 'SYSTEM_LANGUAGE';
    
    IF column_exists = 0 THEN
        DBMS_OUTPUT.PUT_LINE('✓ Verification successful: SYSTEM_LANGUAGE column has been removed.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✗ Verification failed: SYSTEM_LANGUAGE column still exists.');
        RAISE_APPLICATION_ERROR(-20001, 'Failed to remove SYSTEM_LANGUAGE column from SYS_COMPANY table');
    END IF;
END;
/

-- Display current SYS_COMPANY table structure (optional)
PROMPT ''
PROMPT 'Current SYS_COMPANY table structure:'
SELECT column_name, data_type, data_length, nullable, data_default
FROM user_tab_columns
WHERE table_name = 'SYS_COMPANY'
ORDER BY column_id;

PROMPT ''
PROMPT '=== SystemLanguage Column Removal Completed Successfully ==='
PROMPT 'The SYSTEM_LANGUAGE column has been removed from SYS_COMPANY table.'
PROMPT 'System language functionality is no longer available at the company level.'
PROMPT ''

-- Commit the changes
COMMIT;