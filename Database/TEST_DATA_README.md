# Test Data Documentation

This document contains information about the test data inserted into the ThinkOnErp database.

## How to Load Test Data

Run the SQL scripts in the following order:

1. `01_Create_Sequences.sql` - Creates Oracle sequences
2. `02_Create_SYS_ROLE_Procedures.sql` - Creates role stored procedures
3. `03_Create_SYS_CURRENCY_Procedures.sql` - Creates currency stored procedures
4. `04_Create_SYS_COMPANY_Procedures.sql` - Creates company stored procedures
5. `04_Create_SYS_BRANCH_Procedures.sql` - Creates branch stored procedures
6. `05_Create_SYS_USERS_Procedures.sql` - Creates user stored procedures
7. `06_Insert_Test_Data.sql` - Inserts test data

## Test User Credentials

### Admin User (Full Access)
- **Username**: `admin`
- **Password**: `Admin@123`
- **Role**: System Administrator
- **Is Admin**: Yes
- **Email**: admin@thinkon.com
- **Branch**: Head Office - Riyadh

### Manager User
- **Username**: `ahmed.mohammed`
- **Password**: `Manager@123`
- **Role**: Manager
- **Is Admin**: No
- **Email**: ahmed@thinkon.com
- **Branch**: Jeddah Branch

### Accountant User
- **Username**: `fatima.ali`
- **Password**: `Account@123`
- **Role**: Accountant
- **Is Admin**: No
- **Email**: fatima@thinkon.com
- **Branch**: Dammam Branch

### Employee User
- **Username**: `khaled.saeed`
- **Password**: `Employee@123`
- **Role**: Employee
- **Is Admin**: No
- **Email**: khaled@advtech.com
- **Branch**: Head Office - Khobar

### Auditor User
- **Username**: `sara.hassan`
- **Password**: `Auditor@123`
- **Role**: Auditor
- **Is Admin**: No
- **Email**: sara@smartsolutions.com
- **Branch**: Head Office - Dubai

### Inactive User (For Testing)
- **Username**: `inactive.user`
- **Password**: `Test@123`
- **Role**: Employee
- **Is Admin**: No
- **Is Active**: No (Disabled)
- **Email**: inactive@thinkon.com

## Test Data Summary

### Roles (5 records)
1. System Administrator - Full system access
2. Manager - Department manager
3. Accountant - Financial operations
4. Employee - Regular employee
5. Auditor - Internal auditor

### Currencies (4 records)
1. US Dollar (USD) - Rate: 1.00
2. Euro (EUR) - Rate: 1.08
3. Saudi Riyal (SAR) - Rate: 0.27
4. British Pound (GBP) - Rate: 1.27

### Companies (3 records)
1. ThinkOn Company - USD currency
2. Advanced Technology Corporation - SAR currency
3. Smart Solutions Inc - EUR currency

### Branches (5 records)
1. Head Office - Riyadh (ThinkOn Company)
2. Jeddah Branch (ThinkOn Company)
3. Dammam Branch (ThinkOn Company)
4. Head Office - Khobar (Advanced Technology Corporation)
5. Head Office - Dubai (Smart Solutions Inc)

### Users (6 records)
- 1 Admin user (full access)
- 4 Regular users (different roles)
- 1 Inactive user (for testing authentication failures)

## Testing Scenarios

### Authentication Tests
1. **Valid Login**: Use `admin` / `Admin@123` - Should succeed
2. **Invalid Password**: Use `admin` / `WrongPassword` - Should return 401
3. **Invalid Username**: Use `nonexistent` / `Admin@123` - Should return 401
4. **Inactive User**: Use `inactive.user` / `Test@123` - Should return 401

### Authorization Tests
1. **Admin Access**: Login as `admin` - Can access all endpoints
2. **Non-Admin Access**: Login as `ahmed.mohammed` - Cannot access admin-only endpoints (should return 403)
3. **Protected Endpoints**: Access without token - Should return 401

### CRUD Operations Tests
Use the admin user to test:
- Create new roles, currencies, companies, branches, users
- Read all records and individual records
- Update existing records
- Delete records (soft delete - sets IS_ACTIVE to false)

### Password Change Tests
1. Login as any user
2. Change password using the change-password endpoint
3. Verify old password no longer works
4. Verify new password works

## API Testing Examples

### Login Request
```bash
POST http://localhost:5000/api/auth/login
Content-Type: application/json

{
  "userName": "admin",
  "password": "Admin@123"
}
```

### Get All Roles (Requires Authentication)
```bash
GET http://localhost:5000/api/roles
Authorization: Bearer {your-jwt-token}
```

### Create Role (Requires Admin)
```bash
POST http://localhost:5000/api/roles
Authorization: Bearer {admin-jwt-token}
Content-Type: application/json

{
  "rowDesc": "دور جديد",
  "rowDescE": "New Role",
  "note": "Test role"
}
```

## Password Hashing

All passwords are hashed using SHA-256 and stored as hexadecimal strings (64 characters).

Example:
- Plain text: `Admin@123`
- SHA-256 Hash: `8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918`

To generate a new password hash, you can use:
```csharp
using System.Security.Cryptography;
using System.Text;

var password = "YourPassword";
var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
```

## Notes

- All active records have `IS_ACTIVE = '1'`
- Inactive records have `IS_ACTIVE = '0'`
- Soft delete sets `IS_ACTIVE = '0'` instead of physically removing records
- All timestamps use Oracle `SYSDATE`
- Phone numbers use international format
- Email addresses are unique per user

## Cleanup

To remove all test data:
```sql
DELETE FROM SYS_USERS;
DELETE FROM SYS_BRANCH;
DELETE FROM SYS_COMPANY;
DELETE FROM SYS_CURRENCY;
DELETE FROM SYS_ROLE;
COMMIT;
```

To reset sequences:
```sql
DROP SEQUENCE SEQ_SYS_USERS;
DROP SEQUENCE SEQ_SYS_BRANCH;
DROP SEQUENCE SEQ_SYS_COMPANY;
DROP SEQUENCE SEQ_SYS_CURRENCY;
DROP SEQUENCE SEQ_SYS_ROLE;

-- Then re-run 01_Create_Sequences.sql
```
