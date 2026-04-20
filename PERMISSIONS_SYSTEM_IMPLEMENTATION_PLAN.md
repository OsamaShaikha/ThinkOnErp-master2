# Permissions System - Complete Implementation Plan

## Overview
This document outlines the complete implementation plan for the multi-tenant permissions system with 8 core entities.

## Entities to Implement

1. **SysSuperAdmin** - Super admin accounts with 2FA ✅ (PRIORITY - Will implement fully)
2. **SysSystem** - Available systems/modules ✅ (Entity created)
3. **SysScreen** - Screens/pages within systems ✅ (Entity created)
4. **SysCompanySystem** - System access control per company
5. **SysRoleScreenPermission** - Screen permissions per role
6. **SysUserRole** - User to role assignments
7. **SysUserScreenPermission** - Direct user permission overrides
8. **SysAuditLog** - Audit trail (already exists, may need extension)

## Implementation Checklist

### Phase 1: Domain Layer (8 entities)
- [x] SysSuperAdmin entity
- [x] SysSystem entity
- [x] SysScreen entity
- [ ] SysCompanySystem entity
- [ ] SysRoleScreenPermission entity
- [ ] SysUserRole entity
- [ ] SysUserScreenPermission entity
- [ ] Update SysAuditLog entity (if needed)

### Phase 2: Repository Interfaces (8 interfaces)
- [ ] ISuperAdminRepository
- [ ] ISystemRepository
- [ ] IScreenRepository
- [ ] ICompanySystemRepository
- [ ] IRoleScreenPermissionRepository
- [ ] IUserRoleRepository
- [ ] IUserScreenPermissionRepository
- [ ] Update IAuditLogRepository (if needed)

### Phase 3: Database Layer (Stored Procedures)
- [ ] 10_Create_SYS_SUPER_ADMIN_Procedures.sql
- [ ] 10_Create_SYS_SYSTEM_Procedures.sql
- [ ] 10_Create_SYS_SCREEN_Procedures.sql
- [ ] 10_Create_SYS_COMPANY_SYSTEM_Procedures.sql
- [ ] 10_Create_SYS_ROLE_SCREEN_PERMISSION_Procedures.sql
- [ ] 10_Create_SYS_USER_ROLE_Procedures.sql
- [ ] 10_Create_SYS_USER_SCREEN_PERMISSION_Procedures.sql

### Phase 4: Repository Implementations (8 repositories)
- [ ] SuperAdminRepository
- [ ] SystemRepository
- [ ] ScreenRepository
- [ ] CompanySystemRepository
- [ ] RoleScreenPermissionRepository
- [ ] UserRoleRepository
- [ ] UserScreenPermissionRepository
- [ ] Update AuditLogRepository (if needed)

### Phase 5: Application Layer - DTOs (24+ DTOs)

#### SuperAdmin DTOs
- [ ] SuperAdminDto
- [ ] CreateSuperAdminDto
- [ ] UpdateSuperAdminDto

#### System DTOs
- [ ] SystemDto
- [ ] CreateSystemDto
- [ ] UpdateSystemDto

#### Screen DTOs
- [ ] ScreenDto
- [ ] CreateScreenDto
- [ ] UpdateScreenDto

#### CompanySystem DTOs
- [ ] CompanySystemDto
- [ ] CreateCompanySystemDto
- [ ] UpdateCompanySystemDto

#### RoleScreenPermission DTOs
- [ ] RoleScreenPermissionDto
- [ ] CreateRoleScreenPermissionDto
- [ ] UpdateRoleScreenPermissionDto

#### UserRole DTOs
- [ ] UserRoleDto
- [ ] AssignUserRoleDto

#### UserScreenPermission DTOs
- [ ] UserScreenPermissionDto
- [ ] CreateUserScreenPermissionDto
- [ ] UpdateUserScreenPermissionDto

### Phase 6: Application Layer - Commands & Queries (56+ files)

#### SuperAdmin Commands/Queries
- [ ] CreateSuperAdminCommand + Handler + Validator
- [ ] UpdateSuperAdminCommand + Handler + Validator
- [ ] DeleteSuperAdminCommand + Handler + Validator
- [ ] GetAllSuperAdminsQuery + Handler
- [ ] GetSuperAdminByIdQuery + Handler
- [ ] ChangePasswordCommand + Handler + Validator
- [ ] Enable2FACommand + Handler + Validator
- [ ] Disable2FACommand + Handler + Validator

#### System Commands/Queries
- [ ] CreateSystemCommand + Handler + Validator
- [ ] UpdateSystemCommand + Handler + Validator
- [ ] DeleteSystemCommand + Handler + Validator
- [ ] GetAllSystemsQuery + Handler
- [ ] GetSystemByIdQuery + Handler

#### Screen Commands/Queries
- [ ] CreateScreenCommand + Handler + Validator
- [ ] UpdateScreenCommand + Handler + Validator
- [ ] DeleteScreenCommand + Handler + Validator
- [ ] GetAllScreensQuery + Handler
- [ ] GetScreenByIdQuery + Handler
- [ ] GetScreensBySystemIdQuery + Handler

#### CompanySystem Commands/Queries
- [ ] GrantSystemAccessCommand + Handler + Validator
- [ ] RevokeSystemAccessCommand + Handler + Validator
- [ ] GetCompanySystemsQuery + Handler
- [ ] GetSystemCompa niesQuery + Handler

#### RoleScreenPermission Commands/Queries
- [ ] SetRoleScreenPermissionCommand + Handler + Validator
- [ ] GetRoleScreenPermissionsQuery + Handler
- [ ] GetScreenRolePermissionsQuery + Handler

#### UserRole Commands/Queries
- [ ] AssignUserRoleCommand + Handler + Validator
- [ ] RemoveUserRoleCommand + Handler + Validator
- [ ] GetUserRolesQuery + Handler
- [ ] GetRoleUsersQuery + Handler

#### UserScreenPermission Commands/Queries
- [ ] SetUserScreenPermissionCommand + Handler + Validator
- [ ] RemoveUserScreenPermissionCommand + Handler + Validator
- [ ] GetUserScreenPermissionsQuery + Handler
- [ ] CheckUserPermissionQuery + Handler

### Phase 7: API Controllers (8 controllers)
- [ ] SuperAdminController
- [ ] SystemsController
- [ ] ScreensController
- [ ] CompanySystemsController
- [ ] RoleScreenPermissionsController
- [ ] UserRolesController
- [ ] UserScreenPermissionsController
- [ ] PermissionsCheckController (helper for checking permissions)

### Phase 8: DI Registration
- [ ] Register all repositories in DependencyInjection.cs
- [ ] Register any new services

### Phase 9: Testing & Documentation
- [ ] Unit tests for repositories
- [ ] Unit tests for command handlers
- [ ] Integration tests for API endpoints
- [ ] API documentation
- [ ] User guide for permissions system

## File Structure

```
src/ThinkOnErp.Domain/
├── Entities/
│   ├── SysSuperAdmin.cs ✅
│   ├── SysSystem.cs ✅
│   ├── SysScreen.cs ✅
│   ├── SysCompanySystem.cs (exists)
│   ├── SysRoleScreenPermission.cs
│   ├── SysUserRole.cs
│   └── SysUserScreenPermission.cs
└── Interfaces/
    ├── ISuperAdminRepository.cs
    ├── ISystemRepository.cs
    ├── IScreenRepository.cs
    ├── ICompanySystemRepository.cs
    ├── IRoleScreenPermissionRepository.cs
    ├── IUserRoleRepository.cs
    └── IUserScreenPermissionRepository.cs

src/ThinkOnErp.Infrastructure/
└── Repositories/
    ├── SuperAdminRepository.cs
    ├── SystemRepository.cs
    ├── ScreenRepository.cs
    ├── CompanySystemRepository.cs
    ├── RoleScreenPermissionRepository.cs
    ├── UserRoleRepository.cs
    └── UserScreenPermissionRepository.cs

src/ThinkOnErp.Application/
├── DTOs/
│   ├── SuperAdmin/
│   ├── System/
│   ├── Screen/
│   ├── CompanySystem/
│   ├── RoleScreenPermission/
│   ├── UserRole/
│   └── UserScreenPermission/
└── Features/
    ├── SuperAdmins/
    ├── Systems/
    ├── Screens/
    ├── CompanySystemAccess/
    ├── RoleScreenPermissions/
    ├── UserRoles/
    └── UserScreenPermissions/

src/ThinkOnErp.API/
└── Controllers/
    ├── SuperAdminController.cs
    ├── SystemsController.cs
    ├── ScreensController.cs
    ├── CompanySystemsController.cs
    ├── RoleScreenPermissionsController.cs
    ├── UserRolesController.cs
    └── UserScreenPermissionsController.cs

Database/Scripts/
├── 10_Create_SYS_SUPER_ADMIN_Procedures.sql
├── 10_Create_SYS_SYSTEM_Procedures.sql
├── 10_Create_SYS_SCREEN_Procedures.sql
├── 10_Create_SYS_COMPANY_SYSTEM_Procedures.sql
├── 10_Create_SYS_ROLE_SCREEN_PERMISSION_Procedures.sql
├── 10_Create_SYS_USER_ROLE_Procedures.sql
└── 10_Create_SYS_USER_SCREEN_PERMISSION_Procedures.sql
```

## Estimated File Count
- **Domain Entities**: 5 new (3 created)
- **Repository Interfaces**: 7 new
- **Repository Implementations**: 7 new
- **Stored Procedure Files**: 7 new
- **DTOs**: ~24 files
- **Commands/Queries/Handlers/Validators**: ~56 files
- **Controllers**: 7 new
- **Total**: ~108 new files

## Priority Implementation Order

### PHASE 1 (CRITICAL - Implement First)
1. **SuperAdmin** - Complete CRUD with 2FA support
2. **System** - Manage available modules
3. **Screen** - Manage screens within systems

### PHASE 2 (HIGH PRIORITY)
4. **CompanySystem** - Control which systems companies can access
5. **RoleScreenPermission** - Define what roles can do on screens

### PHASE 3 (MEDIUM PRIORITY)
6. **UserRole** - Assign roles to users
7. **UserScreenPermission** - Override permissions for specific users

### PHASE 4 (ENHANCEMENT)
8. **Permission Checking Service** - Helper service to check user permissions
9. **Permission Middleware** - Automatic permission checking
10. **Audit Logging** - Enhanced audit trail

## Next Steps

1. ✅ Create domain entities (3 done)
2. **Implement complete SuperAdmin functionality** (next)
   - Repository interface
   - Stored procedures
   - Repository implementation
   - DTOs
   - Commands/Queries/Handlers/Validators
   - Controller
   - DI registration
3. Use SuperAdmin as template for remaining entities
4. Test and validate
5. Document API endpoints

## Notes

- Follow existing patterns from Users, Roles, Companies, Branches
- Use password hashing for SuperAdmin passwords
- Implement 2FA using TOTP (Time-based One-Time Password)
- All delete operations should be soft deletes (IS_ACTIVE = '0')
- Include comprehensive logging and audit trails
- Use AdminOnly authorization for all management endpoints
- SuperAdmin operations should require SuperAdmin authentication
