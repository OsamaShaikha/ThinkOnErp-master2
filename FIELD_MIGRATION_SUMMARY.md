# Field Migration from SYS_COMPANY to SYS_BRANCH - Summary

## Overview
Moving ROUNDING_RULES, DEFAULT_LANG, and BASE_CURRENCY_ID from SYS_COMPANY to SYS_BRANCH to support branch-level operational settings.

## Progress Status

### ✅ Completed
1. **Database Migration Script**: Created `Database/Scripts/32_Move_Fields_From_Company_To_Branch.sql`
2. **Domain Entities**: Updated `SysBranch` and `SysCompany` entities
3. **DTOs**: Updated all Branch and Company DTOs
4. **Application Layer**: Fixed Company command and query handlers
5. **BranchRepository**: Updated to handle new fields

### 🔄 In Progress
1. **CompanyRepository**: Needs to remove references to migrated fields

### ⏳ Pending
1. **Command/Query Classes**: Update CreateCompanyCommand, UpdateCompanyCommand
2. **Branch Command/Query Handlers**: Update to handle new fields
3. **Controllers**: Update Company and Branch controllers
4. **Validation**: Update validators for new field constraints
5. **Testing**: Update existing tests

## Files Modified So Far

### Database
- `Database/Scripts/32_Move_Fields_From_Company_To_Branch.sql` - Migration script

### Domain Entities
- `src/ThinkOnErp.Domain/Entities/SysBranch.cs` - Added DefaultLang, BaseCurrencyId, RoundingRules
- `src/ThinkOnErp.Domain/Entities/SysCompany.cs` - Removed DefaultLang, BaseCurrencyId, RoundingRules

### DTOs
- `src/ThinkOnErp.Application/DTOs/Branch/CreateBranchDto.cs` - Added new fields
- `src/ThinkOnErp.Application/DTOs/Branch/BranchDto.cs` - Added new fields
- `src/ThinkOnErp.Application/DTOs/Branch/UpdateBranchDto.cs` - Added new fields
- `src/ThinkOnErp.Application/DTOs/Company/CreateCompanyDto.cs` - Moved fields to branch properties
- `src/ThinkOnErp.Application/DTOs/Company/CompanyDto.cs` - Removed migrated fields
- `src/ThinkOnErp.Application/DTOs/Company/UpdateCompanyDto.cs` - Removed migrated fields

### Application Layer
- `src/ThinkOnErp.Application/Features/Companies/Queries/GetCompanyById/GetCompanyByIdQueryHandler.cs` - Fixed
- `src/ThinkOnErp.Application/Features/Companies/Queries/GetAllCompanies/GetAllCompaniesQueryHandler.cs` - Fixed
- `src/ThinkOnErp.Application/Features/Companies/Commands/CreateCompany/CreateCompanyCommandHandler.cs` - Fixed
- `src/ThinkOnErp.Application/Features/Companies/Commands/UpdateCompany/UpdateCompanyCommandHandler.cs` - Fixed

### Infrastructure Layer
- `src/ThinkOnErp.Infrastructure/Repositories/BranchRepository.cs` - Updated for new fields

## Current Issue
CompanyRepository still references the migrated fields and needs to be updated to remove:
- P_DEFAULT_LANG parameter
- P_BASE_CURRENCY_ID parameter  
- P_ROUNDING_RULES parameter
- DefaultLang, BaseCurrencyId, RoundingRules in MapToEntity method

## Next Steps
1. Fix CompanyRepository compilation errors
2. Update Command/Query classes
3. Update Branch handlers
4. Update Controllers
5. Run database migration script
6. Test the changes

## Migration Benefits
- **Branch-level Settings**: Each branch can have its own language, currency, and rounding rules
- **Multi-branch Support**: Better support for companies with multiple branches in different regions
- **Operational Flexibility**: Branch managers can configure settings appropriate for their location
- **Data Consistency**: Settings are applied at the operational level (branch) rather than company level