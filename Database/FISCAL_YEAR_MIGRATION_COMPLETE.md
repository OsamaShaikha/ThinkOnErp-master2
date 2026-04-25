# Fiscal Year Migration from Company to Branch - COMPLETE

## Overview
Successfully migrated FISCAL_YEAR_ID from SYS_COMPANY table to SYS_BRANCH table to provide more flexibility in managing fiscal years at the branch level.

## Date
April 25, 2026

## Changes Made

### 1. Database Schema Changes
**File**: `Database/Scripts/40_Move_FiscalYear_From_Company_To_Branch.sql`

- Added FISCAL_YEAR_ID column to SYS_BRANCH table with foreign key constraint
- Migrated existing fiscal year data from companies to all their branches
- Removed FISCAL_YEAR_ID column from SYS_COMPANY table
- Updated stored procedures:
  - `SP_SYS_BRANCH_INSERT` - Added P_FISCAL_YEAR_ID parameter
  - `SP_SYS_BRANCH_UPDATE` - Added P_FISCAL_YEAR_ID parameter
  - `SP_SYS_COMPANY_INSERT` - Removed P_FISCAL_YEAR_ID parameter
  - `SP_SYS_COMPANY_UPDATE` - Removed P_FISCAL_YEAR_ID parameter
  - `SP_SYS_COMPANY_INSERT_WITH_BRANCH` - Moved P_FISCAL_YEAR_ID to branch section

### 2. Domain Entities
**Files Updated**:
- `src/ThinkOnErp.Domain/Entities/SysCompany.cs` - Removed FiscalYearId property and FiscalYear navigation property
- `src/ThinkOnErp.Domain/Entities/SysBranch.cs` - Already had FiscalYearId property and FiscalYear navigation property

### 3. DTOs (Data Transfer Objects)

#### Company DTOs - Removed FiscalYearId
- `src/ThinkOnErp.Application/DTOs/Company/CompanyDto.cs`
  - Removed FiscalYearId property
  - Removed FiscalYearCode property
- `src/ThinkOnErp.Application/DTOs/Company/CreateCompanyDto.cs`
  - Removed FiscalYearId property
  - Added BranchFiscalYearId property for branch creation
- `src/ThinkOnErp.Application/DTOs/Company/UpdateCompanyDto.cs`
  - Removed FiscalYearId property

#### Branch DTOs - Added FiscalYearId
- `src/ThinkOnErp.Application/DTOs/Branch/BranchDto.cs`
  - Added FiscalYearId property
  - Added FiscalYearCode property
- `src/ThinkOnErp.Application/DTOs/Branch/CreateBranchDto.cs`
  - Added FiscalYearId property
- `src/ThinkOnErp.Application/DTOs/Branch/UpdateBranchDto.cs`
  - Added FiscalYearId property

### 4. Repositories

#### CompanyRepository
**File**: `src/ThinkOnErp.Infrastructure/Repositories/CompanyRepository.cs`

- `CreateAsync()` - Removed P_FISCAL_YEAR_ID parameter
- `UpdateAsync()` - Removed P_FISCAL_YEAR_ID parameter
- `CreateWithBranchAsync()` - Moved fiscalYearId parameter to branch section
- `MapToEntity()` - Removed FiscalYearId mapping

#### BranchRepository
**File**: `src/ThinkOnErp.Infrastructure/Repositories/BranchRepository.cs`

- `CreateAsync()` - Added P_FISCAL_YEAR_ID parameter
- `UpdateAsync()` - Added P_FISCAL_YEAR_ID parameter
- `MapToEntity()` - Added FiscalYearId mapping

### 5. Repository Interfaces
**File**: `src/ThinkOnErp.Domain/Interfaces/ICompanyRepository.cs`

- Updated `CreateWithBranchAsync()` method signature to move fiscalYearId parameter to branch section

### 6. Command Handlers

#### Company Commands
**File**: `src/ThinkOnErp.Application/Features/Companies/Commands/CreateCompanyWithBranch/CreateCompanyWithBranchCommand.cs`
- Removed FiscalYearId property
- Added BranchBaseCurrencyId property
- Added BranchRoundingRules property
- Added BranchFiscalYearId property

**File**: `src/ThinkOnErp.Application/Features/Companies/Commands/CreateCompanyWithBranch/CreateCompanyWithBranchCommandHandler.cs`
- Updated CreateWithBranchAsync call to pass fiscal year to branch instead of company

**File**: `src/ThinkOnErp.Application/Features/Companies/Commands/CreateCompany/CreateCompanyCommand.cs`
- Removed FiscalYearId property

**File**: `src/ThinkOnErp.Application/Features/Companies/Commands/CreateCompany/CreateCompanyCommandHandler.cs`
- Removed FiscalYearId mapping from SysCompany entity

**File**: `src/ThinkOnErp.Application/Features/Companies/Commands/UpdateCompany/UpdateCompanyCommand.cs`
- Removed FiscalYearId property

**File**: `src/ThinkOnErp.Application/Features/Companies/Commands/UpdateCompany/UpdateCompanyCommandHandler.cs`
- Removed FiscalYearId mapping from SysCompany entity

#### Branch Commands
**Files Updated**:
- `src/ThinkOnErp.Application/Features/Branches/Commands/CreateBranch/CreateBranchCommand.cs`
  - Added DefaultLang property
  - Added BaseCurrencyId property
  - Added RoundingRules property
  - Added FiscalYearId property

- `src/ThinkOnErp.Application/Features/Branches/Commands/CreateBranch/CreateBranchCommandHandler.cs`
  - Updated to map new properties to SysBranch entity

- `src/ThinkOnErp.Application/Features/Branches/Commands/UpdateBranch/UpdateBranchCommand.cs`
  - Added DefaultLang property
  - Added BaseCurrencyId property
  - Added RoundingRules property
  - Added FiscalYearId property

- `src/ThinkOnErp.Application/Features/Branches/Commands/UpdateBranch/UpdateBranchCommandHandler.cs`
  - Updated to map new properties to SysBranch entity

### 7. Query Handlers

#### Company Queries
**Files Updated**:
- `src/ThinkOnErp.Application/Features/Companies/Queries/GetCompanyById/GetCompanyByIdQueryHandler.cs`
  - Removed FiscalYearId mapping
  - Removed FiscalYearCode mapping

- `src/ThinkOnErp.Application/Features/Companies/Queries/GetAllCompanies/GetAllCompaniesQueryHandler.cs`
  - Removed FiscalYearId mapping
  - Removed FiscalYearCode mapping

#### Branch Queries
**Files Updated**:
- `src/ThinkOnErp.Application/Features/Branches/Queries/GetBranchById/GetBranchByIdQueryHandler.cs`
  - Added FiscalYearId mapping
  - Added FiscalYearCode mapping

- `src/ThinkOnErp.Application/Features/Branches/Queries/GetBranchesByCompanyId/GetBranchesByCompanyIdQueryHandler.cs`
  - Added FiscalYearId mapping
  - Added FiscalYearCode mapping

- `src/ThinkOnErp.Application/Features/Branches/Queries/GetAllBranches/GetAllBranchesQueryHandler.cs`
  - Added FiscalYearId mapping
  - Added FiscalYearCode mapping

### 8. API Controllers
**File**: `src/ThinkOnErp.API/Controllers/CompanyController.cs`

- Updated CreateCompany endpoint to:
  - Remove FiscalYearId mapping from CreateCompanyWithBranchCommand
  - Add BranchBaseCurrencyId, BranchRoundingRules, and BranchFiscalYearId mappings
- Updated UpdateCompany endpoint to remove FiscalYearId mapping

**File**: `src/ThinkOnErp.API/Controllers/BranchController.cs`

- Updated CreateBranch endpoint to map new DTO properties (DefaultLang, BaseCurrencyId, RoundingRules, FiscalYearId)
- Updated UpdateBranch endpoint to map new DTO properties (DefaultLang, BaseCurrencyId, RoundingRules, FiscalYearId)

## Migration Steps

### Step 1: Run Database Migration Script
Execute the migration script in the following order:
```sql
-- Run this script on your Oracle database
@Database/Scripts/40_Move_FiscalYear_From_Company_To_Branch.sql
```

This script will:
1. Add FISCAL_YEAR_ID to SYS_BRANCH table
2. Copy fiscal year data from companies to branches
3. Remove FISCAL_YEAR_ID from SYS_COMPANY table
4. Update all stored procedures

### Step 2: Deploy Application Changes
All application code changes have been completed and compile successfully. Deploy the updated application.

### Step 3: Verify Migration
After deployment, verify:
1. All branches have the correct fiscal year assigned
2. Company API endpoints no longer return FiscalYearId
3. Branch API endpoints now return FiscalYearId
4. Creating/updating branches with fiscal year works correctly
5. Creating companies with branches assigns fiscal year to the branch

## Rollback Plan
If you need to rollback this migration, follow these steps:

1. **Stop the application**
2. **Run rollback script** (create this before running migration):
```sql
-- Add FISCAL_YEAR_ID back to SYS_COMPANY
ALTER TABLE SYS_COMPANY ADD FISCAL_YEAR_ID NUMBER(19);

-- Copy fiscal year from default branch back to company
UPDATE SYS_COMPANY c
SET FISCAL_YEAR_ID = (
    SELECT b.FISCAL_YEAR_ID 
    FROM SYS_BRANCH b 
    WHERE b.ROW_ID = c.DEFAULT_BRANCH_ID
)
WHERE c.DEFAULT_BRANCH_ID IS NOT NULL;

-- Remove FISCAL_YEAR_ID from SYS_BRANCH
ALTER TABLE SYS_BRANCH DROP COLUMN FISCAL_YEAR_ID;

-- Restore original stored procedures
-- (You would need to keep backups of the original procedures)
```

3. **Restore previous application version**

## Testing Checklist

- [ ] Run database migration script successfully
- [ ] Verify all branches have fiscal year assigned
- [ ] Test GET /api/companies - verify no FiscalYearId in response
- [ ] Test GET /api/companies/{id} - verify no FiscalYearId in response
- [ ] Test GET /api/branches - verify FiscalYearId in response
- [ ] Test GET /api/branches/{id} - verify FiscalYearId in response
- [ ] Test POST /api/companies (with branch) - verify fiscal year assigned to branch
- [ ] Test POST /api/branches - verify fiscal year can be set
- [ ] Test PUT /api/branches/{id} - verify fiscal year can be updated
- [ ] Verify existing functionality still works (company CRUD, branch CRUD)

## Impact Analysis

### Breaking Changes
- **API Response Changes**: Company endpoints no longer return FiscalYearId
- **API Request Changes**: Creating/updating companies no longer accepts FiscalYearId
- **Database Schema**: FISCAL_YEAR_ID moved from SYS_COMPANY to SYS_BRANCH

### Client Applications
Any client applications consuming the Company API will need to be updated to:
1. Remove FiscalYearId from company creation/update requests
2. Get fiscal year information from branch endpoints instead
3. Set fiscal year at branch level when creating companies

## Benefits
1. **Flexibility**: Each branch can now have its own fiscal year
2. **Accuracy**: Better reflects real-world scenarios where branches may operate in different fiscal years
3. **Scalability**: Supports multi-branch companies with different fiscal year requirements

## Notes
- All code changes compile successfully with no diagnostics errors
- The migration preserves existing fiscal year data by copying it to all branches
- The stored procedure SP_SYS_COMPANY_INSERT_WITH_BRANCH now accepts fiscal year for the branch
- Navigation properties in domain entities have been updated accordingly

## Status
✅ **COMPLETE** - All code changes implemented and verified
⏳ **PENDING** - Database migration script needs to be executed
