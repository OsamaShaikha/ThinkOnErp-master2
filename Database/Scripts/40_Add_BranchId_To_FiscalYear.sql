-- =============================================
-- Script: 40_Add_BranchId_To_FiscalYear.sql
-- Description: Add BRANCH_ID column to SYS_FISCAL_YEAR table
-- Author: System
-- Date: 2026-04-25
-- =============================================

-- This script adds BRANCH_ID to SYS_FISCAL_YEAR table so that fiscal years
-- can be associated with specific branches while maintaining company association

PROMPT ========================================
PROMPT Adding BRANCH_ID to SYS_FISCAL_YEAR Table
PROMPT ========================================

-- Step 1: Add BRANCH_ID column to SYS_FISCAL_YEAR table
ALTER TABLE SYS_FISCAL_YEAR ADD (
    BRANCH_ID NUMBER(19) NULL
);

PROMPT BRANCH_ID column added to SYS_FISCAL_YEAR table

-- Step 2: Add foreign key constraint to SYS_BRANCH
ALTER TABLE SYS_FISCAL_YEAR ADD CONSTRAINT FK_FISCAL_YEAR_BRANCH 
    FOREIGN KEY (BRANCH_ID) REFERENCES SYS_BRANCH(ROW_ID);

PROMPT Foreign key constraint added for BRANCH_ID

-- Step 3: Update existing fiscal years to associate with default branches
-- This assumes each company has a default branch set
UPDATE SYS_FISCAL_YEAR fy
SET BRANCH_ID = (
    SELECT c.DEFAULT_BRANCH_ID
    FROM SYS_COMPANY c
    WHERE c.ROW_ID = fy.COMPANY_ID
    AND c.DEFAULT_BRANCH_ID IS NOT NULL
)
WHERE fy.BRANCH_ID IS NULL
AND EXISTS (
    SELECT 1 FROM SYS_COMPANY c 
    WHERE c.ROW_ID = fy.COMPANY_ID 
    AND c.DEFAULT_BRANCH_ID IS NOT NULL
);

PROMPT Existing fiscal years updated with default branch associations

-- Step 4: For companies without default branch, associate with first active branch
UPDATE SYS_FISCAL_YEAR fy
SET BRANCH_ID = (
    SELECT MIN(b.ROW_ID)
    FROM SYS_BRANCH b
    WHERE b.PAR_ROW_ID = fy.COMPANY_ID
    AND b.IS_ACTIVE = 'Y'
)
WHERE fy.BRANCH_ID IS NULL
AND EXISTS (
    SELECT 1 FROM SYS_BRANCH b 
    WHERE b.PAR_ROW_ID = fy.COMPANY_ID 
    AND b.IS_ACTIVE = 'Y'
);

PROMPT Remaining fiscal years updated with first active branch

-- Step 5: Make BRANCH_ID NOT NULL after data migration
ALTER TABLE SYS_FISCAL_YEAR MODIFY BRANCH_ID NUMBER(19) NOT NULL;

PROMPT BRANCH_ID column set to NOT NULL

-- Step 6: Create index on BRANCH_ID for better query performance
CREATE INDEX IDX_FISCAL_YEAR_BRANCH ON SYS_FISCAL_YEAR(BRANCH_ID);

PROMPT Index created on BRANCH_ID

-- Step 7: Update stored procedures to include BRANCH_ID parameter
-- Note: You'll need to update the fiscal year stored procedures manually
-- to include P_BRANCH_ID parameter in INSERT and UPDATE operations

PROMPT ========================================
PROMPT Migration completed successfully!
PROMPT ========================================
PROMPT 
PROMPT IMPORTANT: Update the following stored procedures to include BRANCH_ID:
PROMPT - SP_SYS_FISCAL_YEAR_INSERT
PROMPT - SP_SYS_FISCAL_YEAR_UPDATE
PROMPT 
PROMPT Verify the data migration:
SELECT 
    fy.ROW_ID,
    fy.FISCAL_YEAR_CODE,
    fy.COMPANY_ID,
    c.ROW_DESC_E as COMPANY_NAME,
    fy.BRANCH_ID,
    b.ROW_DESC_E as BRANCH_NAME
FROM SYS_FISCAL_YEAR fy
LEFT JOIN SYS_COMPANY c ON fy.COMPANY_ID = c.ROW_ID
LEFT JOIN SYS_BRANCH b ON fy.BRANCH_ID = b.ROW_ID
WHERE fy.IS_ACTIVE = 'Y'
ORDER BY fy.COMPANY_ID, fy.BRANCH_ID, fy.START_DATE;

COMMIT;
