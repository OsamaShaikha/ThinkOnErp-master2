# Fiscal Year and Company Extension

## Overview
This document describes the database changes for adding fiscal year functionality and extending the company table with additional fields.

## New Database Scripts

### Script 18: Create SYS_FISCAL_YEAR Table
**File:** `18_Create_SYS_FISCAL_YEAR_Table.sql`

Creates the fiscal year table with the following features:
- **Table:** SYS_FISCAL_YEAR
- **Sequence:** SEQ_SYS_FISCAL_YEAR
- **Columns:**
  - ROW_ID (Primary Key)
  - COMPANY_ID (Foreign Key to SYS_COMPANY)
  - FISCAL_YEAR_CODE (Unique per company, e.g., 'FY2024')
  - ROW_DESC (Arabic description)
  - ROW_DESC_E (English description)
  - START_DATE (Fiscal year start date)
  - END_DATE (Fiscal year end date)
  - IS_CLOSED (Flag to indicate if fiscal year is closed)
  - IS_ACTIVE (Soft delete flag)
  - Audit fields (CREATION_USER, CREATION_DATE, UPDATE_USER, UPDATE_DATE)

**Stored Procedures:**
- `SP_SYS_FISCAL_YEAR_SELECT_ALL` - Get all active fiscal years
- `SP_SYS_FISCAL_YEAR_SELECT_BY_ID` - Get fiscal year by ID
- `SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY` - Get all fiscal years for a company
- `SP_SYS_FISCAL_YEAR_INSERT` - Create new fiscal year
- `SP_SYS_FISCAL_YEAR_UPDATE` - Update fiscal year
- `SP_SYS_FISCAL_YEAR_DELETE` - Soft delete fiscal year
- `SP_SYS_FISCAL_YEAR_CLOSE` - Close a fiscal year

### Script 19: Extend SYS_COMPANY Table
**File:** `19_Extend_SYS_COMPANY_Table.sql`

Adds new columns to the SYS_COMPANY table:
- **LEGAL_NAME** (VARCHAR2(300)) - Legal name in Arabic
- **LEGAL_NAME_E** (VARCHAR2(300)) - Legal name in English
- **COMPANY_CODE** (VARCHAR2(50)) - Unique company code
- **DEFAULT_LANG** (VARCHAR2(10)) - Default language (ar/en), default: 'ar'
- **TAX_NUMBER** (VARCHAR2(50)) - Tax registration number
- **FISCAL_YEAR_ID** (NUMBER) - Current active fiscal year (FK to SYS_FISCAL_YEAR)
- **BASE_CURRENCY_ID** (NUMBER) - Base currency (FK to SYS_CURRENCY)
- **SYSTEM_LANGUAGE** (VARCHAR2(10)) - System language preference (ar/en), default: 'ar'
- **ROUNDING_RULES** (VARCHAR2(50)) - Rounding rules for calculations, default: 'HALF_UP'
  - Valid values: HALF_UP, HALF_DOWN, UP, DOWN, CEILING, FLOOR
- **COMPANY_LOGO** (BLOB) - Company logo image

**Constraints:**
- Unique constraint on COMPANY_CODE
- Foreign key to SYS_FISCAL_YEAR
- Foreign key to SYS_CURRENCY (BASE_CURRENCY_ID)
- Check constraints for language fields (ar/en)
- Check constraint for rounding rules

**Indexes:**
- IDX_COMPANY_CODE
- IDX_COMPANY_FISCAL_YEAR
- IDX_COMPANY_BASE_CURRENCY

### Script 20: Update SYS_COMPANY Procedures
**File:** `20_Update_SYS_COMPANY_Procedures.sql`

Updates all company stored procedures to handle new columns:
- `SP_SYS_COMPANY_SELECT_ALL` - Updated to include new columns
- `SP_SYS_COMPANY_SELECT_BY_ID` - Updated to include new columns
- `SP_SYS_COMPANY_INSERT` - Updated with new parameters
- `SP_SYS_COMPANY_UPDATE` - Updated with new parameters
- `SP_SYS_COMPANY_UPDATE_LOGO` - New procedure for updating company logo separately
- `SP_SYS_COMPANY_GET_LOGO` - New procedure for retrieving company logo

### Script 21: Insert Fiscal Year Test Data
**File:** `21_Insert_Fiscal_Year_Test_Data.sql`

Inserts test fiscal year data:
- Company 1: FY2024, FY2025, FY2026 (all open)
- Company 2: FY2024 (closed), FY2025 (open)

### Script 22: Update Company Test Data
**File:** `22_Update_Company_Test_Data.sql`

Updates existing company test data with new field values:
- Company 1: ThinkOn Software Solutions Ltd.
- Company 2: Global Trading Company Ltd.

## Execution Order

**IMPORTANT:** Execute scripts in the following order:

1. **18_Create_SYS_FISCAL_YEAR_Table.sql** - Creates fiscal year table and procedures
2. **19_Extend_SYS_COMPANY_Table.sql** - Adds new columns to company table
3. **20_Update_SYS_COMPANY_Procedures.sql** - Updates company procedures
4. **21_Insert_Fiscal_Year_Test_Data.sql** - Inserts fiscal year test data
5. **22_Update_Company_Test_Data.sql** - Updates company test data

## Rounding Rules

The ROUNDING_RULES column supports the following values:
- **HALF_UP**: Round towards "nearest neighbor" unless both neighbors are equidistant, in which case round up (default)
- **HALF_DOWN**: Round towards "nearest neighbor" unless both neighbors are equidistant, in which case round down
- **UP**: Round away from zero
- **DOWN**: Round towards zero
- **CEILING**: Round towards positive infinity
- **FLOOR**: Round towards negative infinity

## Language Support

Both DEFAULT_LANG and SYSTEM_LANGUAGE support:
- **ar**: Arabic
- **en**: English

## Fiscal Year Management

### Creating a Fiscal Year
```sql
DECLARE
    v_new_id NUMBER;
BEGIN
    SP_SYS_FISCAL_YEAR_INSERT(
        P_COMPANY_ID => 1,
        P_FISCAL_YEAR_CODE => 'FY2026',
        P_ROW_DESC => 'السنة المالية 2026',
        P_ROW_DESC_E => 'Fiscal Year 2026',
        P_START_DATE => TO_DATE('2026-01-01', 'YYYY-MM-DD'),
        P_END_DATE => TO_DATE('2026-12-31', 'YYYY-MM-DD'),
        P_IS_CLOSED => '0',
        P_CREATION_USER => 'admin',
        P_NEW_ID => v_new_id
    );
END;
```

### Closing a Fiscal Year
```sql
BEGIN
    SP_SYS_FISCAL_YEAR_CLOSE(
        P_ROW_ID => 1,
        P_UPDATE_USER => 'admin'
    );
END;
```

### Querying Fiscal Years by Company
```sql
DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY(
        P_COMPANY_ID => 1,
        P_RESULT_CURSOR => v_cursor
    );
END;
```

## Company Logo Management

### Updating Company Logo
```sql
DECLARE
    v_logo BLOB;
BEGIN
    -- Load logo from file or other source
    -- v_logo := ...
    
    SP_SYS_COMPANY_UPDATE_LOGO(
        P_ROW_ID => 1,
        P_COMPANY_LOGO => v_logo,
        P_UPDATE_USER => 'admin'
    );
END;
```

### Retrieving Company Logo
```sql
DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_COMPANY_GET_LOGO(
        P_ROW_ID => 1,
        P_RESULT_CURSOR => v_cursor
    );
END;
```

## Validation Rules

1. **Fiscal Year Dates**: END_DATE must be after START_DATE
2. **Company Code**: Must be unique across all companies
3. **Fiscal Year Code**: Must be unique per company
4. **Language Fields**: Must be either 'ar' or 'en'
5. **Rounding Rules**: Must be one of the supported values
6. **Foreign Keys**: FISCAL_YEAR_ID and BASE_CURRENCY_ID must reference valid records

## Migration Notes

- Existing company records will have NULL values for new columns initially
- Run script 22 to populate test data for existing companies
- Update application code to handle new company fields
- Consider setting default values for FISCAL_YEAR_ID and BASE_CURRENCY_ID for existing companies

## Next Steps

After running these scripts, you'll need to:
1. Update C# entity models to include new properties
2. Update DTOs for company operations
3. Update repository methods to handle new fields
4. Create new controller endpoints for fiscal year management
5. Update API documentation
6. Add validation for new fields in the application layer
