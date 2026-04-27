# Database Migration Scripts - Execution Order

## Current Issue
Your database is out of sync with the application code. Several migration scripts need to be run to fix the stored procedures and table structures.

## Scripts to Run (In Order)

### 1. Script 32: Move Fields from Company to Branch
**File**: `Database/Scripts/32_Move_Fields_From_Company_To_Branch.sql`

**What it does**:
- Adds `DEFAULT_LANG`, `BASE_CURRENCY_ID`, `ROUNDING_RULES` columns to `SYS_BRANCH` table
- Migrates data from `SYS_COMPANY` to `SYS_BRANCH`
- Updates all branch stored procedures to include these fields
- Removes these columns from `SYS_COMPANY` table

**Run**:
```sql
@Database/Scripts/32_Move_Fields_From_Company_To_Branch.sql
```

---

### 2. Script 40: Add BRANCH_ID to Fiscal Year
**File**: `Database/Scripts/40_Add_BranchId_To_FiscalYear.sql`

**What it does**:
- Adds `BRANCH_ID` column to `SYS_FISCAL_YEAR` table
- Migrates existing fiscal years to associate with default branches
- Creates foreign key constraint and index

**Run**:
```sql
@Database/Scripts/40_Add_BranchId_To_FiscalYear.sql
```

---

### 3. Script 41: Update Fiscal Year Procedures
**File**: `Database/Scripts/41_Update_FiscalYear_Procedures_For_BranchId.sql`

**What it does**:
- Updates all fiscal year stored procedures to include `BRANCH_ID` parameter
- Adds validation to ensure branch belongs to company
- Creates `SP_SYS_FISCAL_YEAR_SELECT_BY_BRANCH` procedure

**Run**:
```sql
@Database/Scripts/41_Update_FiscalYear_Procedures_For_BranchId.sql
```

---

### 4. Script 43: Remove FISCAL_YEAR_ID from Company
**File**: `Database/Scripts/43_Remove_FiscalYearId_From_Company.sql`

**What it does**:
- Drops `FK_COMPANY_FISCAL_YEAR` foreign key constraint
- Drops `IDX_COMPANY_FISCAL_YEAR` index
- Removes `FISCAL_YEAR_ID` column from `SYS_COMPANY` table

**Run**:
```sql
@Database/Scripts/43_Remove_FiscalYearId_From_Company.sql
```

---

### 5. Script 45: Fix Company Procedure (Create with Branch and Fiscal Year)
**File**: `Database/Scripts/45_Fix_Company_Procedure_Complete.sql`

**What it does**:
- Drops and recreates `SP_SYS_COMPANY_INSERT_WITH_BRANCH` procedure
- Adds `P_COMPANY_LOGO` parameter
- Adds `P_NEW_FISCAL_YEAR_ID` output parameter
- Automatically creates default fiscal year when creating company

**Run**:
```sql
@Database/Scripts/45_Fix_Company_Procedure_Complete.sql
```

---

### 6. Script 46: Fix Company SELECT Procedures
**File**: `Database/Scripts/46_Fix_Company_Select_Procedures.sql`

**What it does**:
- Updates `SP_SYS_COMPANY_SELECT_ALL` to remove deleted columns
- Updates `SP_SYS_COMPANY_SELECT_BY_ID` to remove deleted columns
- Adds `DEFAULT_BRANCH_ID` to SELECT statements

**Run**:
```sql
@Database/Scripts/46_Fix_Company_Select_Procedures.sql
```

---

## Quick Execution (All Scripts)

If you want to run all scripts at once, execute them in this order:

```sql
-- Connect to your Oracle database first
-- Then run:

@Database/Scripts/32_Move_Fields_From_Company_To_Branch.sql
@Database/Scripts/40_Add_BranchId_To_FiscalYear.sql
@Database/Scripts/41_Update_FiscalYear_Procedures_For_BranchId.sql
@Database/Scripts/43_Remove_FiscalYearId_From_Company.sql
@Database/Scripts/45_Fix_Company_Procedure_Complete.sql
@Database/Scripts/46_Fix_Company_Select_Procedures.sql
```

---

## After Running Scripts

1. **Restart your API** to ensure it picks up any connection pool changes
2. **Test the endpoints**:
   - `GET /api/companies` - Should return list of companies
   - `GET /api/branches` - Should return list of branches
   - `POST /api/companies` - Should create company with branch and fiscal year

---

## What Changed

### SYS_COMPANY Table
**Removed columns**:
- `DEFAULT_LANG` → Moved to `SYS_BRANCH`
- `BASE_CURRENCY_ID` → Moved to `SYS_BRANCH`
- `SYSTEM_LANGUAGE` → Removed completely
- `ROUNDING_RULES` → Moved to `SYS_BRANCH`
- `FISCAL_YEAR_ID` → Removed (fiscal years now reference companies, not vice versa)

**Added columns**:
- `DEFAULT_BRANCH_ID` → References the default branch for the company

### SYS_BRANCH Table
**Added columns**:
- `DEFAULT_LANG` (VARCHAR2) - Default language for the branch
- `BASE_CURRENCY_ID` (NUMBER) - Base currency for the branch
- `ROUNDING_RULES` (NUMBER) - Rounding rules (1-6)
- `BRANCH_LOGO` (BLOB) - Branch logo image

### SYS_FISCAL_YEAR Table
**Added columns**:
- `BRANCH_ID` (NUMBER, NOT NULL) - Foreign key to `SYS_BRANCH`

**Note**: Fiscal years now belong to BOTH companies AND branches

---

## Verification Queries

After running all scripts, verify the changes:

```sql
-- Check SYS_COMPANY structure
SELECT column_name, data_type, nullable
FROM user_tab_columns
WHERE table_name = 'SYS_COMPANY'
ORDER BY column_id;

-- Check SYS_BRANCH structure
SELECT column_name, data_type, nullable
FROM user_tab_columns
WHERE table_name = 'SYS_BRANCH'
ORDER BY column_id;

-- Check SYS_FISCAL_YEAR structure
SELECT column_name, data_type, nullable
FROM user_tab_columns
WHERE table_name = 'SYS_FISCAL_YEAR'
ORDER BY column_id;

-- Check procedure status
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_COMPANY_SELECT_ALL',
    'SP_SYS_COMPANY_SELECT_BY_ID',
    'SP_SYS_COMPANY_INSERT_WITH_BRANCH',
    'SP_SYS_BRANCH_SELECT_ALL',
    'SP_SYS_BRANCH_SELECT_BY_ID',
    'SP_SYS_FISCAL_YEAR_SELECT_ALL'
)
ORDER BY object_name;
```

---

## Troubleshooting

### If you get "column does not exist" errors:
- You may have already run some scripts. Check which columns exist in your tables
- Run the verification queries above to see current state

### If you get "object already exists" errors:
- The script is trying to create something that already exists
- This is usually safe to ignore if the object is already in the correct state

### If procedures show as INVALID:
```sql
-- Recompile invalid procedures
ALTER PROCEDURE SP_SYS_COMPANY_SELECT_ALL COMPILE;
ALTER PROCEDURE SP_SYS_COMPANY_SELECT_BY_ID COMPILE;
ALTER PROCEDURE SP_SYS_COMPANY_INSERT_WITH_BRANCH COMPILE;
ALTER PROCEDURE SP_SYS_BRANCH_SELECT_ALL COMPILE;
```

---

**Date Created**: April 26, 2026  
**Status**: Ready for execution
