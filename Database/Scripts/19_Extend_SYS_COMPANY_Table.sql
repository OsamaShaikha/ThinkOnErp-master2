-- =============================================
-- ThinkOnErp API - Extend SYS_COMPANY Table
-- Description: Adds new columns to SYS_COMPANY table for enhanced company information
-- =============================================

-- Add new columns to SYS_COMPANY table
ALTER TABLE SYS_COMPANY ADD (
    LEGAL_NAME VARCHAR2(300),
    LEGAL_NAME_E VARCHAR2(300),
    COMPANY_CODE VARCHAR2(50),
    DEFAULT_LANG VARCHAR2(10) DEFAULT 'ar',
    TAX_NUMBER VARCHAR2(50),
    FISCAL_YEAR_ID NUMBER,
    BASE_CURRENCY_ID NUMBER,
    SYSTEM_LANGUAGE VARCHAR2(10) DEFAULT 'ar',
    ROUNDING_RULES VARCHAR2(50) DEFAULT 'HALF_UP',
    COMPANY_LOGO BLOB
);

-- Add comments to new columns for documentation
COMMENT ON COLUMN SYS_COMPANY.LEGAL_NAME IS 'Legal name of the company in Arabic';
COMMENT ON COLUMN SYS_COMPANY.LEGAL_NAME_E IS 'Legal name of the company in English';
COMMENT ON COLUMN SYS_COMPANY.COMPANY_CODE IS 'Unique company code for identification';
COMMENT ON COLUMN SYS_COMPANY.DEFAULT_LANG IS 'Default language for the company (ar/en)';
COMMENT ON COLUMN SYS_COMPANY.TAX_NUMBER IS 'Tax registration number';
COMMENT ON COLUMN SYS_COMPANY.FISCAL_YEAR_ID IS 'Current active fiscal year ID';
COMMENT ON COLUMN SYS_COMPANY.BASE_CURRENCY_ID IS 'Base currency for the company';
COMMENT ON COLUMN SYS_COMPANY.SYSTEM_LANGUAGE IS 'System language preference (ar/en)';
COMMENT ON COLUMN SYS_COMPANY.ROUNDING_RULES IS 'Rounding rules for calculations (HALF_UP, HALF_DOWN, UP, DOWN, CEILING, FLOOR)';
COMMENT ON COLUMN SYS_COMPANY.COMPANY_LOGO IS 'Company logo image stored as BLOB';

-- Add foreign key constraints
ALTER TABLE SYS_COMPANY ADD CONSTRAINT FK_COMPANY_FISCAL_YEAR 
    FOREIGN KEY (FISCAL_YEAR_ID) REFERENCES SYS_FISCAL_YEAR(ROW_ID);

ALTER TABLE SYS_COMPANY ADD CONSTRAINT FK_COMPANY_BASE_CURRENCY 
    FOREIGN KEY (BASE_CURRENCY_ID) REFERENCES SYS_CURRENCY(ROW_ID);

-- Add unique constraint for company code
ALTER TABLE SYS_COMPANY ADD CONSTRAINT UK_COMPANY_CODE UNIQUE (COMPANY_CODE);

-- Add check constraints for language fields
ALTER TABLE SYS_COMPANY ADD CONSTRAINT CHK_DEFAULT_LANG 
    CHECK (DEFAULT_LANG IN ('ar', 'en'));

ALTER TABLE SYS_COMPANY ADD CONSTRAINT CHK_SYSTEM_LANGUAGE 
    CHECK (SYSTEM_LANGUAGE IN ('ar', 'en'));

-- Add check constraint for rounding rules
ALTER TABLE SYS_COMPANY ADD CONSTRAINT CHK_ROUNDING_RULES 
    CHECK (ROUNDING_RULES IN ('HALF_UP', 'HALF_DOWN', 'UP', 'DOWN', 'CEILING', 'FLOOR'));

-- Create indexes for better query performance
CREATE INDEX IDX_COMPANY_CODE ON SYS_COMPANY(COMPANY_CODE);
CREATE INDEX IDX_COMPANY_FISCAL_YEAR ON SYS_COMPANY(FISCAL_YEAR_ID);
CREATE INDEX IDX_COMPANY_BASE_CURRENCY ON SYS_COMPANY(BASE_CURRENCY_ID);

-- =============================================
-- Verification: Display table structure
-- =============================================
SELECT column_name, data_type, data_length, nullable, data_default
FROM user_tab_columns
WHERE table_name = 'SYS_COMPANY'
ORDER BY column_id;

-- Display constraints
SELECT constraint_name, constraint_type, search_condition
FROM user_constraints
WHERE table_name = 'SYS_COMPANY'
ORDER BY constraint_type, constraint_name;
