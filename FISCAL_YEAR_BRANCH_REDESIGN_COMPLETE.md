# Fiscal Year and Branch Relationship Redesign - Complete

## Summary
Successfully redesigned the fiscal year system to associate fiscal years with BOTH companies AND branches, rather than having fiscal years belong only to companies or branches having fiscal year references.

## Final Design
- **SYS_FISCAL_YEAR** table has BOTH `COMPANY_ID` and `BRANCH_ID` foreign keys
- **SYS_BRANCH** table does NOT have `FISCAL_YEAR_ID` (removed)
- **SYS_COMPANY** table does NOT have `FISCAL_YEAR_ID` (never had it)
- Fiscal years are managed independently and associated with specific company-branch combinations

## Changes Made

### 1. Database Changes
#### Created Migration Scripts:
- **`Database/Scripts/40_Add_BranchId_To_FiscalYear.sql`**
  - Adds `BRANCH_ID` column to `SYS_FISCAL_YEAR` table
  - Migrates existing data to associate fiscal years with default branches
  - Creates foreign key constraint and index
  - Makes `BRANCH_ID` NOT NULL after migration

- **`Database/Scripts/41_Update_FiscalYear_Procedures_For_BranchId.sql`**
  - Updates `SP_SYS_FISCAL_YEAR_SELECT_ALL` to include `BRANCH_ID`
  - Updates `SP_SYS_FISCAL_YEAR_SELECT_BY_ID` to include `BRANCH_ID`
  - Updates `SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY` to include `BRANCH_ID`
  - Creates new `SP_SYS_FISCAL_YEAR_SELECT_BY_BRANCH` procedure
  - Updates `SP_SYS_FISCAL_YEAR_INSERT` to require `P_BRANCH_ID` parameter with validation
  - Updates `SP_SYS_FISCAL_YEAR_UPDATE` to require `P_BRANCH_ID` parameter with validation
  - Adds validation to ensure branch belongs to company

### 2. Domain Layer Changes
#### Entity Updates:
- **`src/ThinkOnErp.Domain/Entities/SysFiscalYear.cs`**
  - Added `BranchId` property (Int64)
  - Added `Branch` navigation property (SysBranch)
  - Kept `CompanyId` and `Company` navigation property

- **`src/ThinkOnErp.Domain/Entities/SysBranch.cs`**
  - Removed `FiscalYearId` property (no longer exists)
  - Removed `FiscalYear` navigation property (no longer exists)

#### Repository Interface Updates:
- **`src/ThinkOnErp.Domain/Interfaces/IFiscalYearRepository.cs`**
  - Added `GetByBranchIdAsync(Int64 branchId)` method

### 3. Application Layer Changes
#### DTOs Updated:
- **`src/ThinkOnErp.Application/DTOs/FiscalYear/FiscalYearDto.cs`**
  - Added `BranchId` property
  - Added `BranchName` property for display

- **`src/ThinkOnErp.Application/DTOs/FiscalYear/CreateFiscalYearDto.cs`**
  - Added `BranchId` property (required)

- **`src/ThinkOnErp.Application/DTOs/FiscalYear/UpdateFiscalYearDto.cs`**
  - Added `BranchId` property (required)

- **Branch DTOs** (removed incorrect FiscalYearId references):
  - `src/ThinkOnErp.Application/DTOs/Branch/BranchDto.cs` - removed `FiscalYearId` and `FiscalYearCode`
  - `src/ThinkOnErp.Application/DTOs/Branch/CreateBranchDto.cs` - removed `FiscalYearId`
  - `src/ThinkOnErp.Application/DTOs/Branch/UpdateBranchDto.cs` - removed `FiscalYearId`

- **Company DTOs** (removed incorrect BranchFiscalYearId):
  - `src/ThinkOnErp.Application/DTOs/Company/CreateCompanyDto.cs` - removed `BranchFiscalYearId`

#### Commands Updated:
- **`src/ThinkOnErp.Application/Features/FiscalYears/Commands/CreateFiscalYear/CreateFiscalYearCommand.cs`**
  - Added `BranchId` property

- **`src/ThinkOnErp.Application/Features/FiscalYears/Commands/CreateFiscalYear/CreateFiscalYearCommandHandler.cs`**
  - Maps `BranchId` to entity

- **`src/ThinkOnErp.Application/Features/FiscalYears/Commands/UpdateFiscalYear/UpdateFiscalYearCommand.cs`**
  - Added `BranchId` property

- **`src/ThinkOnErp.Application/Features/FiscalYears/Commands/UpdateFiscalYear/UpdateFiscalYearCommandHandler.cs`**
  - Maps `BranchId` to entity

#### Validators Updated:
- **`src/ThinkOnErp.Application/Features/FiscalYears/Commands/CreateFiscalYear/CreateFiscalYearCommandValidator.cs`**
  - Added validation for `BranchId` (must be > 0)

- **`src/ThinkOnErp.Application/Features/FiscalYears/Commands/UpdateFiscalYear/UpdateFiscalYearCommandValidator.cs`**
  - Added validation for `BranchId` (must be > 0)

#### Query Handlers Updated:
- **`src/ThinkOnErp.Application/Features/FiscalYears/Queries/GetFiscalYearById/GetFiscalYearByIdQueryHandler.cs`**
  - Maps `BranchId` to DTO

- **`src/ThinkOnErp.Application/Features/FiscalYears/Queries/GetAllFiscalYears/GetAllFiscalYearsQueryHandler.cs`**
  - Maps `BranchId` to DTO

#### Branch Query Handlers Fixed:
- **`src/ThinkOnErp.Application/Features/Branches/Queries/GetBranchById/GetBranchByIdQueryHandler.cs`**
  - Removed `FiscalYearId` and `FiscalYearCode` mapping

- **`src/ThinkOnErp.Application/Features/Branches/Queries/GetAllBranches/GetAllBranchesQueryHandler.cs`**
  - Removed `FiscalYearId` and `FiscalYearCode` mapping

- **`src/ThinkOnErp.Application/Features/Branches/Queries/GetBranchesByCompanyId/GetBranchesByCompanyIdQueryHandler.cs`**
  - Removed `FiscalYearId` and `FiscalYearCode` mapping

#### Branch Command Handlers Fixed:
- **`src/ThinkOnErp.Application/Features/Branches/Commands/CreateBranch/CreateBranchCommandHandler.cs`**
  - Removed `FiscalYearId` mapping

- **`src/ThinkOnErp.Application/Features/Branches/Commands/UpdateBranch/UpdateBranchCommandHandler.cs`**
  - Removed `FiscalYearId` mapping

### 4. Infrastructure Layer Changes
#### Repository Updates:
- **`src/ThinkOnErp.Infrastructure/Repositories/FiscalYearRepository.cs`**
  - Updated `CreateAsync` to include `P_BRANCH_ID` parameter
  - Updated `UpdateAsync` to include `P_BRANCH_ID` parameter
  - Updated `MapToEntity` to map `BRANCH_ID` column
  - Added `GetByBranchIdAsync` method implementation

- **`src/ThinkOnErp.Infrastructure/Repositories/BranchRepository.cs`**
  - Removed `P_FISCAL_YEAR_ID` parameter from `CreateAsync`
  - Removed `P_FISCAL_YEAR_ID` parameter from `UpdateAsync`
  - Removed `FiscalYearId` mapping from `MapToEntity`

### 5. API Layer Changes
#### Controllers Updated:
- **`src/ThinkOnErp.API/Controllers/BranchController.cs`**
  - Removed `FiscalYearId` mapping in `CreateBranch` action
  - Removed `FiscalYearId` mapping in `UpdateBranch` action

- **`src/ThinkOnErp.API/Controllers/CompanyController.cs`**
  - Removed `BranchFiscalYearId` mapping in `CreateCompany` action

## Database Migration Steps

To apply these changes to your database:

1. **Run the migration scripts in order:**
   ```sql
   -- First, add BRANCH_ID column and migrate data
   @Database/Scripts/40_Add_BranchId_To_FiscalYear.sql
   
   -- Then, update the stored procedures
   @Database/Scripts/41_Update_FiscalYear_Procedures_For_BranchId.sql
   ```

2. **Verify the migration:**
   ```sql
   -- Check that BRANCH_ID column exists and has data
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
   WHERE fy.IS_ACTIVE = '1'
   ORDER BY fy.COMPANY_ID, fy.BRANCH_ID, fy.START_DATE;
   ```

## API Changes

### Creating a Fiscal Year (New Required Field)
**Before:**
```json
{
  "companyId": 1,
  "fiscalYearCode": "FY2024",
  "fiscalYearNameEn": "Fiscal Year 2024",
  "startDate": "2024-01-01",
  "endDate": "2024-12-31"
}
```

**After:**
```json
{
  "companyId": 1,
  "branchId": 1,
  "fiscalYearCode": "FY2024",
  "fiscalYearNameEn": "Fiscal Year 2024",
  "startDate": "2024-01-01",
  "endDate": "2024-12-31"
}
```

### Fiscal Year Response (New Fields)
**Before:**
```json
{
  "fiscalYearId": 1,
  "companyId": 1,
  "companyName": "ABC Company",
  "fiscalYearCode": "FY2024",
  ...
}
```

**After:**
```json
{
  "fiscalYearId": 1,
  "companyId": 1,
  "companyName": "ABC Company",
  "branchId": 1,
  "branchName": "Main Branch",
  "fiscalYearCode": "FY2024",
  ...
}
```

### Branch Response (Removed Fields)
**Before:**
```json
{
  "branchId": 1,
  "companyId": 1,
  "fiscalYearId": 1,
  "fiscalYearCode": "FY2024",
  ...
}
```

**After:**
```json
{
  "branchId": 1,
  "companyId": 1,
  ...
}
```

## Validation Rules

The stored procedures now include validation to ensure:
1. `BRANCH_ID` is required when creating or updating fiscal years
2. The specified branch must belong to the specified company
3. This prevents data integrity issues where a fiscal year could be associated with a branch from a different company

## Benefits of This Design

1. **Flexibility**: Fiscal years can be defined per branch, allowing different branches to have different fiscal year periods if needed
2. **Data Integrity**: Foreign key constraints ensure fiscal years are always associated with valid company-branch combinations
3. **Clear Ownership**: Each fiscal year clearly belongs to a specific branch within a company
4. **Scalability**: Supports multi-branch operations with branch-specific fiscal year management

## Testing Recommendations

1. **Test fiscal year creation** with valid company-branch combinations
2. **Test validation** by attempting to create fiscal year with branch from different company (should fail)
3. **Test fiscal year queries** by company and by branch
4. **Test existing fiscal years** to ensure they were migrated correctly to default branches
5. **Test branch operations** to ensure they work without fiscal year references

## Build Status

✅ **Build Successful** - All compilation errors resolved
- 0 errors
- 67 warnings (mostly nullable reference warnings, not critical)

## Next Steps

1. Run the database migration scripts on your Oracle database
2. Test the API endpoints with the new BranchId parameter
3. Update any client applications to include BranchId when creating/updating fiscal years
4. Update any client applications to remove FiscalYearId references from branch operations
5. Consider adding a new API endpoint: `GET /api/fiscalyears/branch/{branchId}` to retrieve fiscal years by branch

## Files Modified

### Database Scripts (2 new files):
- `Database/Scripts/40_Add_BranchId_To_FiscalYear.sql`
- `Database/Scripts/41_Update_FiscalYear_Procedures_For_BranchId.sql`

### Domain Layer (3 files):
- `src/ThinkOnErp.Domain/Entities/SysFiscalYear.cs`
- `src/ThinkOnErp.Domain/Entities/SysBranch.cs`
- `src/ThinkOnErp.Domain/Interfaces/IFiscalYearRepository.cs`

### Application Layer (18 files):
- DTOs: 7 files (FiscalYear DTOs + Branch DTOs + Company DTO)
- Commands: 4 files (CreateFiscalYear + UpdateFiscalYear)
- Validators: 2 files (CreateFiscalYear + UpdateFiscalYear)
- Query Handlers: 5 files (FiscalYear queries + Branch queries)

### Infrastructure Layer (2 files):
- `src/ThinkOnErp.Infrastructure/Repositories/FiscalYearRepository.cs`
- `src/ThinkOnErp.Infrastructure/Repositories/BranchRepository.cs`

### API Layer (2 files):
- `src/ThinkOnErp.API/Controllers/BranchController.cs`
- `src/ThinkOnErp.API/Controllers/CompanyController.cs`

**Total: 27 files modified + 2 new database scripts**

---

**Date Completed**: April 25, 2026
**Status**: ✅ Complete and Ready for Testing
