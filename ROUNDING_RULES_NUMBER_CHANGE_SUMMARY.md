# ROUNDING_RULES Data Type Change: VARCHAR2 to NUMBER

## Summary
Changed the ROUNDING_RULES field from VARCHAR2(50) to NUMBER across the entire application to use numeric codes instead of text strings for better performance and consistency.

## Mapping
- **1** = HALF_UP (default)
- **2** = HALF_DOWN  
- **3** = UP
- **4** = DOWN
- **5** = CEILING
- **6** = FLOOR

## Changes Made

### ✅ Database Layer
**File**: `Database/Scripts/32_Move_Fields_From_Company_To_Branch.sql`
- Changed column definition: `ROUNDING_RULES NUMBER DEFAULT 1`
- Updated check constraint: `CHECK (ROUNDING_RULES IN (1, 2, 3, 4, 5, 6))`
- Updated data migration to convert string values to numbers
- Updated all stored procedures to use NUMBER parameter type
- Updated procedure validation logic

### ✅ Domain Layer
**File**: `src/ThinkOnErp.Domain/Entities/SysBranch.cs`
- Changed property type: `public int? RoundingRules { get; set; }`
- Updated documentation to reflect numeric codes

### ✅ Application Layer - DTOs
**Files Updated**:
- `src/ThinkOnErp.Application/DTOs/Branch/CreateBranchDto.cs`
- `src/ThinkOnErp.Application/DTOs/Branch/BranchDto.cs`
- `src/ThinkOnErp.Application/DTOs/Branch/UpdateBranchDto.cs`
- `src/ThinkOnErp.Application/DTOs/Company/CreateCompanyDto.cs`

**Changes**: 
- Changed `RoundingRules` property type from `string?` to `int?`
- Updated documentation to show numeric codes

### ✅ Application Layer - Commands
**File**: `src/ThinkOnErp.Application/Features/Companies/Commands/CreateCompanyWithBranch/CreateCompanyWithBranchCommand.cs`
- Changed `RoundingRules` property type from `string` to `int`
- Updated default value from `"HALF_UP"` to `1`

### ✅ Infrastructure Layer - Repositories
**File**: `src/ThinkOnErp.Infrastructure/Repositories/BranchRepository.cs`
- Updated `CreateAsync` method: Changed `OracleDbType.Varchar2` to `OracleDbType.Decimal`
- Updated `UpdateAsync` method: Changed `OracleDbType.Varchar2` to `OracleDbType.Decimal`
- Updated `MapToEntity` method: Changed `reader.GetString()` to `reader.GetInt32()`
- Updated default values from `"HALF_UP"` to `1`

**File**: `src/ThinkOnErp.Infrastructure/Repositories/CompanyRepository.cs`
- Updated `CreateWithBranchAsync` method signature: Changed `string? roundingRules` to `int? roundingRules`
- Updated parameter handling: Changed `OracleDbType.Varchar2` to `OracleDbType.Decimal`
- Updated default value from `"HALF_UP"` to `1`

### ✅ API Layer - Controllers
**File**: `src/ThinkOnErp.API/Controllers/CompanyController.cs`
- Updated `CreateCompany` method: Changed default value from `"HALF_UP"` to `1`

## Database Migration Impact

The migration script now includes:
1. **Column Definition**: `ROUNDING_RULES NUMBER DEFAULT 1`
2. **Data Migration**: Converts existing string values to numeric codes:
   ```sql
   CASE 
       WHEN c.ROUNDING_RULES = 'HALF_UP' THEN 1
       WHEN c.ROUNDING_RULES = 'HALF_DOWN' THEN 2
       WHEN c.ROUNDING_RULES = 'UP' THEN 3
       WHEN c.ROUNDING_RULES = 'DOWN' THEN 4
       WHEN c.ROUNDING_RULES = 'CEILING' THEN 5
       WHEN c.ROUNDING_RULES = 'FLOOR' THEN 6
       ELSE 1 -- Default to HALF_UP
   END
   ```
3. **Constraints**: `CHECK (ROUNDING_RULES IN (1, 2, 3, 4, 5, 6))`
4. **Stored Procedures**: All procedures updated to handle NUMBER type

## API Impact

### Request/Response Changes
- All API endpoints now expect/return numeric codes instead of strings
- **CreateCompany**: `BranchRoundingRules` now accepts `int?` (1-6)
- **CreateBranch**: `RoundingRules` now accepts `int?` (1-6)  
- **UpdateBranch**: `RoundingRules` now accepts `int?` (1-6)
- **GetBranch**: `RoundingRules` now returns `int?` (1-6)

### Example API Payloads
**Before (String)**:
```json
{
  "branchRoundingRules": "HALF_UP"
}
```

**After (Number)**:
```json
{
  "branchRoundingRules": 1
}
```

## Benefits

1. **Performance**: Numeric comparisons are faster than string comparisons
2. **Storage**: Numbers require less storage space than strings
3. **Consistency**: Eliminates case sensitivity and typo issues
4. **Validation**: Database constraints ensure only valid values
5. **Internationalization**: Numeric codes work across all languages

## Client Application Updates Required

Client applications will need to update their code to:
1. Send numeric codes (1-6) instead of strings
2. Handle numeric codes in responses
3. Update UI dropdowns/selectors to use numeric values
4. Update validation logic to accept 1-6 instead of string values

## Rollback Plan

If rollback is needed:
1. Revert all code changes to use `string` type
2. Execute reverse migration script to convert back to VARCHAR2
3. Update stored procedures to handle VARCHAR2 parameters
4. Restore string-based validation logic

---
**Status**: Code changes complete, ready for database migration execution  
**Risk Level**: Medium (requires client application updates)  
**Testing Required**: Full API testing with numeric values