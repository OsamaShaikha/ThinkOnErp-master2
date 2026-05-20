# Deployment and Build Status

## Current Issues

### 1. Docker Deployment - Database Connection Failure

**Error**: `ORA-50232: Network Transport: TCP transport address connect failure for host 178.104.126.99 port 1521`

**Root Cause**: The Docker container cannot reach the Oracle database server.

**Possible Solutions**:

1. **Check Oracle Database Status**:
   ```bash
   # On the database server (178.104.126.99)
   lsnrctl status
   ```

2. **Verify Network Connectivity**:
   ```bash
   # From the Docker host
   telnet 178.104.126.99 1521
   # or
   nc -zv 178.104.126.99 1521
   ```

3. **Check Firewall Rules**:
   - Ensure port 1521 is open on the database server
   - Check if Docker network can reach external hosts

4. **Verify Connection String**:
   - Check `.env` file or `appsettings.Production.json`
   - Ensure the connection string is correct

5. **Oracle Listener Configuration**:
   - Verify the listener is configured to accept connections from the Docker host IP
   - Check `listener.ora` and `tnsnames.ora` files

**Temporary Workaround**:
If the database is on the same server as Docker, use the host's internal IP or `host.docker.internal`:
```bash
docker run -d --name thinkonerp-api \
  --add-host=host.docker.internal:host-gateway \
  -p 5000:5000 \
  -e ConnectionStrings__OracleDb="Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=host.docker.internal)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)));User Id=your_user;Password=your_password;" \
  devosamaarori/thinkonerp-api:v1
```

---

### 2. Local Build Errors - Must Fix Before New Deployment

**Status**: ❌ Build failing with 12 errors

**Critical Errors**:

#### A. Branch Permissions - Type Mismatch (2 errors)
- **File**: `GrantSystemAccessCommandHandler.cs`, `GrantScreenAccessCommandHandler.cs`
- **Error**: Cannot convert from 'long' to 'string' for `GrantedBy` parameter
- **Status**: ✅ **FIXED** - Changed `GrantedBy` from `string` to `long` in Commands and DTOs

#### B. SysSuperAdmin Entity - Missing Properties (10 errors)
- **Files**: Multiple SuperAdmin feature handlers
- **Error**: `SysSuperAdmin` does not contain definitions for `Id`, `NameAr`, `NameEn`
- **Status**: ⚠️ **NEEDS INVESTIGATION** - Entity definition mismatch

**Files with Errors**:
1. `GetAllSuperAdminsQueryHandler.cs` - Lines 22, 23, 24
2. `GetSuperAdminByIdQueryHandler.cs` - Lines 25, 26, 27
3. `UpdateSuperAdminCommandHandler.cs` - Lines 23, 24
4. `CreateSuperAdminCommandHandler.cs` - Lines 37, 38

---

## Branch-Level Permissions Implementation Status

### ✅ Completed Components

1. **Database Layer**:
   - ✅ Tables: `SYS_BRANCH_SYSTEM`, `SYS_BRANCH_SCREEN`
   - ✅ Stored Procedures: All 12 procedures created
   - ✅ Sequences and Indexes

2. **Domain Layer**:
   - ✅ Entities: `SysBranchSystem`, `SysBranchScreen`
   - ✅ Repository Interface: `IBranchPermissionRepository`

3. **Infrastructure Layer**:
   - ✅ Repository Implementation: `BranchPermissionRepository`
   - ✅ DI Registration: Already registered in `DependencyInjection.cs`

4. **Application Layer**:
   - ✅ DTOs: 6 DTOs created (fixed type mismatches)
   - ✅ Commands: 4 command handlers
   - ✅ Queries: 4 query handlers (including `GetAllScreensQueryHandler`)

5. **API Layer**:
   - ✅ Controller: `BranchPermissionsController` with 8 endpoints

### ⚠️ Pending Tasks

1. **Fix SysSuperAdmin Entity Issues**:
   - Investigate the entity definition
   - Ensure properties match what handlers expect
   - Fix all 10 compilation errors

2. **Test Database Scripts**:
   - Execute `88_Create_Branch_Level_Permissions.sql`
   - Execute `89_Create_Branch_Permission_Procedures.sql`
   - Verify tables and procedures work correctly

3. **Test API Endpoints**:
   - Test with Postman/Swagger after build succeeds
   - Verify super admin authorization
   - Test grant/revoke operations

---

## Next Steps

### Immediate Actions (Priority Order):

1. **Fix Build Errors** (CRITICAL):
   ```bash
   # Check SysSuperAdmin entity definition
   # Fix property names or update handlers
   # Rebuild project
   dotnet build
   ```

2. **Fix Database Connection** (CRITICAL for deployment):
   - Verify Oracle database is running
   - Check network connectivity
   - Update connection string if needed

3. **Test Branch Permissions** (After build succeeds):
   - Run database scripts
   - Test API endpoints locally
   - Verify authorization

4. **Deploy New Version** (After all tests pass):
   - Build new Docker image
   - Push to Docker Hub
   - Deploy to server

---

## API Endpoints Summary

### Branch Permissions Controller
**Base Route**: `/api/superadmin/branches`
**Authorization**: Super Admin Only

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/{branchId}/systems/{systemId}/grant` | Grant system access to branch |
| POST | `/{branchId}/systems/{systemId}/revoke` | Revoke system access from branch |
| POST | `/{branchId}/screens/{screenId}/grant` | Grant screen access to branch |
| POST | `/{branchId}/screens/{screenId}/revoke` | Revoke screen access from branch |
| GET | `/{branchId}/systems` | Get all system permissions for branch |
| GET | `/{branchId}/screens` | Get all screen permissions for branch |
| GET | `/systems` | Get all available systems |
| GET | `/screens` | Get all available screens |

---

## Files Created/Modified

### New Files:
1. `Database/Scripts/88_Create_Branch_Level_Permissions.sql`
2. `Database/Scripts/89_Create_Branch_Permission_Procedures.sql`
3. `src/ThinkOnErp.Domain/Entities/SysBranchSystem.cs`
4. `src/ThinkOnErp.Domain/Entities/SysBranchScreen.cs`
5. `src/ThinkOnErp.Domain/Interfaces/IBranchPermissionRepository.cs`
6. `src/ThinkOnErp.Infrastructure/Repositories/BranchPermissionRepository.cs`
7. `src/ThinkOnErp.Application/DTOs/BranchPermission/*.cs` (6 files)
8. `src/ThinkOnErp.Application/Features/BranchPermissions/**/*.cs` (9 files)
9. `src/ThinkOnErp.API/Controllers/BranchPermissionsController.cs`
10. `Database/BRANCH_PERMISSIONS_ERD.md`
11. `BRANCH_LEVEL_PERMISSIONS_GUIDE.md`

### Modified Files:
1. `src/ThinkOnErp.Infrastructure/DependencyInjection.cs` - Already has registration
2. Project files - Fixed merge conflicts

---

## Contact

For questions or issues, refer to:
- `BRANCH_LEVEL_PERMISSIONS_GUIDE.md` - Implementation guide
- `Database/BRANCH_PERMISSIONS_ERD.md` - Database schema diagram
